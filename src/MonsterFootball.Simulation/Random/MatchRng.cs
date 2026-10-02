using System.Numerics;

namespace MonsterFootball.Simulation.Random;

/// <summary>
/// Seeded xoshiro256** generator, state initialised with SplitMix64 (ADR-009).
/// The algorithm is implemented here rather than borrowed from <c>System.Random</c>
/// so the sequence for a seed can never change with a runtime upgrade.
/// </summary>
public sealed class MatchRng : IMatchRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public MatchRng(int seed)
    {
        Seed = seed;
        var x = unchecked((ulong)(long)seed);
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    public int Seed { get; }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive,
                $"maxExclusive must be greater than minInclusive ({minInclusive}).");

        var range = (ulong)((long)maxExclusive - minInclusive);
        // Reject the low values that would bias the modulo (threshold = 2^64 mod range).
        var threshold = unchecked(0UL - range) % range;
        ulong value;
        do
        {
            value = NextUInt64();
        } while (value < threshold);

        return (int)(minInclusive + (long)(value % range));
    }

    public bool Roll(double probability)
    {
        if (double.IsNaN(probability) || probability < 0 || probability > 1)
            throw new ArgumentOutOfRangeException(nameof(probability), probability, "Probability must be between 0 and 1.");
        return NextDouble() < probability;
    }

    private ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
