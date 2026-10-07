namespace MonsterFootball.Simulation.Core;

/// <summary>
/// Fixed-timestep match clock. The integer tick count is authoritative; seconds and the referee's clock are derived
/// from it, so progression never depends on rendering framerate. It also tracks the current period and, once
/// announced, its added time (ADR-024).
/// </summary>
public sealed class MatchClock
{
    internal MatchClock(double tickSeconds, double regularPeriodSeconds)
    {
        TickSeconds = tickSeconds;
        RegularPeriodSeconds = regularPeriodSeconds;
        RegularPeriodTicks = (long)Math.Ceiling(regularPeriodSeconds / tickSeconds - 1e-9);
    }

    public double TickSeconds { get; }
    public double RegularPeriodSeconds { get; }
    public long RegularPeriodTicks { get; }
    public long Tick { get; private set; }

    /// <summary>The current period, from 1.</summary>
    public int Period { get; private set; } = 1;

    /// <summary>Tick at which the current period started.</summary>
    public long PeriodStartTick { get; private set; }

    /// <summary>Tick at which the current period's regular time ends.</summary>
    public long RegularEndTick => PeriodStartTick + RegularPeriodTicks;

    /// <summary>Added time for the current period in ticks, once announced at the end of regular time; otherwise null.</summary>
    public long? AddedTimeTicks { get; private set; }

    public MatchTime Now => new(Tick * TickSeconds)
    {
        Period = Period,
        PeriodSeconds = (Tick - PeriodStartTick) * TickSeconds,
        RegularPeriodSeconds = RegularPeriodSeconds,
    };

    /// <summary>The only way time moves: forward by exactly one tick.</summary>
    internal void Advance() => Tick++;

    internal void AnnounceAddedTime(long ticks) => AddedTimeTicks = ticks;

    /// <summary>Starts the next period now. Half-time takes no ticks: the referee's clock simply restarts at the period's start.</summary>
    internal void StartNextPeriod()
    {
        Period++;
        PeriodStartTick = Tick;
        AddedTimeTicks = null;
    }
}
