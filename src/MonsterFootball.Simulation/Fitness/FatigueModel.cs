using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Fitness;

/// <summary>
/// Every effect of stamina in one place. Current stamina runs from 0 to 100; the Stamina
/// attribute (1–10) controls how fast it drains. Fatigue penalties are deliberately small.
/// </summary>
public static class FatigueModel
{
    /// <summary>Speed lost at zero stamina.</summary>
    public const double MaxSpeedPenalty = 0.3;

    /// <summary>Action-rating effectiveness lost at zero stamina.</summary>
    public const double MaxEffectivenessPenalty = 0.15;

    /// <summary>Drain multiplier = DrainBase − DrainPerStamina × Stamina (1.24 at 1, 1.0 at 5, 0.7 at 10).</summary>
    public const double DrainBase = 1.3;
    public const double DrainPerStamina = 0.06;

    /// <summary>How tired the player is: 0 when fresh, 1 when empty.</summary>
    public static double Tiredness(double currentStamina) =>
        1 - Math.Clamp(currentStamina, PlayerMatchState.MinStamina, PlayerMatchState.MaxStamina) / PlayerMatchState.MaxStamina;

    public static double SpeedFactor(double currentStamina) => 1 - MaxSpeedPenalty * Tiredness(currentStamina);

    /// <summary>Multiplier applied to action ratings (passing, tackling, shooting, goalkeeping).</summary>
    public static double EffectivenessFactor(double currentStamina) => 1 - MaxEffectivenessPenalty * Tiredness(currentStamina);

    public static double EffectivenessFactor(PlayerMatchState player) => EffectivenessFactor(player.CurrentStamina);

    /// <summary>Higher Stamina attribute → slower drain.</summary>
    public static double DrainMultiplier(int staminaAttribute) => DrainBase - DrainPerStamina * staminaAttribute;
}
