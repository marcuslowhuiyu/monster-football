namespace MonsterFootball.Simulation.Restarts;

/// <summary>
/// How long the ball stays dead after play stops (ADR-037), as data (<see cref="Core.MatchConfig.DeadBall"/>).
/// Averages approximate real matches; all values are provisional until the M2.7b calibration pass.
/// </summary>
public sealed record DeadBallTuning
{
    public static DeadBallTuning Default { get; } = new();

    /// <summary>Average time from the ball going out to the throw-in.</summary>
    public double ThrowInSeconds { get; init; } = 15;

    /// <summary>Average time from the ball going out to the goal kick.</summary>
    public double GoalKickSeconds { get; init; } = 25;

    /// <summary>Average time from the ball going out to the corner (players go up for it).</summary>
    public double CornerSeconds { get; init; } = 35;

    /// <summary>Average time from a foul to the direct free kick.</summary>
    public double DirectFreeKickSeconds { get; init; } = 30;

    /// <summary>Average time from an offside to the indirect free kick.</summary>
    public double IndirectFreeKickSeconds { get; init; } = 20;

    /// <summary>Average time from a goal to the kickoff (celebration, walking back).</summary>
    public double GoalSeconds { get; init; } = 60;

    /// <summary>Average time from a foul in the area to the penalty.</summary>
    public double PenaltySeconds { get; init; } = 90;

    /// <summary>Smallest multiple of the average one stoppage can take (one uniform draw between this and <see cref="SpreadMax"/>).</summary>
    public double SpreadMin { get; init; } = 0.65;

    /// <summary>Largest multiple of the average one stoppage can take.</summary>
    public double SpreadMax
    {
        get;
        init => field = value >= SpreadMin ? value : throw new ArgumentOutOfRangeException(nameof(SpreadMax), value, "Spread maximum must not be below the minimum.");
    } = 1.35;

    /// <summary>Share of regular time after which the score starts to change the pace of restarts (2/3 = minute 60).</summary>
    public double GameStateFromFraction
    {
        get;
        init => field = value is >= 0 and < 1 ? value : throw new ArgumentOutOfRangeException(nameof(GameStateFromFraction), value, "Must be in [0, 1).");
    } = 2.0 / 3;

    /// <summary>Goal margin at which the game-state effect is full.</summary>
    public double GameStateFullMargin
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(GameStateFullMargin), value, "Must be positive.");
    } = 2;

    /// <summary>At full strength, a leading side takes this much longer over its restarts (× 1 + this).</summary>
    public double MaxSlowdown { get; init; } = 0.6;

    /// <summary>At full strength, a trailing side takes this much less time over its restarts (× 1 − this).</summary>
    public double MaxHurry { get; init; } = 0.4;

    /// <summary>A free kick in the attacking third takes this many times longer (wall, players go up).</summary>
    public double AttackingFreeKickFactor { get; init; } = 1.3;

    /// <summary>A throw-in in the attacking third takes this many times longer.</summary>
    public double AttackingThrowInFactor { get; init; } = 1.2;

    /// <summary>Extra time when the foul brings a yellow card (the referee books the player).</summary>
    public double BookingSeconds { get; init; } = 30;

    /// <summary>Extra time when the foul brings a red card or second yellow (instead of <see cref="BookingSeconds"/>).</summary>
    public double DismissalSeconds { get; init; } = 45;
}
