using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Tests.Players;

public class PlayerStatsTests
{
    [Theory]
    [InlineData(PlayerStats.Min)]
    [InlineData(PlayerStats.Max)]
    public void Accepts_values_within_scale(int value)
    {
        var stats = PlayerStats.Uniform(value);
        Assert.Equal(value, stats.Keeping);
        Assert.Equal(value, stats.Technique);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-3)]
    public void Rejects_values_outside_scale(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerStats.Uniform(value));
    }

    [Fact]
    public void Reports_which_stat_is_invalid()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlayerStats(5, 5, 5, 5, pace: 12, 5, 5, 5, 5));
        Assert.Equal("pace", ex.ParamName);
    }

    [Fact]
    public void Exposes_exactly_the_approved_attributes()
    {
        // ADR-002: changing this list requires an accepted design decision.
        string[] approved =
            ["Technique", "Passing", "Finishing", "Defending", "Pace", "Power", "Stamina", "Awareness", "Keeping"];

        var actual = typeof(PlayerStats).GetProperties()
            .Where(p => p.PropertyType == typeof(int))
            .Select(p => p.Name);

        Assert.Equal(approved.Order(), actual.Order());
    }
}
