namespace MonsterFootball.Simulation.Restarts;

/// <summary>Restart law distances, set-piece choices and setup timing, as data (<see cref="Core.MatchConfig.Restarts"/>).</summary>
public sealed record RestartTuning
{
    public static RestartTuning Default { get; } = new();

    /// <summary>A direct free kick at most this far from the goal centre may be shot, over a wall.</summary>
    public double FreeKickShootingRange { get; init; } = 32;

    /// <summary>The taker shoots a direct free kick only if its xG (wall included) is at least this.</summary>
    public double FreeKickMinXg { get; init; } = 0.04;

    /// <summary>Players in the wall for a direct free kick within shooting range.</summary>
    public int WallSize { get; init; } = 4;

    /// <summary>Once the stoppage's dead-ball time (ADR-037) is over, how long the restart waits for players to reach position before
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

    /// <summary>At a free kick, opponents stay at least this far from the ball (Law 13).</summary>
    public double FreeKickExclusionRadius { get; init; } = 9.15;

    /// <summary>Furthest teammate a thrower will throw to.</summary>
    public double ThrowInMaxRange { get; init; } = 25;
}
