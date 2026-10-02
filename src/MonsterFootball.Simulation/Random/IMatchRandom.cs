namespace MonsterFootball.Simulation.Random;

/// <summary>
/// The only source of randomness in the simulation (ADR-005).
/// One instance is owned by each match; systems receive it, they never create their own.
/// </summary>
public interface IMatchRandom
{
    /// <summary>Uniform double in [0, 1).</summary>
    double NextDouble();

    /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>True with the given probability (0–1). Always consumes exactly one draw.</summary>
    bool Roll(double probability);
}
