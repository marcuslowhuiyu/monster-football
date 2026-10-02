namespace MonsterFootball.Simulation.Core;

/// <summary>A point in simulated match time, displayed as MM:SS.</summary>
public readonly record struct MatchTime(double Seconds) : IComparable<MatchTime>
{
    public int CompareTo(MatchTime other) => Seconds.CompareTo(other.Seconds);

    public override string ToString()
    {
        var whole = (long)Math.Floor(Seconds);
        return $"{whole / 60:00}:{whole % 60:00}";
    }
}
