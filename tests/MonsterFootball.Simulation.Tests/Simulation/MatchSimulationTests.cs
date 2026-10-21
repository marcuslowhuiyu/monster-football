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
    public void Full_match_plays_two_halves_and_finishes_after_regular_time()
    {
        var result = CheckedRun.ToCompletion(NewSimulation());
        var clock = result.FinalState.Clock;

        Assert.Equal(MatchPhase.Finished, result.FinalState.Phase);
        Assert.Equal(2, clock.Period);
        Assert.True(clock.Now.PeriodSeconds >= MatchConfig.Default.PeriodSeconds);

        var ends = result.Events.Where(e => e.Type == MatchEventType.PeriodEnded).ToList();
        Assert.Equal(["1", "2"], ends.Select(e => e.Metadata["period"]));
        Assert.Equal(MatchEventType.MatchFinished, result.Events[^1].Type);
        Assert.Same(ends[1], result.Events[^2]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)] // first half ends during a goal stoppage; the scorers still kick off the second half
    public void The_other_team_kicks_off_the_second_half_at_45_minutes(int seed)
    {
        var events = NewSimulation(seed).RunToCompletion().Events;
        var opening = events.First(e => e.Type == MatchEventType.Kickoff);
        var halfTime = events.ToList().FindIndex(e => e.Type == MatchEventType.PeriodEnded);
        var secondHalf = events.Skip(halfTime).First(e => e.Type == MatchEventType.Kickoff);

        Assert.Equal(opening.Team!.Value.Opponent(), secondHalf.Team);
        Assert.Equal(2, secondHalf.Timestamp.Period);
        Assert.Equal("45:00", secondHalf.Timestamp.ToString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Added_time_is_announced_once_per_half_from_its_goals_penalties_cards_and_wasted_time(int seed)
    {
        var events = NewSimulation(seed).RunToCompletion().Events;
        var announced = events.Where(e => e.Type == MatchEventType.AddedTimeAnnounced).ToList();

        Assert.Equal(2, announced.Count);
        foreach (var (e, period) in announced.Select((e, i) => (e, i + 1)))
        {
            Assert.Equal(period, e.Timestamp.Period);
            Assert.Equal(MatchConfig.Default.PeriodSeconds, e.Timestamp.PeriodSeconds);
            bool InRegularTime(MatchEvent x) => x.Timestamp.Period == period && x.Timestamp.PeriodSeconds < MatchConfig.Default.PeriodSeconds;
            var allowances = MatchConfig.Default.AddedTime;
            var seconds = events.Where(InRegularTime).Sum(x => x.Type switch
            {
                MatchEventType.GoalScored => allowances.PerGoalSeconds,
                MatchEventType.PlayStopped => (x.Metadata["restart"] == "Penalty" ? allowances.PerPenaltySeconds : 0) + double.Parse(x.Metadata["wasted"], System.Globalization.CultureInfo.InvariantCulture),
                MatchEventType.YellowCard => allowances.PerCardSeconds,
                MatchEventType.RedCard => allowances.PerDismissalSeconds + (x.Metadata["reason"] == "SecondYellow" ? 0 : allowances.PerCardSeconds),
                _ => 0,
            });
            var expected = (int)Math.Ceiling(seconds / 60 - 1e-9);
            Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), e.Metadata["minutes"]);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_half_ends_at_a_stoppage_after_time_is_up_or_at_the_safety_cap(int seed)
    {
        var config = MatchConfig.Default;
        var events = NewSimulation(seed).RunToCompletion().Events;
        foreach (var end in events.Where(e => e.Type == MatchEventType.PeriodEnded))
        {
            var minutes = double.Parse(events.Last(e => e.Type == MatchEventType.AddedTimeAnnounced && e.Timestamp.Period == end.Timestamp.Period)
                .Metadata["minutes"], System.Globalization.CultureInfo.InvariantCulture);
            var timeUp = config.PeriodSeconds + 60 * minutes;
            Assert.True(end.Timestamp.PeriodSeconds >= timeUp, $"seed {seed}: {end} before time was up");
            if (end.Metadata["end"] == "SafetyCap")
                Assert.True(end.Timestamp.PeriodSeconds >= timeUp + config.AddedTime.SafetyCapSeconds);
            else
                Assert.Equal("Stoppage", end.Metadata["end"]);
        }
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
        Assert.Equal(2, finished.Timestamp.Period);
        Assert.True(finished.Timestamp.PeriodSeconds >= MatchConfig.Default.PeriodSeconds);
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
    public void Short_configured_match_plays_both_halves_and_finishes()
    {
        var config = MatchConfig.Default with { PeriodSeconds = 10 };
        var result = CheckedRun.ToCompletion(NewSimulation(config: config));

        Assert.Equal(2, result.FinalState.Clock.Period);
        Assert.Equal(2, result.Events.Count(e => e.Type == MatchEventType.PeriodEnded));
        Assert.True(result.FinalState.Clock.Tick >= 2 * config.PeriodTicks);
    }
}
