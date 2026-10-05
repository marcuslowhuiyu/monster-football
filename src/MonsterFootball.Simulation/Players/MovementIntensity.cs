namespace MonsterFootball.Simulation.Players;

/// <summary>How a player is moving toward their target; sets speed (MovementSystem) and stamina cost per metre (StaminaSystem).</summary>
public enum MovementIntensity
{
    /// <summary>Jog that slows on arrival near the target; used by the team in possession holding position.</summary>
    Easy,

    Jog,
    Sprint,

    /// <summary>Running with the ball; speed depends on Technique.</summary>
    Carry,
}
