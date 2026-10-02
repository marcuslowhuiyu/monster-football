using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Tests.Random;

public class MatchRngTests
{
    [Fact]
    public void Same_seed_produces_same_sequence()
    {
        var a = new MatchRng(123456);
        var b = new MatchRng(123456);

        for (var i = 0; i < 1000; i++)
            Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    [Fact]
    public void Different_seeds_produce_different_sequences()
    {
        var a = new MatchRng(1);
        var b = new MatchRng(2);

        var sameCount = Enumerable.Range(0, 100).Count(_ => a.NextDouble() == b.NextDouble());
        Assert.True(sameCount < 5);
    }

    [Fact]
    public void Sequence_for_a_seed_is_pinned()
    {
        // Regression guard (ADR-009): if this fails, every recorded seed now plays a different match.
        var rng = new MatchRng(123456);
        int[] actual = [.. Enumerable.Range(0, 5).Select(_ => rng.NextInt(0, 1_000_000))];
        Assert.Equal(PinnedValues, actual);
    }

    private static readonly int[] PinnedValues = [514180, 956733, 773270, 508675, 125752];

    [Fact]
    public void NextDouble_is_in_unit_interval()
    {
        var rng = new MatchRng(7);
        for (var i = 0; i < 10_000; i++)
        {
            var value = rng.NextDouble();
            Assert.InRange(value, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void NextInt_respects_bounds_and_covers_range()
    {
        var rng = new MatchRng(99);
        var seen = new HashSet<int>();
        for (var i = 0; i < 10_000; i++)
        {
            var value = rng.NextInt(-3, 4);
            Assert.InRange(value, -3, 3);
            seen.Add(value);
        }
        Assert.Equal(7, seen.Count);
    }

    [Fact]
    public void NextInt_rejects_empty_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRng(1).NextInt(5, 5));
    }

    [Fact]
    public void Roll_extremes_are_certain()
    {
        var rng = new MatchRng(5);
        for (var i = 0; i < 1000; i++)
        {
            Assert.False(rng.Roll(0));
            Assert.True(rng.Roll(1));
        }
    }

    [Fact]
    public void Roll_frequency_matches_probability()
    {
        var rng = new MatchRng(42);
        var hits = Enumerable.Range(0, 100_000).Count(_ => rng.Roll(0.3));
        Assert.InRange(hits / 100_000.0, 0.29, 0.31);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Roll_rejects_invalid_probability(double probability)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRng(1).Roll(probability));
    }
}
