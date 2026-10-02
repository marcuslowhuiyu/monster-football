namespace MonsterFootball.Simulation.Core;

public sealed class Score
{
    public int Home { get; private set; }
    public int Away { get; private set; }

    public int For(TeamSide side) => side == TeamSide.Home ? Home : Away;

    internal void AddGoal(TeamSide scoringSide)
    {
        if (scoringSide == TeamSide.Home)
            Home++;
        else
            Away++;
    }

    public override string ToString() => $"{Home}–{Away}";
}
