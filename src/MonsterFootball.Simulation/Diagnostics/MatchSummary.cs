using System.Globalization;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Statistics;

namespace MonsterFootball.Simulation.Diagnostics;

/// <summary>Compact per-match figures for batch runs; the full event log is not kept.</summary>
public sealed record MatchSummary(
    int Seed,
    int HomeGoals,
    int AwayGoals,
    MatchStatistics Statistics,
    int Tackles,
    int Carries,
    int StrikerGoals,
    double OutfieldDistanceKm,
    double OutfieldStaminaAtFullTime,
    int Periods,
    int SafetyCapPeriods,
    int AddedMinutes)
{
    public bool Abandoned { get; init; }

    public static MatchSummary From(MatchResult result)
    {
        var state = result.FinalState;
        var outfield = state.Players.Where(p => !p.Definition.Position.IsGoalkeeper()).ToList();

        return new MatchSummary(
            result.Seed,
            result.HomeGoals,
            result.AwayGoals,
            result.Statistics,
            result.Events.Count(e => e.Type == MatchEventType.TackleAttempted),
            result.Events.Count(e => e.Type == MatchEventType.CarryStarted),
            result.Events.Count(e => e.Type == MatchEventType.GoalScored
                                     && state.Player(e.ActorPlayerId!.Value).Definition.Position == PlayingPosition.ST),
            outfield.Average(p => p.DistanceCovered) / 1000,
            outfield.Average(p => p.CurrentStamina),
            result.Events.Count(e => e.Type == MatchEventType.PeriodEnded),
            result.Events.Count(e => e.Type == MatchEventType.PeriodEnded && e.Metadata.GetValueOrDefault("end") == "SafetyCap"),
            result.Events.Where(e => e.Type == MatchEventType.AddedTimeAnnounced)
                .Sum(e => int.Parse(e.Metadata["minutes"], CultureInfo.InvariantCulture)))
        {
            Abandoned = result.Abandoned,
        };
    }
}
