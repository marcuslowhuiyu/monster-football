namespace MonsterFootball.Simulation.Core;

/// <summary>
/// A point in simulated match time. <see cref="Seconds"/> is time elapsed since the opening kickoff: it never decreases
/// and is what durations are computed from. <see cref="Period"/>, <see cref="PeriodSeconds"/> and
/// <see cref="RegularPeriodSeconds"/> give the referee's clock, displayed as MM:SS (e.g. <c>63:12</c>) or, in added
/// time, regular end + overrun (e.g. <c>45+01:30</c>). The second half's clock starts at 45:00 however much time was
/// added to the first.
/// </summary>
public readonly record struct MatchTime(double Seconds) : IComparable<MatchTime>
{
    public int Period { get; init; } = 1;

    /// <summary>Time elapsed in the current period.</summary>
    public double PeriodSeconds { get; init; } = Seconds;

    /// <summary>Regular length of a period; infinite when unknown (the clock then shows elapsed time only).</summary>
    public double RegularPeriodSeconds { get; init; } = double.PositiveInfinity;

    public int CompareTo(MatchTime other) => Seconds.CompareTo(other.Seconds);

    public override string ToString()
    {
        if (double.IsInfinity(RegularPeriodSeconds))
            return Format(Seconds);

        var periodStart = (Period - 1) * RegularPeriodSeconds;
        return PeriodSeconds <= RegularPeriodSeconds
            ? Format(periodStart + PeriodSeconds)
            : $"{(long)Math.Floor((periodStart + RegularPeriodSeconds) / 60)}+{Format(PeriodSeconds - RegularPeriodSeconds)}";
    }

    private static string Format(double seconds)
    {
        var whole = (long)Math.Floor(seconds);
        return $"{whole / 60:00}:{whole % 60:00}";
    }
}
