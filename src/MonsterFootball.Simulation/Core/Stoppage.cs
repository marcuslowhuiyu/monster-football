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

    /// <summary>A defender fouled the ball carrier.</summary>
    Foul,
}

/// <summary>How play resumes after a stoppage. New restart types are appended as they are implemented.</summary>
public enum RestartType
{
    Kickoff,
    ThrowIn,
    GoalKick,
    Corner,
    IndirectFreeKick,
    DirectFreeKick,
    Penalty,
}

/// <summary>
/// One stoppage (ADR-023): why play stopped, how it resumes, who takes the restart, where, and when play stopped.
/// Fields are added only when a law needs them (offender, fouled player, disciplinary outcome, …).
/// </summary>
public sealed record Stoppage(StoppageReason Reason, RestartType Restart, TeamSide AwardedTeam, Vec2 Location, long StoppedAtTick)
{
    /// <summary>The player who committed the offence (offside, foul), if any.</summary>
    public PlayerId? Offender { get; init; }

    /// <summary>The player fouled, for a foul.</summary>
    public PlayerId? Fouled { get; init; }

    /// <summary>The card shown to the offender, if any.</summary>
    public DisciplinaryOutcome Discipline { get; init; }

    /// <summary>How long the ball stays dead before the restart setup begins (ADR-037); 0 for opening kickoffs.</summary>
    public double DeadBallSeconds { get; init; }
}

/// <summary>The card shown for a foul.</summary>
public enum DisciplinaryOutcome
{
    None,
    Yellow,

    /// <summary>A second caution: the player is sent off.</summary>
    SecondYellow,

    /// <summary>A straight red: the player is sent off.</summary>
    Red,
}
