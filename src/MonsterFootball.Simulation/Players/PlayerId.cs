namespace MonsterFootball.Simulation.Players;

public readonly record struct PlayerId(int Value)
{
    public override string ToString() => $"P{Value}";
}
