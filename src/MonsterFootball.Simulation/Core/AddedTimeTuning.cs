namespace MonsterFootball.Simulation.Core;

/// <summary>
/// Added time (stoppage time) as data (ADR-024): allowances for significant stoppages during a period's regular time,
/// announced rounded up to whole minutes, and the safety cap that ends a period if play never stops.
/// </summary>
public sealed record AddedTimeTuning
{
    public static AddedTimeTuning Default { get; } = new();

    /// <summary>Time added for each goal scored in a period's regular time.</summary>
    public double PerGoalSeconds { get; init; } = 45;

    /// <summary>
    /// A period ends at the first stoppage after regular plus added time; if play has not stopped this long after
    /// that, it ends at the next tick with no ball in flight. Interim value until the ball can go out of play (M2.4),
    /// when goals are the only stoppages; revisit at the M2.7 calibration checkpoint.
    /// </summary>
    public double SafetyCapSeconds { get; init; } = 60;
}
