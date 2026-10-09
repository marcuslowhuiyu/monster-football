using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Restarts;

/// <summary>
/// Who takes each restart (MILESTONE_2 §11.1). Deterministic: ties go to the earlier player in
/// <see cref="MatchState.Players"/> order. No player-specific set-piece roles.
/// </summary>
internal static class RestartTakers
{
    private static readonly PlayingPosition[] WidePlayers = [PlayingPosition.LB, PlayingPosition.RB, PlayingPosition.LW, PlayingPosition.RW];

    /// <summary>
    /// Kickoff: the striker. Throw-in: the nearest full-back or winger. Goal kick: the goalkeeper. Corner: the best
    /// passer outside the centre-backs and striker (who go up for it). Each falls back to any outfield player.
    /// </summary>
    public static PlayerMatchState For(MatchState state, Stoppage stoppage)
    {
        var team = state.TeamPlayers(stoppage.AwardedTeam).ToList();
        var outfield = team.Where(p => !p.Definition.Position.IsGoalkeeper()).ToList();
        return stoppage.Restart switch
        {
            RestartType.Kickoff => Nearest(Prefer(outfield, p => p.Definition.Position == PlayingPosition.ST), stoppage.Location),
            RestartType.ThrowIn => Nearest(Prefer(outfield, p => WidePlayers.Contains(p.Definition.Position)), stoppage.Location),
            RestartType.GoalKick => team.FirstOrDefault(p => p.Definition.Position.IsGoalkeeper()) ?? Nearest(outfield, stoppage.Location),
            RestartType.Corner => BestPasser(Prefer(outfield, p => p.Definition.Position is not (PlayingPosition.LCB or PlayingPosition.RCB or PlayingPosition.ST))),
            _ => throw new ArgumentOutOfRangeException(nameof(stoppage), stoppage.Restart, "No taker rule for this restart."),
        };
    }

    private static List<PlayerMatchState> Prefer(List<PlayerMatchState> candidates, Func<PlayerMatchState, bool> preferred) =>
        candidates.Where(preferred).ToList() is { Count: > 0 } chosen ? chosen : candidates;

    private static PlayerMatchState Nearest(List<PlayerMatchState> candidates, Vec2 spot) =>
        candidates.MinBy(p => p.Position.DistanceTo(spot))!;

    /// <summary>Highest Passing, then Technique.</summary>
    private static PlayerMatchState BestPasser(List<PlayerMatchState> candidates) =>
        candidates.OrderByDescending(p => p.Definition.Stats.Passing).ThenByDescending(p => p.Definition.Stats.Technique).First();
}
