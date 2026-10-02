using MonsterFootball.Simulation.Core;
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

    /// <summary>Speed lost at zero stamina (linear in stamina).</summary>
    public const double MaxFatigueSpeedPenalty = 0.3;

    public static double FatigueFactor(double stamina) =>
        1 - MaxFatigueSpeedPenalty * (1 - stamina / PlayerMatchState.MaxStamina);

    public static double TopSpeed(PlayerMatchState player) =>
        (BaseSpeed + SpeedPerPace * player.Definition.Stats.Pace) * FatigueFactor(player.CurrentStamina);

    public static double Speed(PlayerMatchState player) =>
        TopSpeed(player) * (player.MovementIntensity == MovementIntensity.Sprint ? 1.0 : JogFraction);

    public void Apply(MatchState state, double tickSeconds)
    {
        foreach (var player in state.Players)
        {
            var next = player.Position.MoveTowards(player.MovementTarget, Speed(player) * tickSeconds);
            player.Position = state.Pitch.Clamp(next);
        }
    }
}
