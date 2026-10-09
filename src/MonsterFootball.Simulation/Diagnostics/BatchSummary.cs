using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Diagnostics;

/// <summary>
/// Aggregate figures over many matches. Built from summaries in seed order, so the result is identical
/// however the matches were scheduled (e.g. in parallel).
/// </summary>
public sealed record BatchSummary(
    int Matches,
    double AverageGoals,
    double AverageShots,
    double AverageShotsOnTarget,
    double AveragePasses,
    double PassCompletion,
    double HomeWinShare,
    double DrawShare,
    double AwayWinShare,
    double AverageHomePossession,
    double AverageTackles,
    double AverageCarries,
    double AveragePossessionChanges,
    double StrikerGoalShare,
    double TeamMatchesWithoutShotShare,
    int MaxGoalsInMatch,
    double AverageOutfieldDistanceKm,
    double AverageOutfieldStaminaAtFullTime,
    double AverageBallInPlayMinutes,
    double AverageAddedMinutesPerPeriod,
    double SafetyCapPeriodShare)
{
    public double AverageThrowIns { get; init; }
    public double AverageGoalKicks { get; init; }
    public double AverageCorners { get; init; }
    public double AverageOffsides { get; init; }
    public double AverageFouls { get; init; }
    public double AveragePenalties { get; init; }
    public double PenaltyConversion { get; init; }

    public static BatchSummary From(IReadOnlyList<MatchSummary> summaries)
    {
        if (summaries.Count == 0)
            throw new ArgumentException("At least one match is required.", nameof(summaries));

        var ordered = summaries.OrderBy(s => s.Seed).ToList();
        var n = (double)ordered.Count;
        var goals = ordered.Sum(s => s.HomeGoals + s.AwayGoals);
        var attempted = ordered.Sum(s => s.Statistics.Home.PassesAttempted + s.Statistics.Away.PassesAttempted);
        var completed = ordered.Sum(s => s.Statistics.Home.PassesCompleted + s.Statistics.Away.PassesCompleted);

        return new BatchSummary(
            ordered.Count,
            goals / n,
            ordered.Average(s => s.Statistics.Home.Shots + s.Statistics.Away.Shots),
            ordered.Average(s => s.Statistics.Home.ShotsOnTarget + s.Statistics.Away.ShotsOnTarget),
            attempted / n,
            attempted == 0 ? 0 : (double)completed / attempted,
            ordered.Count(s => s.HomeGoals > s.AwayGoals) / n,
            ordered.Count(s => s.HomeGoals == s.AwayGoals) / n,
            ordered.Count(s => s.HomeGoals < s.AwayGoals) / n,
            ordered.Average(s => s.Statistics.PossessionShare(TeamSide.Home)),
            ordered.Average(s => s.Tackles),
            ordered.Average(s => s.Carries),
            ordered.Average(s => s.Statistics.PossessionChanges),
            goals == 0 ? 0 : (double)ordered.Sum(s => s.StrikerGoals) / goals,
            ordered.Sum(s => (s.Statistics.Home.Shots == 0 ? 1 : 0) + (s.Statistics.Away.Shots == 0 ? 1 : 0)) / (2 * n),
            ordered.Max(s => s.HomeGoals + s.AwayGoals),
            ordered.Average(s => s.OutfieldDistanceKm),
            ordered.Average(s => s.OutfieldStaminaAtFullTime),
            ordered.Average(s => s.Statistics.BallInPlaySeconds) / 60,
            (double)ordered.Sum(s => s.AddedMinutes) / ordered.Sum(s => s.Periods),
            (double)ordered.Sum(s => s.SafetyCapPeriods) / ordered.Sum(s => s.Periods))
        {
            AverageThrowIns = ordered.Average(s => s.Statistics.Home.ThrowIns + s.Statistics.Away.ThrowIns),
            AverageGoalKicks = ordered.Average(s => s.Statistics.Home.GoalKicks + s.Statistics.Away.GoalKicks),
            AverageCorners = ordered.Average(s => s.Statistics.Home.Corners + s.Statistics.Away.Corners),
            AverageOffsides = ordered.Average(s => s.Statistics.Home.Offsides + s.Statistics.Away.Offsides),
            AverageFouls = ordered.Average(s => s.Statistics.Home.Fouls + s.Statistics.Away.Fouls),
            AveragePenalties = ordered.Average(s => s.Statistics.Home.Penalties + s.Statistics.Away.Penalties),
            PenaltyConversion = ordered.Sum(s => s.Statistics.Home.Penalties + s.Statistics.Away.Penalties) is var taken and > 0
                ? (double)ordered.Sum(s => s.Statistics.Home.PenaltiesScored + s.Statistics.Away.PenaltiesScored) / taken
                : 0,
        };
    }
}
