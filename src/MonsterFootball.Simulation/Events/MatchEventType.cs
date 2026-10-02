namespace MonsterFootball.Simulation.Events;

/// <summary>Event types are appended as each subtask implements the behaviour that emits them.</summary>
public enum MatchEventType
{
    MatchStarted,
    Kickoff,
    PassAttempted,
    PassCompleted,
    PassIntercepted,
    PassMisplaced,
    PossessionChanged,
    CarryStarted,
    TackleAttempted,
    TackleWon,
    TackleLost,
    TackleLooseBall,
    MatchFinished,
}
