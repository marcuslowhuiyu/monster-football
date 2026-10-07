using MonsterFootball.Playback;

namespace MonsterFootball.Simulation.Tests.Playback;

public class PlaybackClockTests
{
    [Fact]
    public void A_new_clock_is_paused_at_the_start_at_normal_speed()
    {
        var clock = new PlaybackClock();
        Assert.False(clock.IsPlaying);
        Assert.Equal(0, clock.Position);
        Assert.Equal(1, clock.Speed);
    }

    [Fact]
    public void Paused_time_does_not_move_the_match()
    {
        var clock = new PlaybackClock();
        clock.Advance(realSeconds: 5, tickSeconds: 0.25);
        Assert.Equal(0, clock.Position);
    }

    [Theory]
    [InlineData(0.25, 1)]
    [InlineData(1, 4)]
    [InlineData(4, 16)]
    public void Playing_converts_real_time_into_ticks_at_the_chosen_speed(double speed, double expectedTicks)
    {
        var clock = new PlaybackClock();
        clock.SetSpeed(speed);
        clock.Play();

        clock.Advance(realSeconds: 1, tickSeconds: 0.25);

        Assert.Equal(expectedTicks, clock.Position, precision: 9);
    }

    [Fact]
    public void Changing_speed_keeps_the_current_position()
    {
        var clock = new PlaybackClock();
        clock.Play();
        clock.Advance(1, 0.25);
        clock.SetSpeed(2);
        Assert.Equal(4, clock.Position, precision: 9);
        clock.Advance(1, 0.25);
        Assert.Equal(12, clock.Position, precision: 9);
    }

    [Fact]
    public void Toggle_seek_and_invalid_speeds()
    {
        var clock = new PlaybackClock();
        clock.Toggle();
        Assert.True(clock.IsPlaying);
        clock.Toggle();
        Assert.False(clock.IsPlaying);

        clock.Seek(120.5);
        Assert.Equal(120.5, clock.Position);
        clock.Seek(-3);
        Assert.Equal(0, clock.Position);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed(0));
        Assert.Equal([0.25, 0.5, 1, 2, 4], PlaybackClock.Speeds);
    }
}
