namespace MonsterFootball.Simulation.Core;

/// <summary>
/// Whether the ball is in play (ADR-023). Play stops (<see cref="Stopped"/>: the dead ball, while the minimum
/// dead-ball time runs and players head for their restart positions), the restart is set up
/// (<see cref="RestartSetup"/>: waiting until everyone is in position), and the restart is taken, returning to
/// <see cref="OpenPlay"/>. <see cref="MatchState.CurrentStoppage"/> describes the stoppage while play is stopped.
/// </summary>
public enum MatchPhase
{
    Stopped,
    RestartSetup,
    OpenPlay,
    Finished,
}
