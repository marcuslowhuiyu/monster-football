namespace MonsterFootball.Simulation.Players;

/// <summary>Persistent player information. Does not change during a match.</summary>
public sealed record PlayerDefinition
{
    public PlayerDefinition(PlayerId id, string name, int shirtNumber, PlayingPosition position, PlayerStats stats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(shirtNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(shirtNumber, 99);
        ArgumentNullException.ThrowIfNull(stats);

        Id = id;
        Name = name;
        ShirtNumber = shirtNumber;
        Position = position;
        Stats = stats;
    }

    public PlayerId Id { get; }
    public string Name { get; }
    public int ShirtNumber { get; }
    public PlayingPosition Position { get; }
    public PlayerStats Stats { get; }
}
