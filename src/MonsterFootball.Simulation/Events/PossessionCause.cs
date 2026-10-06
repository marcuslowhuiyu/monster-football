namespace MonsterFootball.Simulation.Events;

/// <summary>
/// How a player came to have the ball. Carried by <see cref="MatchEventType.PossessionChanged"/> as the
/// <c>cause</c> metadata so consumers can tell possession won in open play from a restart awarded by the laws.
/// </summary>
public enum PossessionCause
{
    /// <summary>A completed pass reached a teammate.</summary>
    Reception,

    /// <summary>An opponent cut out a pass.</summary>
    Interception,

    /// <summary>A defender won the ball cleanly in a tackle.</summary>
    Tackle,

    /// <summary>The nearest player collected a loose ball.</summary>
    LooseBall,

    /// <summary>The goalkeeper saved a shot and kept the ball.</summary>
    Save,

    /// <summary>The goalkeeper restarted play after a missed shot (stand-in for a goal kick).</summary>
    KeeperRestart,

    /// <summary>The kicking side took a kickoff.</summary>
    Kickoff,
}

public static class PossessionCauseExtensions
{
    public const string MetadataKey = "cause";

    /// <summary>True when the ball was won in open play; false for restarts (kickoffs, keeper restarts).</summary>
    public static bool IsOpenPlay(this PossessionCause cause) =>
        cause is not (PossessionCause.Kickoff or PossessionCause.KeeperRestart);

    /// <summary>The cause recorded on a possession event, or null if the event carries none.</summary>
    public static PossessionCause? GetPossessionCause(this MatchEvent e) =>
        e.Metadata.TryGetValue(MetadataKey, out var value) && Enum.TryParse<PossessionCause>(value, out var cause)
            ? cause
            : null;
}
