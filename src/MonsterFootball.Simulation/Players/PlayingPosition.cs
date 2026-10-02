namespace MonsterFootball.Simulation.Players;

/// <summary>The 4-3-3 slots used in Milestone 1 (ADR-008).</summary>
public enum PlayingPosition
{
    GK,
    LB,
    LCB,
    RCB,
    RB,
    LCM,
    CM,
    RCM,
    LW,
    ST,
    RW,
}

public enum PositionGroup
{
    Goalkeeper,
    Defender,
    Midfielder,
    Forward,
}

public static class PlayingPositionExtensions
{
    public static PositionGroup Group(this PlayingPosition position) => position switch
    {
        PlayingPosition.GK => PositionGroup.Goalkeeper,
        PlayingPosition.LB or PlayingPosition.LCB or PlayingPosition.RCB or PlayingPosition.RB => PositionGroup.Defender,
        PlayingPosition.LCM or PlayingPosition.CM or PlayingPosition.RCM => PositionGroup.Midfielder,
        PlayingPosition.LW or PlayingPosition.ST or PlayingPosition.RW => PositionGroup.Forward,
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };

    public static bool IsGoalkeeper(this PlayingPosition position) => position == PlayingPosition.GK;
}
