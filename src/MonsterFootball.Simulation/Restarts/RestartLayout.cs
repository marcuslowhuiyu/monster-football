using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Restarts;

/// <summary>
/// Where players stand for a restart (MILESTONE_2 §11.1). Kickoffs use the 4-3-3 kickoff layout. Throw-ins and
/// goal kicks and free kicks use both team shapes built around the restart spot; corners send set slots into the box. Then the taker
/// stands on the ball and opponents are moved out of any area the law keeps them from.
/// </summary>
internal static class RestartLayout
{
    /// <summary>Opponents stand this much beyond an exclusion distance, so walking there satisfies it.</summary>
    public const double ExclusionMargin = 0.5;

    // Corner spots as (metres from the goal line, metres from the centre toward the corner's side).
    private static readonly (PlayingPosition Slot, double Depth, double Lateral)[] CornerAttack =
    [
        (PlayingPosition.ST, 7, 0),
        (PlayingPosition.LCB, 9, 3),
        (PlayingPosition.RCB, 10, -4),
        (PlayingPosition.LW, 6, -6),
        (PlayingPosition.RW, 12, 7),
        (PlayingPosition.CM, 18, 0),
    ];

    private static readonly (PlayingPosition Slot, double Depth, double Lateral)[] CornerDefence =
    [
        (PlayingPosition.LCB, 6, 2),
        (PlayingPosition.RCB, 6, -3),
        (PlayingPosition.LB, 1, 3.6),
        (PlayingPosition.RB, 1, -3.6),
        (PlayingPosition.CM, 11, 0),
        (PlayingPosition.LCM, 9, 7),
        (PlayingPosition.RCM, 9, -7),
        (PlayingPosition.LW, 16, 12),
        (PlayingPosition.RW, 16, -10),
        (PlayingPosition.ST, 25, 0),
    ];

    /// <summary>Sets every player's restart target (and a jogging intensity) for <paramref name="stoppage"/>.</summary>
    public static void Assign(MatchState state, TeamShapeSystem shape, RestartTuning tuning, Stoppage stoppage, PlayerMatchState taker)
    {
        if (stoppage.Restart == RestartType.Kickoff)
        {
            foreach (var player in state.Players)
                player.MovementTarget = player == taker
                    ? stoppage.Location
                    : Formation433.KickoffPosition(state.Pitch, player.Team, player.Definition.Position);
        }
        else
        {
            shape.ApplyForRestart(state, stoppage.AwardedTeam, stoppage.Location);
            if (stoppage.Restart == RestartType.Corner)
                ApplyCornerSpots(state, stoppage);
            taker.MovementTarget = stoppage.Location;
            foreach (var opponent in state.TeamPlayers(stoppage.AwardedTeam.Opponent()))
                opponent.MovementTarget = Legal(state, tuning, stoppage, opponent.MovementTarget, ExclusionMargin);
        }

        foreach (var player in state.Players)
            player.MovementIntensity = MovementIntensity.Jog;
    }

    /// <summary>True if an opponent of the awarded team at <paramref name="position"/> respects the restart's law.</summary>
    public static bool IsLegalForOpponent(MatchState state, RestartTuning tuning, Stoppage stoppage, Vec2 position) =>
        Legal(state, tuning, stoppage, position, margin: 0) == position;

    /// <summary>The nearest legal spot for an opponent of the awarded team, <paramref name="margin"/> clear of the limit.</summary>
    private static Vec2 Legal(MatchState state, RestartTuning tuning, Stoppage stoppage, Vec2 position, double margin)
    {
        switch (stoppage.Restart)
        {
            case RestartType.ThrowIn:
                return OutsideRadius(state.Pitch, stoppage.Location, position, tuning.ThrowInExclusionRadius, margin);
            case RestartType.Corner:
                return OutsideRadius(state.Pitch, stoppage.Location, position, tuning.CornerExclusionRadius, margin);
            case RestartType.IndirectFreeKick:
                return OutsideRadius(state.Pitch, stoppage.Location, position, tuning.FreeKickExclusionRadius, margin);
            case RestartType.GoalKick:
                if (!state.Zones.IsIn(stoppage.AwardedTeam, position, TacticalAreas.OwnPenaltyArea))
                    return position;
                var local = TeamFrame.Convert(state.Pitch, stoppage.AwardedTeam, position);
                var outside = new Vec2(PitchGeometry.PenaltyAreaDepth + Math.Max(margin, 1e-6), local.Y);
                return TeamFrame.Convert(state.Pitch, stoppage.AwardedTeam, outside);
            default:
                return position;
        }
    }

    private static Vec2 OutsideRadius(PitchGeometry pitch, Vec2 ball, Vec2 position, double radius, double margin)
    {
        var offset = position - ball;
        if (offset.Length >= radius)
            return position;
        var direction = offset.LengthSquared > 1e-12 ? offset.Normalized() : (pitch.Centre - ball).Normalized();
        return pitch.Clamp(ball + direction * (radius + margin));
    }

    private static void ApplyCornerSpots(MatchState state, Stoppage stoppage)
    {
        Place(state, stoppage.AwardedTeam, stoppage.Location, CornerAttack, attacking: true);
        Place(state, stoppage.AwardedTeam.Opponent(), stoppage.Location, CornerDefence, attacking: false);
    }

    private static void Place(MatchState state, TeamSide side, Vec2 corner, (PlayingPosition Slot, double Depth, double Lateral)[] spots, bool attacking)
    {
        var pitch = state.Pitch;
        var cornerLocal = TeamFrame.Convert(pitch, side, corner);
        var towardCorner = cornerLocal.Y > pitch.Width / 2 ? 1 : -1;
        foreach (var (slot, depth, lateral) in spots)
        {
            var player = state.TeamPlayers(side).FirstOrDefault(p => p.Definition.Position == slot);
            if (player is null)
                continue;
            var x = attacking ? pitch.Length - depth : depth;
            var local = new Vec2(x, pitch.Width / 2 + towardCorner * lateral);
            player.MovementTarget = TeamFrame.Convert(pitch, side, local);
        }
    }
}
