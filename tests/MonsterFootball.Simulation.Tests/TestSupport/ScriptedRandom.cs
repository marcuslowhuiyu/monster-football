using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Tests.TestSupport;

/// <summary>Returns pre-scripted draws so tests can force specific outcomes.</summary>
internal sealed class ScriptedRandom(params double[] draws) : IMatchRandom
{
    private readonly Queue<double> _draws = new(draws);

    public double NextDouble() =>
        _draws.Count > 0 ? _draws.Dequeue() : throw new InvalidOperationException("ScriptedRandom ran out of draws.");

    public int NextInt(int minInclusive, int maxExclusive) =>
        minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));

    public bool Roll(double probability) => NextDouble() < probability;
}
