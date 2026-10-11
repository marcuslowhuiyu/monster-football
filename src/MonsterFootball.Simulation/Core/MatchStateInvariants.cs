namespace MonsterFootball.Simulation.Core;

using MonsterFootball.Simulation.Players;

/// <summary>
/// Reusable validity checks for a <see cref="MatchState"/>. Intended to run after every tick in tests.
/// Each violation message names the offending player or value.
/// </summary>
public static class MatchStateInvariants
{
    public static IReadOnlyList<string> FindViolations(MatchState state)
    {
        var violations = new List<string>();

        if (state.Players.Count != MatchState.SquadPlayerCount)
            violations.Add($"Expected {MatchState.SquadPlayerCount} players but found {state.Players.Count}.");

        foreach (var side in Enum.GetValues<TeamSide>())
        {
            var count = state.TeamPlayers(side).Count();
            if (state.Phase != MatchPhase.Finished && count is < MatchState.MinimumPlayers or > TeamDefinition.PlayersPerTeam)
                violations.Add($"{side} has {count} active players; expected {MatchState.MinimumPlayers}–{TeamDefinition.PlayersPerTeam}.");
            var keepers = state.TeamPlayers(side).Count(p => p.Role.IsGoalkeeper());
            if (count > 0 && keepers != 1)
                violations.Add($"{side} has {keepers} active goalkeepers; expected exactly one.");
            var roles = state.TeamPlayers(side).Select(p => p.Role).ToList();
            if (roles.Distinct().Count() != roles.Count)
                violations.Add($"{side} has two players in the same role: {string.Join(", ", roles)}.");
        }

        foreach (var sentOff in state.Players.Where(p => !p.IsActive))
        {
            if (state.Pitch.Contains(sentOff.Position))
                violations.Add($"Sent-off player still on the pitch: {sentOff}.");
            if (state.HasBall(sentOff.PlayerId))
                violations.Add($"Sent-off player has the ball: {sentOff}.");
        }

        if (state.Players.Select(p => p.PlayerId).Distinct().Count() != state.Players.Count)
            violations.Add("Active player ids are not unique.");

        if (state.Ball.PossessorId is { } possessor)
        {
            var carrier = state.ActivePlayers.FirstOrDefault(p => p.PlayerId == possessor);
            if (carrier is null)
                violations.Add($"Ball possessor {possessor} is not an active player.");
            else if (carrier.Position != state.Ball.Position)
                violations.Add($"Possessed ball at {state.Ball.Position} is not with its carrier: {carrier}.");
        }

        if (!state.Pitch.Contains(state.Ball.Position))
            violations.Add($"Ball outside pitch at {state.Ball.Position}.");

        if (state.Ball.IsInFlight && state.Ball.PossessorId is not null)
            violations.Add($"Ball in flight but possessed by {state.Ball.PossessorId}.");

        var stopped = state.Phase is MatchPhase.Stopped or MatchPhase.RestartSetup;
        if (stopped && state.CurrentStoppage is null)
            violations.Add($"Phase {state.Phase} without a stoppage record.");
        if (state.Phase == MatchPhase.OpenPlay && state.CurrentStoppage is not null)
            violations.Add($"Open play with a stoppage still recorded: {state.CurrentStoppage}.");
        if (stopped && state.Ball.PossessorId is { } holder)
            violations.Add($"Play is stopped but {holder} holds the ball.");

        if (state.Score.Home < 0 || state.Score.Away < 0)
            violations.Add($"Score is negative: {state.Score}.");

        foreach (var player in state.ActivePlayers)
        {
            if (!state.Pitch.Contains(player.Position))
                violations.Add($"Player outside pitch: {player}.");

            if (player.ActiveCarry is not null && !state.HasBall(player.PlayerId))
                violations.Add($"Player has an active carry without the ball: {player}.");

            if (player.CurrentStamina is < PlayerMatchState.MinStamina or > PlayerMatchState.MaxStamina)
                violations.Add($"Stamina {player.CurrentStamina} out of range [{PlayerMatchState.MinStamina}, {PlayerMatchState.MaxStamina}]: {player}.");
        }

        return violations;
    }

    /// <summary>Throws <see cref="InvariantViolationException"/> listing every violation.</summary>
    public static void Verify(MatchState state)
    {
        var violations = FindViolations(state);
        if (violations.Count > 0)
            throw new InvariantViolationException(violations);
    }
}

public sealed class InvariantViolationException(IReadOnlyList<string> violations)
    : Exception("Match state invariants violated:" + Environment.NewLine + string.Join(Environment.NewLine, violations.Select(v => " - " + v)))
{
    public IReadOnlyList<string> Violations { get; } = violations;
}
