using MonsterFootball.Playback;
using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Tests.Playback;

public class PlaybackSessionTests
{
    private static PlaybackSession NewSession(int seed = 5, MatchConfig? config = null) =>
        new(config ?? MatchConfig.Default, SampleTeams.Create("Blue", 1), SampleTeams.Create("Red", 101), seed);

    [Fact]
    public void Rendering_only_simulates_as_far_as_the_frame_it_shows()
    {
        var session = NewSession();
        session.Clock.Play();

        for (var i = 0; i < 60; i++)
        {
            var view = session.Update(1.0 / 60);   // one second of real time at 60 fps
            Assert.True(session.Match.SimulatedTick <= Math.Floor(session.Clock.Position) + 1,
                "the simulation ran ahead of what is being shown");
            Assert.Equal(Math.Floor(session.Clock.Position), view.Frame.Tick);
        }

        Assert.Equal(4, session.Clock.Position, precision: 6); // 1 s at 1x = 4 ticks
    }

    [Fact]
    public void Paused_rendering_neither_moves_nor_simulates()
    {
        var session = NewSession();
        session.Update(0.5);
        var tick = session.Match.SimulatedTick;

        for (var i = 0; i < 30; i++)
            session.Update(1.0 / 30);

        Assert.Equal(tick, session.Match.SimulatedTick);
        Assert.Equal(0, session.Clock.Position);
    }

    [Fact]
    public void Seeking_backwards_reuses_recorded_frames()
    {
        var session = NewSession();
        session.Clock.Seek(400);
        var late = session.Update(0);
        var simulated = session.Match.SimulatedTick;

        session.Clock.Seek(100);
        var early = session.Update(0);

        Assert.Equal(simulated, session.Match.SimulatedTick);
        Assert.Equal(100, early.Frame.Tick);
        Assert.Same(session.Match.Frames[100], early.Frame);
        Assert.Equal(400, late.Frame.Tick);
    }

    [Fact]
    public void Playback_stops_at_the_end_of_the_match()
    {
        var session = NewSession(config: MatchConfig.Default with { PeriodSeconds = 10 });
        session.Clock.SetSpeed(4);
        session.Clock.Play();

        for (var i = 0; i < 2000 && session.Clock.IsPlaying; i++)
            session.Update(0.5);

        Assert.True(session.Match.IsComplete);
        Assert.False(session.Clock.IsPlaying);
        Assert.Equal(session.Match.LastTick, session.Clock.Position);
        Assert.Equal(MatchPhase.Finished, session.Update(1).Frame.Phase);
    }

    [Fact]
    public void Restarting_replays_the_same_seed_identically_and_a_new_seed_differently()
    {
        var session = NewSession(seed: 9);
        session.Clock.Seek(2000);
        var first = session.Update(0).Frame;

        session.Restart();
        session.Clock.Seek(2000);
        var replay = session.Update(0).Frame;
        Assert.Equal(first.Players.Select(p => p.Position), replay.Players.Select(p => p.Position));

        session.Restart(seed: 10);
        Assert.Equal(10, session.Match.Seed);
        session.Clock.Seek(2000);
        Assert.NotEqual(first.Players.Select(p => p.Position), session.Update(0).Frame.Players.Select(p => p.Position));
    }
}
