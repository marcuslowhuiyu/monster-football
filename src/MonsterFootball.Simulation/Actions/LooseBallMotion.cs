using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Actions;

/// <summary>
/// A loose ball rolling to a stop (D4, ADR-028): straight line, constant deceleration. Position is a closed-form
/// function of time, so it never drifts with the tick length. Started after a misplaced pass lands and after a parry.
/// </summary>
public sealed record LooseBallMotion(Vec2 Start, Vec2 Direction, double Speed, long StartTick)
{
    /// <summary>Rolling deceleration on grass, m/s².</summary>
    public const double Deceleration = 3;

    /// <summary>A roll that comes to rest <paramref name="distance"/> metres from <paramref name="start"/>.</summary>
    public static LooseBallMotion Covering(Vec2 start, Vec2 direction, double distance, long startTick) =>
        new(start, direction.Normalized(), Math.Sqrt(2 * Deceleration * Math.Max(0, distance)), startTick);

    public double StopSeconds => Speed / Deceleration;

    public double RollDistance => Speed * Speed / (2 * Deceleration);

    public Vec2 PositionAt(long tick, double tickSeconds)
    {
        var t = Math.Min(Math.Max(0, (tick - StartTick) * tickSeconds), StopSeconds);
        return Start + Direction * (Speed * t - 0.5 * Deceleration * t * t);
    }

    public bool HasStoppedBy(long tick, double tickSeconds) => (tick - StartTick) * tickSeconds >= StopSeconds - 1e-9;
}
