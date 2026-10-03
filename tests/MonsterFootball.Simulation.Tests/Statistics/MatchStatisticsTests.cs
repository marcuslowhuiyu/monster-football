using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Statistics;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Statistics;

public class MatchStatisticsTests
{
    private static MatchEvent E(double seconds, MatchEventType type, TeamSide? team = null) =>
        new(new MatchTime(seconds), type, ActorPlayerId: new PlayerId(1), Team: team);

    [Fact]
    public void Counts_shots_on_target_passes_and_goals_per_team()
    {
        MatchEvent[] events =
        [
            E(0, MatchEventType.MatchStarted),
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(2, MatchEventType.PassAttempted, TeamSide.Home),
            E(3, MatchEventType.PassCompleted, TeamSide.Home),
            E(5, MatchEventType.PassAttempted, TeamSide.Home),
            E(6, MatchEventType.PassMisplaced, TeamSide.Home),
            E(10, MatchEventType.ShotTaken, TeamSide.Home),
            E(10, MatchEventType.ShotSaved, TeamSide.Away),     // saving side on the event
            E(20, MatchEventType.ShotTaken, TeamSide.Home),
            E(20, MatchEventType.GoalScored, TeamSide.Home),
            E(30, MatchEventType.ShotTaken, TeamSide.Away),
            E(30, MatchEventType.ShotMissed, TeamSide.Away),
            E(60, MatchEventType.MatchFinished),
        ];

        var stats = MatchStatistics.FromEvents(events);

        Assert.Equal(1, stats.Home.Goals);
        Assert.Equal(2, stats.Home.Shots);
        Assert.Equal(2, stats.Home.ShotsOnTarget);
        Assert.Equal(2, stats.Home.PassesAttempted);
        Assert.Equal(1, stats.Home.PassesCompleted);
        Assert.Equal(0.5, stats.Home.PassCompletion);
        Assert.Equal(1, stats.Away.Shots);
        Assert.Equal(0, stats.Away.ShotsOnTarget);
        Assert.Equal(0, stats.Away.Goals);
    }

    [Fact]
    public void Possession_time_follows_kickoff_and_possession_changes_until_full_time()
    {
        MatchEvent[] events =
        [
            E(0, MatchEventType.Kickoff, TeamSide.Away),
            E(40, MatchEventType.PossessionChanged, TeamSide.Home),
            E(70, MatchEventType.PossessionChanged, TeamSide.Away),
            E(100, MatchEventType.MatchFinished),
        ];

        var stats = MatchStatistics.FromEvents(events);

        Assert.Equal(30, stats.Home.PossessionSeconds);
        Assert.Equal(70, stats.Away.PossessionSeconds);
        Assert.Equal(0.3, stats.PossessionShare(TeamSide.Home), precision: 9);
        Assert.Equal(2, stats.PossessionChanges);
        Assert.Equal(1, stats.Home.PossessionsWon);
        Assert.Equal(1, stats.Away.PossessionsWon);
    }

    [Fact]
    public void Later_kickoffs_do_not_reset_possession_tracking()
    {
        // After a goal the conceding side kicks off; the switch is carried by PossessionChanged.
        MatchEvent[] events =
        [
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(50, MatchEventType.GoalScored, TeamSide.Home),
            E(50.25, MatchEventType.Kickoff, TeamSide.Away),
            E(50.25, MatchEventType.PossessionChanged, TeamSide.Away),
            E(100, MatchEventType.MatchFinished),
        ];

        var stats = MatchStatistics.FromEvents(events);

        Assert.Equal(50.25, stats.Home.PossessionSeconds, precision: 9);
        Assert.Equal(49.75, stats.Away.PossessionSeconds, precision: 9);
    }

    [Fact]
    public void Empty_match_has_zero_statistics()
    {
        var stats = MatchStatistics.FromEvents([]);
        Assert.Equal(0, stats.Home.PassCompletion);
        Assert.Equal(0, stats.PossessionShare(TeamSide.Home));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Full_match_statistics_are_consistent_with_the_result(int seed)
    {
        var result = CheckedRun.ToCompletion(
            new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed));
        var stats = result.Statistics;

        Assert.Equal(result.HomeGoals, stats.Home.Goals);
        Assert.Equal(result.AwayGoals, stats.Away.Goals);
        Assert.Equal(MatchConfig.Default.MatchDurationSeconds, stats.Home.PossessionSeconds + stats.Away.PossessionSeconds, precision: 6);
        Assert.Equal(result.Events.Count(e => e.Type == MatchEventType.PossessionChanged), stats.PossessionChanges);

        foreach (var team in new[] { stats.Home, stats.Away })
        {
            Assert.InRange(team.ShotsOnTarget, team.Goals, team.Shots);
            Assert.InRange(team.PassesCompleted, 0, team.PassesAttempted);
            Assert.True(team.PassesAttempted > 0);
        }
    }

    [Fact]
    public void Statistics_are_deterministic_by_seed()
    {
        MatchStatistics Run() => new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), 42)
            .RunToCompletion().Statistics;

        Assert.Equal(Run(), Run());
    }
}
