namespace MonsterFootball.Simulation.Restarts;

/// <summary>Restart timing and law distances, as data (<see cref="Core.MatchConfig.Restarts"/>).</summary>
public sealed record RestartTuning
{
    public static RestartTuning Default { get; } = new();

    /// <summary>Minimum time between a goal and the kickoff (celebration, walking back).</summary>
    public double GoalDeadBallSeconds { get; init; } = 45;

    /// <summary>Minimum time between the ball going out and the throw-in.</summary>
    public double ThrowInDeadBallSeconds { get; init; } = 8;

    /// <summary>Minimum time between the ball going out and the goal kick.</summary>
    public double GoalKickDeadBallSeconds { get; init; } = 15;

    /// <summary>Minimum time between the ball going out and the corner (players go up for it).</summary>
    public double CornerDeadBallSeconds { get; init; } = 20;

    /// <summary>Once the dead-ball time is over, how long the restart waits for players to reach position before
    /// placing any stragglers directly.</summary>
    public double SetupTimeoutSeconds { get; init; } = 30;

    /// <summary>A player is in position for a restart within this distance of their restart target.</summary>
    public double InPositionTolerance { get; init; } = 1;

    /// <summary>At a kickoff, opponents stay at least this far from the ball (the centre circle).</summary>
    public double KickoffExclusionRadius { get; init; } = 9.15;

    /// <summary>At a throw-in, opponents stay at least this far from the thrower (Law 15).</summary>
    public double ThrowInExclusionRadius { get; init; } = 2;

    /// <summary>At a corner, opponents stay at least this far from the ball.</summary>
    public double CornerExclusionRadius { get; init; } = 9.15;

    /// <summary>Furthest teammate a thrower will throw to.</summary>
    public double ThrowInMaxRange { get; init; } = 25;
}
