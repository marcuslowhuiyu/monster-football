namespace MonsterFootball.Simulation.Restarts;

/// <summary>Restart timing and law distances, as data (<see cref="Core.MatchConfig.Restarts"/>).</summary>
public sealed record RestartTuning
{
    public static RestartTuning Default { get; } = new();

    /// <summary>Minimum time between a goal and the kickoff (celebration, walking back).</summary>
    public double GoalDeadBallSeconds { get; init; } = 45;

    /// <summary>Once the dead-ball time is over, how long the restart waits for players to reach position before
    /// placing any stragglers directly.</summary>
    public double SetupTimeoutSeconds { get; init; } = 30;

    /// <summary>A player is in position for a restart within this distance of their restart target.</summary>
    public double InPositionTolerance { get; init; } = 1;

    /// <summary>At a kickoff, opponents stay at least this far from the ball (the centre circle).</summary>
    public double KickoffExclusionRadius { get; init; } = 9.15;
}
