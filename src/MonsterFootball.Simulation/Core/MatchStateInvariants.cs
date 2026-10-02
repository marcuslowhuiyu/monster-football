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

        if (state.Players.Count != MatchState.ActivePlayerCount)
            violations.Add($"Expected {MatchState.ActivePlayerCount} active players but found {state.Players.Count}.");

        foreach (var side in Enum.GetValues<TeamSide>())
        {
            var count = state.TeamPlayers(side).Count();
            if (count != TeamDefinition.PlayersPerTeam)
                violations.Add($"{side} has {count} active players; expected {TeamDefinition.PlayersPerTeam}.");
        }

        if (state.Players.Select(p => p.PlayerId).Distinct().Count() != state.Players.Count)
            violations.Add("Active player ids are not unique.");

        if (state.Ball.PossessorId is { } possessor)
        {
            var carrier = state.Players.FirstOrDefault(p => p.PlayerId == possessor);
            if (carrier is null)
                violations.Add($"Ball possessor {possessor} is not an active player.");
            else if (carrier.Position != state.Ball.Position)
                violations.Add($"Possessed ball at {state.Ball.Position} is not with its carrier: {carrier}.");
        }

        if (!state.Pitch.Contains(state.Ball.Position))
            violations.Add($"Ball outside pitch at {state.Ball.Position}.");

        if (state.Ball.IsInFlight && state.Ball.PossessorId is not null)
            violations.Add($"Ball in flight but possessed by {state.Ball.PossessorId}.");

        if (state.Score.Home < 0 || state.Score.Away < 0)
            violations.Add($"Score is negative: {state.Score}.");

        foreach (var player in state.Players)
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
