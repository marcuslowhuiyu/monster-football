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
    public double AverageClearances { get; init; }
    public double AverageCrosses { get; init; }
    public double AverageShotsFromCrosses { get; init; }
    public double AverageGoalsFromCrosses { get; init; }
    public double AverageYellowCards { get; init; }
    public double AverageRedCards { get; init; }
    public double AbandonedShare { get; init; }
    public double AveragePenalties { get; init; }
    public double PenaltyConversion { get; init; }

    /// <summary>Mean dead-ball seconds per stoppage of each type (ADR-037); 0 when the batch has none.</summary>
    public double AverageThrowInDeadBallSeconds { get; init; }
    public double AverageGoalKickDeadBallSeconds { get; init; }
    public double AverageCornerDeadBallSeconds { get; init; }
    public double AverageDirectFreeKickDeadBallSeconds { get; init; }
    public double AverageIndirectFreeKickDeadBallSeconds { get; init; }

    /// <summary>Mean dead-ball seconds from a goal to the kickoff.</summary>
    public double AverageGoalDeadBallSeconds { get; init; }
    public double AveragePenaltyDeadBallSeconds { get; init; }

    /// <summary>Mean seconds per match of slow-down by a leading side (added to added time only in regular time).</summary>
    public double AverageWastedSeconds { get; init; }

    public static BatchSummary From(IReadOnlyList<MatchSummary> summaries)
    {
        if (summaries.Count == 0)
            throw new ArgumentException("At least one match is required.", nameof(summaries));

        var ordered = summaries.OrderBy(s => s.Seed).ToList();
        var n = (double)ordered.Count;
        var goals = ordered.Sum(s => s.HomeGoals + s.AwayGoals);
        var attempted = ordered.Sum(s => s.Statistics.Home.PassesAttempted + s.Statistics.Away.PassesAttempted);
        var completed = ordered.Sum(s => s.Statistics.Home.PassesCompleted + s.Statistics.Away.PassesCompleted);
        var stoppages = ordered.SelectMany(s => s.DeadBalls).ToList();
        double DeadBall(RestartType restart) =>
            stoppages.Where(d => d.Restart == restart).Select(d => d.Seconds).DefaultIfEmpty(0).Average();

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
            AverageClearances = ordered.Average(s => s.Statistics.Home.Clearances + s.Statistics.Away.Clearances),
            AverageCrosses = ordered.Average(s => s.Statistics.Home.Crosses + s.Statistics.Away.Crosses),
            AverageShotsFromCrosses = ordered.Average(s => s.Statistics.Home.ShotsFromCrosses + s.Statistics.Away.ShotsFromCrosses),
            AverageGoalsFromCrosses = ordered.Average(s => s.Statistics.Home.GoalsFromCrosses + s.Statistics.Away.GoalsFromCrosses),
            AverageYellowCards = ordered.Average(s => s.Statistics.Home.YellowCards + s.Statistics.Away.YellowCards),
            AverageRedCards = ordered.Average(s => s.Statistics.Home.RedCards + s.Statistics.Away.RedCards),
            AbandonedShare = ordered.Count(s => s.Abandoned) / n,
            AveragePenalties = ordered.Average(s => s.Statistics.Home.Penalties + s.Statistics.Away.Penalties),
            PenaltyConversion = ordered.Sum(s => s.Statistics.Home.Penalties + s.Statistics.Away.Penalties) is var taken and > 0
                ? (double)ordered.Sum(s => s.Statistics.Home.PenaltiesScored + s.Statistics.Away.PenaltiesScored) / taken
                : 0,
            AverageThrowInDeadBallSeconds = DeadBall(RestartType.ThrowIn),
            AverageGoalKickDeadBallSeconds = DeadBall(RestartType.GoalKick),
            AverageCornerDeadBallSeconds = DeadBall(RestartType.Corner),
            AverageDirectFreeKickDeadBallSeconds = DeadBall(RestartType.DirectFreeKick),
            AverageIndirectFreeKickDeadBallSeconds = DeadBall(RestartType.IndirectFreeKick),
            AverageGoalDeadBallSeconds = DeadBall(RestartType.Kickoff), // only goals emit PlayStopped with a kickoff
            AveragePenaltyDeadBallSeconds = DeadBall(RestartType.Penalty),
            AverageWastedSeconds = ordered.Average(s => s.DeadBalls.Sum(d => d.WastedSeconds)),
        };
    }
}
