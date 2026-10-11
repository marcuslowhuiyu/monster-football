using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Fitness;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Movement;

/// <summary>
/// Moves every player toward their movement target. Speed comes from Pace and current stamina.
/// There is no collision or avoidance; movement never decides football outcomes.
/// </summary>
internal sealed class MovementSystem
{
    /// <summary>Top speed in m/s = BaseSpeed + SpeedPerPace × Pace (Pace 1 → 5.4, Pace 10 → 9.0).</summary>
    public const double BaseSpeed = 5.0;
    public const double SpeedPerPace = 0.4;

    /// <summary>Jogging speed as a fraction of top speed.</summary>
    public const double JogFraction = 0.6;

    /// <summary>
    /// Arrival for <see cref="MovementIntensity.Easy"/>: within this distance of their target the player slows in
    /// proportion to the remaining distance, down to <see cref="MinArrivalFraction"/> of jogging speed, so players
    /// near their spot amble instead of jogging constantly after a target that shifts with the ball.
    /// </summary>
    public const double ArrivalRadius = 12;

    /// <summary>Smaller arrival radius for <see cref="MovementIntensity.Jog"/>: removes tick-to-tick shuffling while
    /// keeping defenders quick to cover larger shifts.</summary>
    public const double JogArrivalRadius = 4;

    public const double MinArrivalFraction = 0.25;

    public static double TopSpeed(PlayerMatchState player) =>
        (BaseSpeed + SpeedPerPace * player.Definition.Stats.Pace) * FatigueModel.SpeedFactor(player.CurrentStamina);

    /// <summary>Carrying speed as a fraction of top speed: 0.55 + 0.03 × Technique (0.58 at 1, 0.85 at 10).</summary>
    public static double CarrySpeedFraction(int technique) => 0.55 + 0.03 * technique;

    public static double CarrySpeed(PlayerMatchState player) =>
        TopSpeed(player) * CarrySpeedFraction(player.Definition.Stats.Technique);

    public static double Speed(PlayerMatchState player) => player.MovementIntensity switch
    {
        MovementIntensity.Sprint => TopSpeed(player),
        MovementIntensity.Carry => CarrySpeed(player),
        MovementIntensity.Easy => TopSpeed(player) * JogFraction * ArrivalFactor(player.Position.DistanceTo(player.MovementTarget), ArrivalRadius),
        _ => TopSpeed(player) * JogFraction * ArrivalFactor(player.Position.DistanceTo(player.MovementTarget), JogArrivalRadius),
    };

    public static double ArrivalFactor(double distanceToTarget, double radius) =>
        Math.Clamp(distanceToTarget / radius, MinArrivalFraction, 1);

    public void Apply(MatchState state, double tickSeconds)
    {
        foreach (var player in state.ActivePlayers)
        {
            var previous = player.Position;
            var next = player.Position.MoveTowards(player.MovementTarget, Speed(player) * tickSeconds);
            player.Position = state.Pitch.Clamp(next);
            player.LastTickDistance = previous.DistanceTo(player.Position);
            player.DistanceCovered += player.LastTickDistance;
        }
    }
}
