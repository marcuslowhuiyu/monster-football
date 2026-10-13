using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Scores off-ball behaviours for one player (ADR-018). <see cref="ActionKind.MoveIntoPosition"/> keeps the
/// team-shape target already assigned this tick; the others move the player elsewhere. Pressing
/// (<see cref="ActionKind.CloseDownOpponent"/>) is assigned team-wide by <see cref="TeamShapeSystem"/> and is
/// not generated here. Work is done in team frame.
/// </summary>
internal sealed class OffBallCandidateGenerator(OffBallWeights weights)
{
    private OffBallWeights W { get; } = weights;

    public OffBallWeights Weights => W;

    // Eight compass directions (team frame) without trigonometry.
    private static readonly Vec2[] Directions =
    [
        new(1, 0), new(0.7071067811865476, 0.7071067811865476), new(0, 1), new(-0.7071067811865476, 0.7071067811865476),
        new(-1, 0), new(-0.7071067811865476, -0.7071067811865476), new(0, -1), new(0.7071067811865476, -0.7071067811865476),
    ];

    /// <summary>Candidates for <paramref name="player"/>; <paramref name="marked"/> are opponents already being marked by teammates.</summary>
    public IReadOnlyList<ActionCandidate> Generate(
        MatchState state,
        PlayerMatchState player,
        bool attacking,
        bool supportAvailable,
        bool runAvailable,
        IReadOnlySet<PlayerId> marked)
    {
        var pitch = state.Pitch;
        var side = player.Team;
        var shapeTarget = player.MovementTarget;
        var local = TeamFrame.Convert(pitch, side, player.Position);
        var ballLocal = TeamFrame.Convert(pitch, side, state.Ball.Position);
        var opponents = state.TeamPlayers(side.Opponent()).ToList();
        var group = player.Role.Group();

        var candidates = new List<ActionCandidate> { new(ActionKind.MoveIntoPosition, W.MoveIntoPositionUtility, Target: shapeTarget) };

        if (attacking)
        {
            if (supportAvailable && group != PositionGroup.Defender && state.PossessionPlayerId is { } carrierId)
                AddSupport(state, player, state.Player(carrierId), opponents, candidates);

            if (runAvailable && ballLocal.X > W.RunMinBallX && player.Role is
                    PlayingPosition.ST or PlayingPosition.LW or PlayingPosition.RW or PlayingPosition.LCM or PlayingPosition.RCM)
                AddForwardRun(pitch, player, opponents, candidates);
        }
        else
        {
            var distanceToShape = player.Position.DistanceTo(shapeTarget);
            if (distanceToShape > W.RecoverThreshold)
            {
                var utility = W.MoveIntoPositionUtility + W.RecoverPerMetre * (distanceToShape - W.RecoverThreshold)
                    + (local.X > ballLocal.X ? W.RecoverAheadOfBallBonus : 0);
                candidates.Add(new ActionCandidate(ActionKind.RecoverPosition, utility, Target: shapeTarget));
            }

            if (group is PositionGroup.Defender or PositionGroup.Midfielder)
                AddMarking(state, player, shapeTarget, ballLocal, opponents, marked, candidates);
        }

        return candidates;
    }

    /// <summary>Goal-side marking point for <paramref name="attacker"/>, from the defending side's point of view.</summary>
    public Vec2 MarkPoint(MatchState state, TeamSide defendingSide, PlayerMatchState attacker)
    {
        var ownGoal = state.Pitch.OwnGoalCentre(defendingSide);
        return state.Pitch.Clamp(attacker.Position + (ownGoal - attacker.Position).Normalized() * W.MarkGoalSideDistance);
    }

