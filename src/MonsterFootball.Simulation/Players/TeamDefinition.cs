namespace MonsterFootball.Simulation.Players;

/// <summary>
/// A validated starting eleven. Milestone 1 requires every 4-3-3 slot to be filled
/// exactly once (ADR-008); construction throws <see cref="InvalidTeamException"/> otherwise.
/// </summary>
public sealed class TeamDefinition
{
    public const int PlayersPerTeam = 11;

    public TeamDefinition(string name, IEnumerable<PlayerDefinition> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var list = players.ToList();
        var errors = Validate(name, list);
        if (errors.Count > 0)
            throw new InvalidTeamException(name, errors);

        Name = name;
        Players = list.AsReadOnly();
    }

    public string Name { get; }
    public IReadOnlyList<PlayerDefinition> Players { get; }
    public PlayerDefinition Goalkeeper => Players.Single(p => p.Position.IsGoalkeeper());

    /// <summary>Returns every rule the proposed team breaks; empty when valid.</summary>
    public static IReadOnlyList<string> Validate(string? name, IReadOnlyList<PlayerDefinition> players)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Team name must not be empty.");

        if (players.Count != PlayersPerTeam)
            errors.Add($"Team must have exactly {PlayersPerTeam} players but has {players.Count}.");

        var goalkeepers = players.Count(p => p.Position.IsGoalkeeper());
        if (goalkeepers != 1)
            errors.Add($"Team must have exactly one goalkeeper but has {goalkeepers}.");

        foreach (var id in Duplicates(players.Select(p => p.Id)))
            errors.Add($"Player id {id} is used more than once.");

        foreach (var shirt in Duplicates(players.Select(p => p.ShirtNumber)))
            errors.Add($"Shirt number {shirt} is used more than once.");

        foreach (var slot in Enum.GetValues<PlayingPosition>())
        {
            var count = players.Count(p => p.Position == slot);
            if (count != 1)
                errors.Add($"4-3-3 slot {slot} must be filled exactly once but has {count} players.");
        }

        return errors;
    }

    private static IEnumerable<T> Duplicates<T>(IEnumerable<T> values) =>
        values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
}

public sealed class InvalidTeamException(string? teamName, IReadOnlyList<string> errors)
    : Exception($"Invalid team '{teamName}': {string.Join(" ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
