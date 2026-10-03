using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Statistics;

namespace MonsterFootball.Simulation;

/// <summary>Outcome of a completed match.</summary>
public sealed record MatchResult(
    int HomeGoals,
    int AwayGoals,
    IReadOnlyList<MatchEvent> Events,
    MatchState FinalState,
    MatchStatistics Statistics,
    int Seed);
