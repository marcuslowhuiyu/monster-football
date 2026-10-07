using MonsterFootball.Playback;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Observation;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Playback;

public class FrameInterpolatorTests
{
    private static (MatchFrame From, MatchFrame To) TwoFrames()
    {
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), 4);
        for (var i = 0; i < 30; i++)
            simulation.Step();
        var from = MatchFrame.Capture(simulation.State);
        simulation.Step();
        return (from, MatchFrame.Capture(simulation.State));
    }

    [Fact]
    public void Endpoints_are_the_frames_themselves()
    {
        var (from, to) = TwoFrames();

        var start = FrameInterpolator.Interpolate(from, to, 0);
        var end = FrameInterpolator.Interpolate(from, to, 1);

        Assert.Equal(from.Players.Select(p => p.Position), start.PlayerPositions);
        Assert.Equal(to.Players.Select(p => p.Position), end.PlayerPositions);
        Assert.Equal(from.Ball.Position, start.BallPosition);
        Assert.Equal(to.Ball.Position, end.BallPosition);
    }

    [Fact]
    public void Halfway_is_the_midpoint_and_discrete_state_comes_from_the_earlier_frame()
    {
        var (from, to) = TwoFrames();

        var view = FrameInterpolator.Interpolate(from, to, 0.5);

        for (var i = 0; i < from.Players.Length; i++)
        {
            var expected = (from.Players[i].Position + to.Players[i].Position) * 0.5;
            Assert.Equal(expected.X, view.PlayerPositions[i].X, precision: 9);
            Assert.Equal(expected.Y, view.PlayerPositions[i].Y, precision: 9);
        }

        Assert.Same(from, view.Frame);
        Assert.Equal(from.Tick + 0.5, view.Tick);
    }

    [Fact]
    public void Fractions_outside_zero_to_one_are_clamped()
    {
        var (from, to) = TwoFrames();
        Assert.Equal(to.Players.Select(p => p.Position), FrameInterpolator.Interpolate(from, to, 7).PlayerPositions);
    }
}
