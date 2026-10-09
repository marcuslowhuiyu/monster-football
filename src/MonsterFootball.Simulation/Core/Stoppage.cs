using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Core;

/// <summary>Why play stopped. New reasons are appended as the laws that cause them are added.</summary>
public enum StoppageReason
{
    /// <summary>Before the opening kickoff.</summary>
    MatchStart,

    Goal,

    /// <summary>Before the kickoff that starts a later period (the second half).</summary>
    PeriodStart,

    /// <summary>The ball wholly crossed a touchline or goal line without a goal.</summary>
    BallOutOfPlay,

    /// <summary>A player in an offside position became involved in play.</summary>
    Offside,
}

/// <summary>How play resumes after a stoppage. New restart types are appended as they are implemented.</summary>
public enum RestartType
{
    Kickoff,
    ThrowIn,
    GoalKick,
    Corner,
    IndirectFreeKick,
}

/// <summary>
/// One stoppage (ADR-023): why play stopped, how it resumes, who takes the restart, where, and when play stopped.
/// Fields are added only when a law needs them (offender, fouled player, disciplinary outcome, …).
/// </summary>
public sealed record Stoppage(StoppageReason Reason, RestartType Restart, TeamSide AwardedTeam, Vec2 Location, long StoppedAtTick)
{
    /// <summary>The player who committed the offence (offside), if any.</summary>
    public PlayerId? Offender { get; init; }
}
