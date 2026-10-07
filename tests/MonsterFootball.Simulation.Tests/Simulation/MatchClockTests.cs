using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class MatchClockTests
{
    [Fact]
    public void Default_config_is_four_ticks_per_second_and_two_halves_of_45_minutes()
    {
        var config = MatchConfig.Default;
        Assert.Equal(0.25, config.TickSeconds);
        Assert.Equal(2, config.Periods);
        Assert.Equal(45 * 60, config.PeriodSeconds);
        Assert.Equal(45 * 60 * 4, config.PeriodTicks);
    }

    [Fact]
    public void Tick_length_is_configurable()
    {
        var config = MatchConfig.Default with { TickSeconds = 1.0 };
        Assert.Equal(2700, config.PeriodTicks);
    }

    [Fact]
    public void Partial_final_tick_rounds_up()
    {
        var config = MatchConfig.Default with { TickSeconds = 0.4, PeriodSeconds = 1.0 };
        Assert.Equal(3, config.PeriodTicks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Rejects_non_positive_period(double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchConfig.Default with { PeriodSeconds = seconds });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.25)]
    public void Rejects_non_positive_tick(double tick)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchConfig.Default with { TickSeconds = tick });
    }

    [Fact]
    public void Each_step_advances_exactly_one_tick()
    {
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed: 1);

        for (var i = 1; i <= 10; i++)
        {
            simulation.Step();
            Assert.Equal(i, simulation.State.Clock.Tick);
            Assert.Equal(i * 0.25, simulation.State.Clock.Now.Seconds);
        }
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(271.75, "04:31")]
    [InlineData(5400, "90:00")]
    public void MatchTime_formats_as_minutes_and_seconds(double seconds, string expected)
    {
        Assert.Equal(expected, new MatchTime(seconds).ToString());
    }

    [Theory]
    [InlineData(1, 2790, 2790, "45+01:30")]  // first-half added time
    [InlineData(2, 2820, 0, "45:00")]        // second half starts at 45:00 whatever the first half's added time
    [InlineData(2, 5520, 2700, "90:00")]
    [InlineData(2, 5580, 2760, "90+01:00")]
    public void MatchTime_shows_the_referees_clock_with_added_time(int period, double elapsed, double intoPeriod, string expected)
    {
        var time = new MatchTime(elapsed) { Period = period, PeriodSeconds = intoPeriod, RegularPeriodSeconds = 2700 };
        Assert.Equal(expected, time.ToString());
    }
}
