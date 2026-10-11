namespace MonsterFootball.Simulation.Players;

/// <summary>Whether a player is still taking part. A sent-off player leaves the pitch and every system ignores them.</summary>
public enum PlayerStatus
{
    Active,
    SentOff,
}
