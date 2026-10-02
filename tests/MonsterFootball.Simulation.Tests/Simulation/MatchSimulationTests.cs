using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class MatchSimulationTests
{
    private static MatchSimulation NewSimulation(int seed = 123456, MatchConfig? config = null) =>
        new(config ?? MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed);

    [Fact]
    public void Full_match_runs_to_ninety_minutes_and_finishes()
    {
        var result = CheckedRun.ToCompletion(NewSimulation());

        Assert.Equal(MatchPhase.Finished, result.FinalState.Phase);
        Assert.Equal(MatchConfig.Default.TotalTicks, result.FinalState.Clock.Tick);
        Assert.Equal("90:00", result.FinalState.Clock.Now.ToString());
    }

    [Fact]
    public void Match_starts_with_MatchStarted_then_Kickoff()
    {
        var simulation = NewSimulation();
        simulation.Step();

        Assert.Equal([MatchEventType.MatchStarted, MatchEventType.Kickoff], simulation.Events.Select(e => e.Type));
        Assert.Equal(MatchPhase.OpenPlay, simulation.State.Phase);
    }

    [Fact]
    public void Kickoff_gives_ball_to_kicking_striker_at_centre_spot()
    {
        var simulation = NewSimulation();
        simulation.Step();

        var kickoff = simulation.Events.Single(e => e.Type == MatchEventType.Kickoff);
        var taker = simulation.State.Player(kickoff.ActorPlayerId!.Value);

        Assert.Equal(PlayingPosition.ST, taker.Definition.Position);
        Assert.Equal(kickoff.Team, taker.Team);
        Assert.Equal(taker.Team, simulation.State.PossessionTeam);
        Assert.Equal(simulation.State.Pitch.Centre, simulation.State.Ball.Position);
    }

    [Theory]
    [InlineData(0.1, TeamSide.Home)]
    [InlineData(0.9, TeamSide.Away)]
    public void Coin_toss_uses_match_random_to_pick_kickoff_team(double draw, TeamSide expected)
    {
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101),
            new ScriptedRandom(draw), seed: 0);

        simulation.Step();

        Assert.Equal(expected, simulation.Events.Single(e => e.Type == MatchEventType.Kickoff).Team);
    }

    [Fact]
    public void MatchFinished_is_emitted_exactly_once_at_full_time()
    {
        var result = NewSimulation().RunToCompletion();

        var finished = Assert.Single(result.Events, e => e.Type == MatchEventType.MatchFinished);
        Assert.Same(finished, result.Events[^1]);
        Assert.Equal("90:00", finished.Timestamp.ToString());
    }

    [Fact]
    public void Simulation_cannot_continue_after_finish()
    {
        var simulation = NewSimulation();
        simulation.RunToCompletion();
        var eventCount = simulation.Events.Count;
        var tick = simulation.State.Clock.Tick;

        Assert.Throws<InvalidOperationException>(simulation.Step);
        Assert.Equal(eventCount, simulation.Events.Count);
        Assert.Equal(tick, simulation.State.Clock.Tick);
    }

    [Fact]
    public void RunToCompletion_after_finish_returns_same_result_without_new_events()
    {
        var simulation = NewSimulation();
        var first = simulation.RunToCompletion();
        var second = simulation.RunToCompletion();

        Assert.Equal(first.Events.Count, second.Events.Count);
        Assert.Equal(first.HomeGoals, second.HomeGoals);
    }

    [Fact]
    public void Result_carries_seed()
    {
        Assert.Equal(777, NewSimulation(777).RunToCompletion().Seed);
    }

    [Fact]
    public void Event_timestamps_never_decrease()
    {
        var events = NewSimulation().RunToCompletion().Events;
        for (var i = 1; i < events.Count; i++)
            Assert.True(events[i].Timestamp.CompareTo(events[i - 1].Timestamp) >= 0, $"{events[i - 1]} then {events[i]}");
    }

    [Fact]
    public void Short_configured_match_finishes_on_time()
    {
        var config = MatchConfig.Default with { MatchDurationSeconds = 10 };
        var result = NewSimulation(config: config).RunToCompletion();
        Assert.Equal(40, result.FinalState.Clock.Tick);
    }
}
