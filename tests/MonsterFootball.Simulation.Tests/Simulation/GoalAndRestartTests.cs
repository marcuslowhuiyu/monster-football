using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class GoalAndRestartTests
{
    private static MatchSimulation NewSimulation(int seed = 123456) =>
        new(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed);

    /// <summary>Steps a match until the first goal has been scored; null if there is none.</summary>
    private static MatchSimulation? UntilFirstGoal(int seed)
    {
        var simulation = NewSimulation(seed);
        while (!simulation.IsFinished && simulation.State.Score.Home + simulation.State.Score.Away == 0)
            simulation.Step();
        return simulation.IsFinished ? null : simulation;
    }

    [Fact]
    public void A_goal_stops_play_with_a_kickoff_awarded_to_the_conceding_team()
    {
        var simulation = Enumerable.Range(1, 8).Select(UntilFirstGoal).First(s => s is not null)!;
        var goal = simulation.Events.Last(e => e.Type == MatchEventType.GoalScored);
        var stopped = simulation.Events[^1];

        Assert.Equal(MatchPhase.Stopped, simulation.State.Phase);
        Assert.Equal(MatchEventType.PlayStopped, stopped.Type);
        Assert.Equal(goal.Timestamp, stopped.Timestamp);

        var stoppage = simulation.State.CurrentStoppage!;
        Assert.Equal(StoppageReason.Goal, stoppage.Reason);
        Assert.Equal(RestartType.Kickoff, stoppage.Restart);
        Assert.Equal(goal.Team!.Value.Opponent(), stoppage.AwardedTeam);
        Assert.Equal(simulation.State.Pitch.Centre, stoppage.Location);
        Assert.Equal("Goal", stopped.Metadata["reason"]);
        Assert.Equal("Kickoff", stopped.Metadata["restart"]);
        Assert.Equal(stoppage.AwardedTeam, stopped.Team);
        Assert.Null(simulation.State.PossessionPlayerId);
    }

    [Fact]
    public void During_the_stoppage_players_walk_back_and_nothing_else_happens_until_the_kickoff()
    {
        // A goal once time is up ends the period instead (ADR-024); a regular-time goal is always followed by a kickoff.
        var simulation = Enumerable.Range(1, 8).Select(UntilFirstGoal)
            .First(s => s is not null && s.State.Clock.Tick < s.State.Clock.RegularEndTick)!;
        var state = simulation.State;
        var config = MatchConfig.Default;
        var awarded = state.CurrentStoppage!.AwardedTeam;
        var deadBall = state.CurrentStoppage!.DeadBallSeconds;
        var stoppedAt = state.Clock.Now.Seconds - config.TickSeconds;
        var eventsBefore = simulation.Events.Count;

        while (state.Phase != MatchPhase.OpenPlay)
        {
            var before = state.Players.ToDictionary(p => p.PlayerId, p => p.Position);
            simulation.Step();
            MatchStateInvariants.Verify(state);
            if (state.Phase == MatchPhase.OpenPlay)
                break;

            Assert.Null(state.PossessionPlayerId);
            foreach (var player in state.Players)
                Assert.True(player.Position.DistanceTo(before[player.PlayerId]) <= MovementSystem.TopSpeed(player) * config.TickSeconds + 1e-9,
                    $"{player} teleported during the stoppage");
        }

        var newEvents = simulation.Events.Skip(eventsBefore).Select(e => e.Type).ToList();
        Assert.Equal(MatchEventType.Kickoff, newEvents[0]);
        var kickoff = simulation.Events[eventsBefore];
        Assert.Equal(awarded, kickoff.Team);
        Assert.InRange(kickoff.Timestamp.Seconds - stoppedAt, deadBall, deadBall + config.Restarts.SetupTimeoutSeconds + config.TickSeconds);

        // At the kickoff everyone is in their own half and opponents are outside the centre circle.
        foreach (var player in state.Players)
        {
            Assert.True(state.Pitch.IsInOwnHalf(player.Position, player.Team), $"{player} not in own half at the kickoff");
            if (player.Team != awarded)
                Assert.True(player.Position.DistanceTo(state.Pitch.Centre) >= config.Restarts.KickoffExclusionRadius - 1e-9,
                    $"{player} inside the centre circle at the kickoff");
        }
    }

    [Fact]
    public void Score_matches_goal_events_over_full_matches()
    {
        var totalGoals = 0;
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var result = CheckedRun.ToCompletion(NewSimulation(seed));
            var goals = result.Events.Where(e => e.Type == MatchEventType.GoalScored).ToList();

            Assert.Equal(result.HomeGoals, goals.Count(e => e.Team == TeamSide.Home));
            Assert.Equal(result.AwayGoals, goals.Count(e => e.Team == TeamSide.Away));
            totalGoals += goals.Count;
        }

        Assert.True(totalGoals > 0, "no goals in 8 matches");
    }

    [Fact]
    public void Every_goal_is_followed_by_a_kickoff_for_the_conceding_team()
    {
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var events = NewSimulation(seed).RunToCompletion().Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != MatchEventType.GoalScored)
                    continue;

                // A half that ends during the goal stoppage restarts with the period's own kickoff instead.
                var next = events.Skip(i + 1).First(e => e.Type is MatchEventType.Kickoff or MatchEventType.PeriodEnded);
                if (next.Type == MatchEventType.Kickoff)
                    Assert.Equal(events[i].Team!.Value.Opponent(), next.Team);
            }
        }
    }

    [Fact]
    public void Possession_after_a_goal_is_a_kickoff_not_a_ball_won()
    {
        var kickoffChanges = 0;
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var events = NewSimulation(seed).RunToCompletion().Events;
            for (var i = 1; i < events.Count; i++)
            {
                if (events[i].Type != MatchEventType.PossessionChanged || events[i - 1].Type != MatchEventType.Kickoff)
                    continue;
                Assert.Equal(PossessionCause.Kickoff, events[i].GetPossessionCause());
                kickoffChanges++;
            }
        }

        Assert.True(kickoffChanges > 0, "no post-goal kickoffs in 8 matches");
    }

    [Fact]
    public void Every_shot_has_exactly_one_outcome_after_it_has_travelled()
    {
        MatchEventType[] outcomes = [MatchEventType.GoalScored, MatchEventType.ShotSaved, MatchEventType.ShotMissed];
        foreach (var seed in Enumerable.Range(1, 4))
        {
            var events = NewSimulation(seed).RunToCompletion().Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != MatchEventType.ShotTaken)
                    continue;

                // Nothing else can happen while a shot is in the air — not even the end of a half (a half ends only
                // at a stoppage): the next event is its outcome, applied later than the strike.
                var next = events[i + 1];
                Assert.Contains(next.Type, outcomes);
                Assert.True(next.Timestamp.Seconds > events[i].Timestamp.Seconds, $"seed {seed}: outcome at the strike tick");
            }
        }
    }

    [Fact]
    public void Score_never_decreases_during_a_match()
    {
        var simulation = NewSimulation(2);
        var home = 0;
        var away = 0;
        while (!simulation.IsFinished)
        {
            simulation.Step();
            Assert.True(simulation.State.Score.Home >= home && simulation.State.Score.Away >= away);
            home = simulation.State.Score.Home;
            away = simulation.State.Score.Away;
        }
    }

    [Fact]
    public void Goalkeepers_never_shoot_or_carry()
    {
        var result = NewSimulation(4).RunToCompletion();
        foreach (var e in result.Events.Where(e => e.Type is MatchEventType.ShotTaken or MatchEventType.CarryStarted))
            Assert.False(result.FinalState.Player(e.ActorPlayerId!.Value).Definition.Position.IsGoalkeeper());
    }

    [Fact]
    public void Every_ball_out_foul_or_offside_is_followed_by_its_restart_or_the_end_of_the_half()
    {
        var restarts = 0;
        foreach (var seed in Enumerable.Range(1, 6))
        {
            var events = NewSimulation(seed).RunToCompletion().Events;
            for (var i = 0; i < events.Count; i++)
            {
                var stop = events[i];
                if (stop.Type != MatchEventType.PlayStopped
                    || stop.Metadata["reason"] is not (nameof(StoppageReason.BallOutOfPlay) or nameof(StoppageReason.Foul) or nameof(StoppageReason.Offside)))
                    continue;

                var next = events.Skip(i + 1).First(e => e.Type is MatchEventType.RestartTaken or MatchEventType.PeriodEnded);
                if (next.Type == MatchEventType.PeriodEnded)
                    continue;
                Assert.Equal(stop.Metadata["restart"], next.Metadata["restart"]);
                Assert.Equal(stop.Team, next.Team);
                Assert.Equal(stop.Position, next.Position);
                restarts++;
            }
        }

        Assert.True(restarts > 6 * 10, $"only {restarts} restarts from the ball going out in 6 matches");
    }
}
