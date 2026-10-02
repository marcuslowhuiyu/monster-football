namespace MonsterFootball.Simulation.Players;

/// <summary>
/// The approved core attribute model (ADR-002). Every stat is an integer from 1 to 10.
/// Do not add attributes without an accepted design decision.
/// </summary>
public sealed record PlayerStats
{
    public const int Min = 1;
    public const int Max = 10;

    public PlayerStats(
        int technique,
        int passing,
        int finishing,
        int defending,
        int pace,
        int power,
        int stamina,
        int awareness,
        int keeping)
    {
        Technique = Validate(technique, nameof(technique));
        Passing = Validate(passing, nameof(passing));
        Finishing = Validate(finishing, nameof(finishing));
        Defending = Validate(defending, nameof(defending));
        Pace = Validate(pace, nameof(pace));
        Power = Validate(power, nameof(power));
        Stamina = Validate(stamina, nameof(stamina));
        Awareness = Validate(awareness, nameof(awareness));
        Keeping = Validate(keeping, nameof(keeping));
    }

    public int Technique { get; }
    public int Passing { get; }
    public int Finishing { get; }
    public int Defending { get; }
    public int Pace { get; }
    public int Power { get; }
    public int Stamina { get; }
    public int Awareness { get; }
    public int Keeping { get; }

    /// <summary>All nine stats set to the same value.</summary>
    public static PlayerStats Uniform(int value) =>
        new(value, value, value, value, value, value, value, value, value);

    private static int Validate(int value, string name) =>
        value is >= Min and <= Max
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"Player stats must be between {Min} and {Max}.");
}
