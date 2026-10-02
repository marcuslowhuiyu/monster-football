namespace MonsterFootball.Simulation.Core;

/// <summary>Identifies a team within a match. Home attacks toward increasing X.</summary>
public enum TeamSide
{
    Home,
    Away,
}

public static class TeamSideExtensions
{
    public static TeamSide Opponent(this TeamSide side) =>
        side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
}
