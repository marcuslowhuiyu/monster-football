using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Restarts;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Core;

/// <summary>Match-wide settings. Defaults: 105 × 68 pitch, 0.25 s ticks, two halves of 45 minutes plus added time.</summary>
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

    /// <summary>Regular length of each period (half), before added time.</summary>
    public double PeriodSeconds
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(PeriodSeconds), value, "Period length must be positive.");
    } = 45 * 60;

    public int Periods
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(Periods), value, "A match needs at least one period.");
    } = 2;

    /// <summary>Added-time allowances and the safety cap that ends a period if play never stops (ADR-024).</summary>
    public AddedTimeTuning AddedTime { get; init; } = AddedTimeTuning.Default;

    /// <summary>Utility AI weights (ADR-018).</summary>
    public AiTuning Ai { get; init; } = AiTuning.Default;

    /// <summary>Restart timing and law distances (ADR-023).</summary>
    public RestartTuning Restarts { get; init; } = RestartTuning.Default;

    /// <summary>Dead-ball times after each stoppage and the wasted time the referee adds (ADR-037).</summary>
    public DeadBallTuning DeadBall { get; init; } = DeadBallTuning.Default;

    /// <summary>Whether offside is enforced (ADR-029).</summary>
    public bool OffsideEnabled { get; init; } = true;

    /// <summary>Band and channel splits for the 20 tactical zones (ADR-027).</summary>
    public ZoneBoundaries Zones { get; init; } = ZoneBoundaries.Default;

    /// <summary>Ticks in a period's regular time, rounded up so a period never ends early.</summary>
    public long PeriodTicks => TicksFor(PeriodSeconds);

    /// <summary>Whole ticks covering <paramref name="seconds"/>, rounded up.</summary>
    public long TicksFor(double seconds) => (long)Math.Ceiling(seconds / TickSeconds - 1e-9);
}
