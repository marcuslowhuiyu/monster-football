using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Restarts;

namespace MonsterFootball.Simulation.Core;

/// <summary>Match-wide settings. Defaults: 105 × 68 pitch, 0.25 s ticks, 90 minutes.</summary>
public sealed record MatchConfig
{
    public static MatchConfig Default { get; } = new();

    public PitchGeometry Pitch { get; init; } = PitchGeometry.Standard;

    /// <summary>Simulated seconds per tick.</summary>
    public double TickSeconds
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(TickSeconds), value, "Tick length must be positive.");
    } = 0.25;

    public double MatchDurationSeconds
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(MatchDurationSeconds), value, "Match duration must be positive.");
    } = 90 * 60;

    /// <summary>Utility AI weights (ADR-018).</summary>
    public AiTuning Ai { get; init; } = AiTuning.Default;

    /// <summary>Restart timing and law distances (ADR-023).</summary>
    public RestartTuning Restarts { get; init; } = RestartTuning.Default;

    /// <summary>Ticks in a full match, rounded up so the match never ends early.</summary>
    public long TotalTicks => (long)Math.Ceiling(MatchDurationSeconds / TickSeconds - 1e-9);
}
