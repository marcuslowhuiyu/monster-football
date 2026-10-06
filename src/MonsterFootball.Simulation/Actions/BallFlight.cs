using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Actions;

/// <summary>
/// A pass or a shot travelling through the air between <see cref="LaunchTick"/> and <see cref="ArrivalTick"/>.
/// Its outcome was decided when the ball was struck and is applied on arrival (ADR-012, ADR-022).
/// Exactly one of <see cref="Pass"/> and <see cref="Shot"/> is set.
/// </summary>
public sealed record BallFlight
{
    private BallFlight(PassResult? pass, ShotResult? shot, long launchTick, long arrivalTick)
    {
        Pass = pass;
        Shot = shot;
        LaunchTick = launchTick;
        ArrivalTick = arrivalTick;
    }

    public static BallFlight ForPass(PassResult pass, long launchTick, long arrivalTick) => new(pass, null, launchTick, arrivalTick);

    public static BallFlight ForShot(ShotResult shot, long launchTick, long arrivalTick) => new(null, shot, launchTick, arrivalTick);

    public PassResult? Pass { get; }
    public ShotResult? Shot { get; }
    public long LaunchTick { get; }
    public long ArrivalTick { get; }

    /// <summary>The team that struck the ball.</summary>
    public TeamSide Team => Pass?.Team ?? Shot!.Team;

    public Vec2 StartPosition => Pass?.StartPosition ?? Shot!.StartPosition;

    /// <summary>Where the ball arrives (the actual landing point, not necessarily the intended one).</summary>
    public Vec2 TargetPosition => Pass?.TargetPosition ?? Shot!.TargetPosition;

    public Vec2 PositionAt(long tick)
    {
        var progress = ArrivalTick == LaunchTick ? 1 : Math.Clamp((double)(tick - LaunchTick) / (ArrivalTick - LaunchTick), 0, 1);
        return StartPosition + (TargetPosition - StartPosition) * progress;
    }

    /// <summary>Whole ticks a ball takes to travel for <paramref name="travelSeconds"/>; at least one.</summary>
    internal static long TravelTicks(double travelSeconds, double tickSeconds) =>
        Math.Max(1, (long)Math.Ceiling(travelSeconds / tickSeconds - 1e-9));
}
