using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Passing;

public enum PassOutcome
{
    Completed,
    Intercepted,
    Misplaced,
}

/// <summary>
/// The resolved outcome of a pass, decided when the ball is struck and applied when it arrives.
/// Carries the presentation data needed to animate it.
/// </summary>
public sealed record PassResult(
    PassOutcome Outcome,
    TeamSide Team,
    PlayerId Passer,
    PlayerId IntendedReceiver,
    PlayerId? Interceptor,
    Vec2 StartPosition,
    Vec2 IntendedTarget,
    Vec2 TargetPosition,
    double TravelDuration,
    double Quality)
{
    /// <summary>The player who ends up with the ball, or null if it is left loose.</summary>
    public PlayerId? FinalReceiver => Outcome switch
    {
        PassOutcome.Completed => IntendedReceiver,
        PassOutcome.Intercepted => Interceptor,
        _ => null,
    };
}

/// <summary>A pass travelling through the air between <see cref="LaunchTick"/> and <see cref="ArrivalTick"/>.</summary>
public sealed record BallFlight(PassResult Pass, long LaunchTick, long ArrivalTick)
{
    public Vec2 PositionAt(long tick)
    {
        var progress = ArrivalTick == LaunchTick ? 1 : Math.Clamp((double)(tick - LaunchTick) / (ArrivalTick - LaunchTick), 0, 1);
        return Pass.StartPosition + (Pass.TargetPosition - Pass.StartPosition) * progress;
    }
}
