namespace MonsterFootball.Simulation.Tactical;

/// <summary>
/// Where the bands and channels split, as fractions of pitch length (from the analysing team's own goal line) and
/// width (from its right touchline). The single place to tune zone geometry; the default is equal quarters and
/// fifths (MILESTONE_2 §20).
/// </summary>
public sealed record ZoneBoundaries
{
    public static ZoneBoundaries Default { get; } = new();

    /// <summary>Three increasing fractions in (0, 1) separating the four bands.</summary>
    public IReadOnlyList<double> BandSplits
    {
        get;
        init => field = Validate(value, Enum.GetValues<LongitudinalBand>().Length - 1, nameof(BandSplits));
    } = [0.25, 0.5, 0.75];

    /// <summary>Four increasing fractions in (0, 1) separating the five channels, measured from the right touchline.</summary>
    public IReadOnlyList<double> ChannelSplits
    {
        get;
        init => field = Validate(value, Enum.GetValues<VerticalChannel>().Length - 1, nameof(ChannelSplits));
    } = [0.2, 0.4, 0.6, 0.8];

    private static IReadOnlyList<double> Validate(IReadOnlyList<double> splits, int count, string name)
    {
        ArgumentNullException.ThrowIfNull(splits, name);
        if (splits.Count != count)
            throw new ArgumentException($"Expected {count} splits, got {splits.Count}.", name);
        for (var i = 0; i < splits.Count; i++)
        {
            var low = i == 0 ? 0 : splits[i - 1];
            if (!(splits[i] > low && splits[i] < 1))
                throw new ArgumentException($"Splits must increase strictly within (0, 1); got {string.Join(", ", splits)}.", name);
        }
        return splits.ToArray();
    }
}
