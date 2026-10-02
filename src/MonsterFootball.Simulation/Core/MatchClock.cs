namespace MonsterFootball.Simulation.Core;

/// <summary>
/// Fixed-timestep match clock. The integer tick count is authoritative;
/// seconds are derived from it, so progression never depends on rendering framerate.
/// </summary>
public sealed class MatchClock
{
    internal MatchClock(double tickSeconds)
    {
        TickSeconds = tickSeconds;
    }

    public double TickSeconds { get; }
    public long Tick { get; private set; }
    public MatchTime Now => new(Tick * TickSeconds);

    /// <summary>The only way the clock changes: forward by exactly one tick.</summary>
    internal void Advance() => Tick++;
}
