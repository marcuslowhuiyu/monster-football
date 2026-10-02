using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class GoalAndRestartTests
{
    private static MatchSimulation NewSimulation(int seed = 123456) =>
        new(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed);

    [Fact]
    public void After_a_goal_players_reset_and_the_conceding_team_kicks_off()
    {
        var simulation = NewSimulation();
        for (var i = 0; i < 40; i++)
            simulation.Step();

        // Simulate the aftermath of a Home goal.
        simulation.State.Phase = MatchPhase.GoalRestart;
        simulation.State.KickoffTeam = TeamSide.Away;

        simulation.Step(); // GoalRestart → reset
        Assert.Equal(MatchPhase.Kickoff, simulation.State.Phase);
        Assert.True(simulation.State.Ball.IsLoose);
        Assert.Equal(simulation.State.Pitch.Centre, simulation.State.Ball.Position);
        foreach (var player in simulation.State.Players)
            Assert.Equal(Formation433.KickoffPosition(simulation.State.Pitch, player.Team, player.Definition.Position), player.Position);

        simulation.Step(); // Kickoff
        var kickoff = simulation.Events.Last(e => e.Type == MatchEventType.Kickoff);
        Assert.Equal(simulation.State.Clock.Now.Seconds - MatchConfig.Default.TickSeconds, kickoff.Timestamp.Seconds);
        Assert.Equal(TeamSide.Away, kickoff.Team);
        Assert.Equal(TeamSide.Away, simulation.State.PossessionTeam);
        Assert.Equal(PlayingPosition.ST, simulation.State.Player(kickoff.ActorPlayerId!.Value).Definition.Position);
        Assert.Equal(MatchPhase.OpenPlay, simulation.State.Phase);
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

                var next = events.Skip(i + 1).First(e => e.Type is MatchEventType.Kickoff or MatchEventType.MatchFinished);
                if (next.Type == MatchEventType.Kickoff)
                    Assert.Equal(events[i].Team!.Value.Opponent(), next.Team);
            }
        }
    }

    [Fact]
    public void Every_shot_has_exactly_one_outcome()
    {
        var events = NewSimulation(3).RunToCompletion().Events;
        for (var i = 0; i < events.Count; i++)
            if (events[i].Type == MatchEventType.ShotTaken)
                Assert.Contains(events[i + 1].Type, new[] { MatchEventType.GoalScored, MatchEventType.ShotSaved, MatchEventType.ShotMissed });
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
}
