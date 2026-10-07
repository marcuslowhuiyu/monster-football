namespace MonsterFootball.Simulation.Tactical;

/// <summary>
/// One of the 20 primary tactical zones: a band crossed with a channel. Always relative to a team, so the same
/// point is <c>Attacking / RightWide</c> for one side and <c>Defensive / LeftWide</c> for the other (ADR-027).
/// </summary>
public readonly record struct TacticalZone(LongitudinalBand Band, VerticalChannel Channel)
{
    /// <summary>All 20 zones, band by band from own goal, channels left to right.</summary>
    public static IReadOnlyList<TacticalZone> All { get; } =
        (from band in Enum.GetValues<LongitudinalBand>()
         from channel in Enum.GetValues<VerticalChannel>()
         select new TacticalZone(band, channel)).ToArray();

    public override string ToString() => $"{Band} / {Channel}";
}
