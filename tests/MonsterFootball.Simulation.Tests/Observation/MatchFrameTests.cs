using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Observation;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Observation;

public class MatchFrameTests
{
    private static MatchSimulation NewSimulation(int seed) =>
        new(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed);

    [Fact]
    public void A_frame_copies_what_can_be_observed_at_the_end_of_a_tick()
    {
        var simulation = NewSimulation(1);
        for (var i = 0; i < 200; i++)
            simulation.Step();
        var state = simulation.State;

        var frame = MatchFrame.Capture(state);

        Assert.Equal(state.Clock.Tick, frame.Tick);
        Assert.Equal(state.Clock.Now, frame.Time);
        Assert.Equal(state.Phase, frame.Phase);
        Assert.Equal(state.CurrentStoppage, frame.Stoppage);
        Assert.Equal((state.Score.Home, state.Score.Away), (frame.HomeGoals, frame.AwayGoals));
        Assert.Equal(state.Ball.Position, frame.Ball.Position);
        Assert.Equal(state.Ball.PossessorId, frame.Ball.PossessorId);
        Assert.Equal(state.AttackingTeam, frame.AttackingTeam);
        Assert.Equal(state.Players.Select(p => p.PlayerId), frame.Players.Select(p => p.Id));
        foreach (var (player, copy) in state.Players.Zip(frame.Players))
        {
            Assert.Equal(player.Team, copy.Team);
            Assert.Equal(player.Definition.Position, copy.Slot);
            Assert.Equal(player.Definition.ShirtNumber, copy.ShirtNumber);
            Assert.Equal(player.Position, copy.Position);
            Assert.Equal(player.MovementTarget, copy.MovementTarget);
            Assert.Equal(player.CurrentStamina, copy.Stamina);
            Assert.Equal(player.OffBallPlan?.Kind, copy.OffBallPlan);
        }
    }

    [Fact]
    public void A_frame_does_not_change_when_the_match_moves_on()
    {
        var simulation = NewSimulation(2);
        for (var i = 0; i < 50; i++)
            simulation.Step();
        var frame = MatchFrame.Capture(simulation.State);
        var positions = frame.Players.Select(p => p.Position).ToList();

        for (var i = 0; i < 50; i++)
            simulation.Step();

        Assert.Equal(positions, frame.Players.Select(p => p.Position));
        Assert.Equal(50, frame.Tick);
    }

    [Fact]
    public void A_ball_in_flight_is_described_with_its_path()
    {
        var simulation = NewSimulation(3);
        while (!simulation.State.Ball.IsInFlight)
            simulation.Step();

        var frame = MatchFrame.Capture(simulation.State);
        var flight = simulation.State.Ball.Flight!;

        Assert.NotNull(frame.Ball.Flight);
        Assert.Equal(flight.StartPosition, frame.Ball.Flight.Start);
        Assert.Equal(flight.TargetPosition, frame.Ball.Flight.Target);
        Assert.Equal(flight.ArrivalTick, frame.Ball.Flight.ArrivalTick);
        Assert.Equal(flight.Team, frame.Ball.Flight.Team);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Capturing_a_frame_every_tick_does_not_change_the_match(int seed)
    {
        var plain = NewSimulation(seed).RunToCompletion();

        var observed = NewSimulation(seed);
        var frames = 0;
        while (!observed.IsFinished)
        {
            observed.Step();
            _ = MatchFrame.Capture(observed.State);
            frames++;
        }

        Assert.True(frames > 20_000);
        Assert.Equal(MatchFingerprint.Create(plain), MatchFingerprint.Create(observed.RunToCompletion()));
    }
}
