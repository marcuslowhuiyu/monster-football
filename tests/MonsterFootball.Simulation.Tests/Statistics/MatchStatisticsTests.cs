using System.Collections.Immutable;
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

    private static MatchEvent Changed(double seconds, TeamSide team, PossessionCause cause) =>
        E(seconds, MatchEventType.PossessionChanged, team) with
        {
            Metadata = ImmutableSortedDictionary.CreateRange([new KeyValuePair<string, string>("cause", cause.ToString())]),
        };

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
            Changed(40, TeamSide.Home, PossessionCause.Tackle),
            Changed(70, TeamSide.Away, PossessionCause.Interception),
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
            Changed(50.25, TeamSide.Away, PossessionCause.Kickoff),
            E(100, MatchEventType.MatchFinished),
        ];

        var stats = MatchStatistics.FromEvents(events);

        Assert.Equal(50.25, stats.Home.PossessionSeconds, precision: 9);
        Assert.Equal(49.75, stats.Away.PossessionSeconds, precision: 9);
    }

    [Fact]
    public void Restarts_change_possession_but_are_not_possessions_won()
    {
        MatchEvent[] events =
        [
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(20, MatchEventType.GoalScored, TeamSide.Home),
            E(20.25, MatchEventType.Kickoff, TeamSide.Away),
            Changed(20.25, TeamSide.Away, PossessionCause.Kickoff),
            Changed(30, TeamSide.Home, PossessionCause.LooseBall),
            E(40, MatchEventType.ShotMissed, TeamSide.Home),
            Changed(40, TeamSide.Away, PossessionCause.KeeperRestart),
            Changed(50, TeamSide.Home, PossessionCause.Save),
            E(60, MatchEventType.MatchFinished),
        ];

        var stats = MatchStatistics.FromEvents(events);

        Assert.Equal(4, stats.PossessionChanges);
        Assert.Equal(2, stats.Home.PossessionsWon);
        Assert.Equal(0, stats.Away.PossessionsWon);
    }

    [Fact]
    public void Possession_and_ball_in_play_time_pause_while_play_is_stopped()
    {
        MatchEvent[] events =
        [
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(20, MatchEventType.GoalScored, TeamSide.Home),
            E(20, MatchEventType.PlayStopped, TeamSide.Away),
            E(65, MatchEventType.Kickoff, TeamSide.Away),
            Changed(65, TeamSide.Away, PossessionCause.Kickoff),
            E(80, MatchEventType.PeriodEnded),
            E(80, MatchEventType.Kickoff, TeamSide.Home),
            Changed(80, TeamSide.Home, PossessionCause.Kickoff),
            E(90, MatchEventType.PeriodEnded),
            E(90, MatchEventType.MatchFinished),
        ];

        var stats = MatchStatistics.FromEvents(events);

        Assert.Equal(20 + 15 + 10, stats.BallInPlaySeconds);
        Assert.Equal(20 + 10, stats.Home.PossessionSeconds);
        Assert.Equal(15, stats.Away.PossessionSeconds);
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
        Assert.Equal(stats.BallInPlaySeconds, stats.Home.PossessionSeconds + stats.Away.PossessionSeconds, precision: 6);
        Assert.True(stats.BallInPlaySeconds < result.FinalState.Clock.Now.Seconds, "stoppages are not ball-in-play time");
        // At least the ball-in-play health band's floor (45 min; milestone §16.4). Real football is 55–60 min.
        Assert.True(stats.BallInPlaySeconds > 45 * 60, $"{stats.BallInPlaySeconds / 60:0.0} min in play");
        var changes = result.Events.Where(e => e.Type == MatchEventType.PossessionChanged).ToList();
        Assert.Equal(changes.Count, stats.PossessionChanges);
        Assert.All(changes, e => Assert.NotNull(e.GetPossessionCause()));
        Assert.Equal(changes.Count(e => e.GetPossessionCause()!.Value.IsOpenPlay()), stats.Home.PossessionsWon + stats.Away.PossessionsWon);
        Assert.True(stats.Home.PossessionsWon + stats.Away.PossessionsWon < stats.PossessionChanges, "keeper restarts are not possessions won");

        foreach (var team in new[] { stats.Home, stats.Away })
        {
            Assert.InRange(team.ShotsOnTarget, team.Goals, team.Shots);
            Assert.InRange(team.PassesCompleted, 0, team.PassesAttempted);
            Assert.True(team.PassesAttempted > 0);
        }
    }

    [Fact]
    public void Play_resumes_at_each_restart_and_restarts_are_counted_for_the_team_taking_them()
    {
        MatchEvent Restart(double seconds, TeamSide team, RestartType type) => E(seconds, MatchEventType.RestartTaken, team) with
        {
            Metadata = ImmutableSortedDictionary.CreateRange([new KeyValuePair<string, string>("restart", type.ToString())]),
        };

        var stats = MatchStatistics.FromEvents(
        [
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(100, MatchEventType.PlayStopped, TeamSide.Away),
            Restart(108, TeamSide.Away, RestartType.ThrowIn),
            E(200, MatchEventType.PlayStopped, TeamSide.Home),
            Restart(220, TeamSide.Home, RestartType.Corner),
            E(250, MatchEventType.PlayStopped, TeamSide.Away),
            Restart(265, TeamSide.Away, RestartType.GoalKick),
            E(300, MatchEventType.MatchFinished),
        ]);

        Assert.Equal(100 + 92 + 30 + 35, stats.BallInPlaySeconds, 9);
        Assert.Equal(100 + 30, stats.Home.PossessionSeconds, 9);
        Assert.Equal((0, 1, 0), (stats.Home.ThrowIns, stats.Home.Corners, stats.Home.GoalKicks));
        Assert.Equal((1, 0, 1), (stats.Away.ThrowIns, stats.Away.Corners, stats.Away.GoalKicks));
    }

    [Fact]
    public void Offsides_count_for_the_offending_team_and_free_kicks_for_the_team_taking_them()
    {
        var stats = MatchStatistics.FromEvents(
        [
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(100, MatchEventType.PlayStopped, TeamSide.Away) with
            {
                Metadata = ImmutableSortedDictionary.CreateRange([new KeyValuePair<string, string>("reason", "Offside"), new KeyValuePair<string, string>("restart", "IndirectFreeKick")]),
            },
            E(112, MatchEventType.RestartTaken, TeamSide.Away) with
            {
                Metadata = ImmutableSortedDictionary.CreateRange([new KeyValuePair<string, string>("restart", "IndirectFreeKick")]),
            },
            E(200, MatchEventType.MatchFinished),
        ]);

        Assert.Equal((1, 0), (stats.Home.Offsides, stats.Away.Offsides));
        Assert.Equal((0, 1), (stats.Home.FreeKicks, stats.Away.FreeKicks));
    }

    [Fact]
    public void Fouls_count_for_the_offending_team_and_penalties_for_the_team_taking_them()
    {
        static ImmutableSortedDictionary<string, string> M(string key, string value) =>
            ImmutableSortedDictionary.CreateRange([new KeyValuePair<string, string>(key, value)]);

        var stats = MatchStatistics.FromEvents(
        [
            E(0, MatchEventType.Kickoff, TeamSide.Home),
            E(50, MatchEventType.FoulCommitted, TeamSide.Away),
            E(50, MatchEventType.PlayStopped, TeamSide.Home),
            E(95, MatchEventType.RestartTaken, TeamSide.Home) with { Metadata = M("restart", "Penalty") },
            E(96, MatchEventType.GoalScored, TeamSide.Home) with { Metadata = M("penalty", "true") },
            E(200, MatchEventType.MatchFinished),
        ]);

        Assert.Equal((0, 1), (stats.Home.Fouls, stats.Away.Fouls));
        var cards = MatchStatistics.FromEvents([E(10, MatchEventType.YellowCard, TeamSide.Away), E(20, MatchEventType.YellowCard, TeamSide.Away), E(20, MatchEventType.RedCard, TeamSide.Away)]);
        Assert.Equal((2, 1), (cards.Away.YellowCards, cards.Away.RedCards));
        Assert.Equal((1, 1), (stats.Home.Penalties, stats.Home.PenaltiesScored));
    }

    [Fact]
    public void Statistics_are_deterministic_by_seed()
    {
        MatchStatistics Run() => new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), 42)
            .RunToCompletion().Statistics;

        Assert.Equal(Run(), Run());
    }
}
