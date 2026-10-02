namespace MonsterFootball.Simulation.Players;

/// <summary>How a player is moving toward their target. Stamina cost by intensity arrives in M1.8.</summary>
public enum MovementIntensity
{
    Jog,
    Sprint,

    /// <summary>Running with the ball; speed depends on Technique.</summary>
    Carry,
}
