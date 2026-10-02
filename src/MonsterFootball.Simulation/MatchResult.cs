using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation;

/// <summary>Outcome of a completed match. Statistics are added in M1.10.</summary>
public sealed record MatchResult(
    int HomeGoals,
    int AwayGoals,
    IReadOnlyList<MatchEvent> Events,
    MatchState FinalState,
    int Seed);
