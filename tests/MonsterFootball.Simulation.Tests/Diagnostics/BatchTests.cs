using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Diagnostics;

public class BatchTests
{
    private static MatchSummary Summarise(int seed) =>
        MatchSummary.From(new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed).RunToCompletion());

    [Fact]
    public void Summary_matches_the_result_it_came_from()
    {
        var result = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), 3).RunToCompletion();
        var summary = MatchSummary.From(result);

        Assert.Equal(result.Seed, summary.Seed);
        Assert.Equal(result.HomeGoals, summary.HomeGoals);
        Assert.Equal(result.Statistics, summary.Statistics);
        Assert.InRange(summary.StrikerGoals, 0, result.HomeGoals + result.AwayGoals);
        Assert.True(summary.OutfieldDistanceKm > 1);
        Assert.Equal(2, summary.Periods);
        Assert.InRange(summary.SafetyCapPeriods, 0, summary.Periods);
        Assert.True(summary.AddedMinutes >= 0);
    }

    [Fact]
    public void Batch_aggregate_is_independent_of_input_order()
    {
        var summaries = Enumerable.Range(1, 6).Select(Summarise).ToList();
        var shuffled = summaries.AsEnumerable().Reverse().ToList();

        Assert.Equal(BatchSummary.From(summaries), BatchSummary.From(shuffled));
    }

    [Fact]
    public void Batch_aggregate_is_consistent()
    {
        var batch = BatchSummary.From(Enumerable.Range(1, 6).Select(Summarise).ToList());

        Assert.Equal(6, batch.Matches);
        Assert.Equal(1, batch.HomeWinShare + batch.DrawShare + batch.AwayWinShare, precision: 9);
        Assert.InRange(batch.AverageShotsOnTarget, 0, batch.AverageShots);
        Assert.InRange(batch.PassCompletion, 0, 1);
        Assert.InRange(batch.AverageHomePossession, 0, 1);
        Assert.InRange(batch.AverageBallInPlayMinutes, 45, 100);
        Assert.InRange(batch.SafetyCapPeriodShare, 0, 1);
    }

    [Fact]
    public void Empty_batch_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => BatchSummary.From([]));
    }

    [Fact]
    public void Health_checks_flag_pathological_figures()
    {
        var healthy = BatchSummary.From(Enumerable.Range(1, 6).Select(Summarise).ToList());
        var broken = healthy with { AverageGoals = 25, AverageHomePossession = 0.8, TeamMatchesWithoutShotShare = 0.5 };

        var failed = BatchHealth.Evaluate(broken).Where(c => !c.Passed).Select(c => c.Name).ToList();

        Assert.Contains("Goals per match", failed);
        Assert.Contains("Home possession (equal teams)", failed);
        Assert.Contains("Team-matches without a shot", failed);
        Assert.False(BatchHealth.IsHealthy(broken));
    }

    [Fact]
    public void Check_reports_value_and_band()
    {
        var check = new BatchHealth.Check("Goals per match", 2.5, 0.5, 6);
        Assert.True(check.Passed);
        Assert.StartsWith("ok", check.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("FAIL", (check with { Value = 9 }).ToString(), StringComparison.Ordinal);
    }
}
