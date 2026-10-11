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

    /// <summary>Time added for each penalty awarded in a period's regular time (D3).</summary>
    public double PerPenaltySeconds { get; init; } = 45;

    /// <summary>Time added for each caution in a period's regular time (D3).</summary>
    public double PerCardSeconds { get; init; } = 20;

    /// <summary>Time added for each dismissal in a period's regular time (D3).</summary>
    public double PerDismissalSeconds { get; init; } = 30;

    /// <summary>
    /// A period ends at the first stoppage after regular plus added time; if play has not stopped this long after
    /// that, it ends at the next tick with no ball in flight. 180 s (D2, ADR-033): about one half in ten still reaches it
    /// until clearances (D6) bring more stoppages.
    /// </summary>
    public double SafetyCapSeconds { get; init; } = 180;
}