    private void AddSupport(MatchState state, PlayerMatchState player, PlayerMatchState carrier, List<PlayerMatchState> opponents, List<ActionCandidate> candidates)
    {
        if (player.Position.DistanceTo(carrier.Position) > W.SupportMaxRange)
            return;

        var pitch = state.Pitch;
        var carrierLocal = TeamFrame.Convert(pitch, player.Team, carrier.Position);
        ActionCandidate? best = null;

        foreach (var direction in Directions)
        {
            var spotLocal = carrierLocal + direction * W.SupportDistance;
            var spot = TeamFrame.Convert(pitch, player.Team, spotLocal);
            if (!pitch.Contains(spot))
                continue;

            var openness = Math.Min(W.SupportOpennessCap, opponents.Min(o => o.Position.DistanceTo(spot)));
            var lane = Math.Min(W.SupportLaneCap, opponents.Min(o =>
                o.Position.DistanceTo(Vec2.ClosestPointOnSegment(carrier.Position, spot, o.Position, out _))));
            var utility = W.SupportBase
                + W.SupportOpennessWeight * openness
                + W.SupportLaneWeight * lane
                - W.SupportTravelPenalty * player.Position.DistanceTo(spot)
                + (direction.X > 0 ? W.SupportForwardBonus : 0);

            if (best is null || utility > best.Utility)
                best = new ActionCandidate(ActionKind.SupportBallCarrier, utility, Target: spot);
        }

        if (best is not null)
            candidates.Add(best);
    }

    private void AddForwardRun(PitchGeometry pitch, PlayerMatchState player, List<PlayerMatchState> opponents, List<ActionCandidate> candidates)
    {
        // The offside line: the second-last opponent (usually the last outfield defender), in our frame.
        var xs = opponents.Select(o => TeamFrame.Convert(pitch, player.Team, o.Position).X).OrderByDescending(x => x).ToList();
        var lineX = xs.Count >= 2 ? xs[1] : xs.FirstOrDefault();

        var centreY = pitch.Width / 2;
        var laneY = player.Role switch
        {
            PlayingPosition.LW => centreY + 10,
            PlayingPosition.RW => centreY - 10,
            PlayingPosition.LCM => centreY + 6,
            PlayingPosition.RCM => centreY - 6,
            _ => centreY,
        };
        var roleBonus = player.Role switch
        {
            PlayingPosition.ST => 8,
            PlayingPosition.LW or PlayingPosition.RW => 5,
            _ => 0,
        };

        var targetX = Math.Min(lineX + W.RunBeyondLine, pitch.Length - W.RunMaxDepthFromGoalLine);
        var space = Math.Min(W.RunSpaceCap, (pitch.Length - lineX) * W.RunSpaceWeight);
        var utility = W.RunBase + W.RunPerPace * player.Definition.Stats.Pace + space + roleBonus;
        var target = TeamFrame.Convert(pitch, player.Team, new Vec2(targetX, laneY));
        candidates.Add(new ActionCandidate(ActionKind.MakeForwardRun, utility, Target: target));
    }

    private void AddMarking(
        MatchState state,
        PlayerMatchState player,
        Vec2 shapeTarget,
        Vec2 ballLocal,
        List<PlayerMatchState> opponents,
        IReadOnlySet<PlayerId> marked,
        List<ActionCandidate> candidates)
    {
        var pitch = state.Pitch;
        var ownGoal = pitch.OwnGoalCentre(player.Team);
        var attacker = opponents
            .Where(o => !o.Role.IsGoalkeeper()
                        && !state.HasBall(o.PlayerId)
                        && !marked.Contains(o.PlayerId)
                        && o.Position.DistanceTo(shapeTarget) <= W.MarkZoneRadius
                        && TeamFrame.Convert(pitch, player.Team, o.Position).X < ballLocal.X + W.MarkZoneRadius)
            .MinBy(o => o.Position.DistanceTo(shapeTarget));

        if (attacker is null)
            return;

        var point = MarkPoint(state, player.Team, attacker);
        var threat = Math.Max(0, 1 - attacker.Position.DistanceTo(ownGoal) / W.MarkThreatRange);
        var utility = W.MarkBase + W.MarkThreatWeight * threat - W.MarkTravelPenalty * player.Position.DistanceTo(point);
        candidates.Add(new ActionCandidate(ActionKind.DefendGoalSide, utility, Target: point, Subject: attacker.PlayerId));
    }
}
