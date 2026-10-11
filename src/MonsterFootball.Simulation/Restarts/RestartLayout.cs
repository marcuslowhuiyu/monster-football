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
            foreach (var player in state.ActivePlayers)
                player.MovementTarget = player == taker
                    ? stoppage.Location
                    : Formation433.KickoffPosition(state.Pitch, player.Team, player.Role);
        }
        else
        {
            shape.ApplyForRestart(state, stoppage.AwardedTeam, stoppage.Location);
            if (stoppage.Restart == RestartType.Corner)
                ApplyCornerSpots(state, stoppage);
            if (RestartTakers.InShootingRange(state, stoppage, tuning))
                ApplyWall(state, tuning, stoppage);
            taker.MovementTarget = stoppage.Location;
            foreach (var player in state.ActivePlayers.Where(p => p != taker && MustRespect(state, stoppage, p)))
                player.MovementTarget = Legal(state, tuning, stoppage, player.MovementTarget, ExclusionMargin);
            if (stoppage.Restart == RestartType.Penalty && Keeper(state, stoppage.AwardedTeam.Opponent()) is { } keeper)
                keeper.MovementTarget = state.Pitch.OwnGoalCentre(keeper.Team);
        }

        foreach (var player in state.ActivePlayers)
            player.MovementIntensity = MovementIntensity.Jog;
    }

    /// <summary>
    /// Whether the restart's law constrains <paramref name="player"/>: opponents of the awarded team, and at a penalty
    /// everyone except the taker's goalkeeper-opponent (who stands on the line) — the taker is handled separately.
    /// </summary>
    public static bool MustRespect(MatchState state, Stoppage stoppage, PlayerMatchState player) =>
        stoppage.Restart == RestartType.Penalty
            ? !(player.Team != stoppage.AwardedTeam && player.Role.IsGoalkeeper())
            : player.Team != stoppage.AwardedTeam;

    /// <summary>True if a player at <paramref name="position"/> respects the restart's law (see <see cref="MustRespect"/>).</summary>
    public static bool IsLegalForOpponent(MatchState state, RestartTuning tuning, Stoppage stoppage, Vec2 position) =>
        Legal(state, tuning, stoppage, position, margin: 0) == position;

    public static PlayerMatchState? Keeper(MatchState state, TeamSide side) =>
        state.Goalkeeper(side);

    /// <summary>The nearest legal spot for an opponent of the awarded team, <paramref name="margin"/> clear of the limit.</summary>
    private static Vec2 Legal(MatchState state, RestartTuning tuning, Stoppage stoppage, Vec2 position, double margin)
    {
        switch (stoppage.Restart)
        {
            case RestartType.ThrowIn:
                return OutsideRadius(state.Pitch, stoppage.Location, position, tuning.ThrowInExclusionRadius, margin);
            case RestartType.Corner:
                return OutsideRadius(state.Pitch, stoppage.Location, position, tuning.CornerExclusionRadius, margin);
            case RestartType.IndirectFreeKick or RestartType.DirectFreeKick:
                return OutsideRadius(state.Pitch, stoppage.Location, position, tuning.FreeKickExclusionRadius, margin);
            case RestartType.Penalty:
                // Outside the penalty area and the penalty arc (9.15 m from the spot).
                var defending = stoppage.AwardedTeam.Opponent();
                var outOfArea = position;
                if (state.Zones.IsIn(defending, position, TacticalAreas.OwnPenaltyArea))
                {
                    var inArea = TeamFrame.Convert(state.Pitch, defending, position);
                    outOfArea = TeamFrame.Convert(state.Pitch, defending, new Vec2(PitchGeometry.PenaltyAreaDepth + Math.Max(margin, 1e-6), inArea.Y));
                }

                return OutsideRadius(state.Pitch, stoppage.Location, outOfArea, tuning.FreeKickExclusionRadius, margin);
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

    // Who stands in a free-kick wall, in order of preference.
    private static readonly PlayingPosition[] WallSlots =
        [PlayingPosition.LCM, PlayingPosition.CM, PlayingPosition.RCM, PlayingPosition.LW, PlayingPosition.RW, PlayingPosition.ST];

    /// <summary>
    /// A wall of <see cref="RestartTuning.WallSize"/> defenders across the line from the ball to the goal centre, just
    /// beyond the free-kick distance; the shot model counts them as blockers in the shooting lane.
    /// </summary>
    private static void ApplyWall(MatchState state, RestartTuning tuning, Stoppage stoppage)
    {
        var goal = state.Pitch.OpponentGoalCentre(stoppage.AwardedTeam);
        var toGoal = (goal - stoppage.Location).Normalized();
        var across = new Vec2(-toGoal.Y, toGoal.X);
        var centre = stoppage.Location + toGoal * (tuning.FreeKickExclusionRadius + ExclusionMargin);
        var wall = WallSlots
            .Select(slot => state.TeamPlayers(stoppage.AwardedTeam.Opponent()).FirstOrDefault(p => p.Role == slot))
            .Where(p => p is not null)
            .Take(tuning.WallSize)
            .ToList();
        for (var i = 0; i < wall.Count; i++)
            wall[i]!.MovementTarget = state.Pitch.Clamp(centre + across * (0.7 * (i - (wall.Count - 1) / 2.0)));
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
            var player = state.TeamPlayers(side).FirstOrDefault(p => p.Role == slot);
            if (player is null)
                continue;
            var x = attacking ? pitch.Length - depth : depth;
            var local = new Vec2(x, pitch.Width / 2 + towardCorner * lateral);
            player.MovementTarget = TeamFrame.Convert(pitch, side, local);
        }
    }
}
