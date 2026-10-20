using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Tactical;

/// <summary>
/// Classifies continuous positions into tactical zones and areas for one pitch. Pure, deterministic and read-only:
/// positions are never snapped to the grid, and every point (including one off the pitch) has exactly one zone.
/// Work happens in the analysing team's frame (own goal at X = 0, left flank at +Y), so Home and Away share one
/// implementation and mirror exactly (ADR-027).
/// </summary>
public sealed class TacticalZones
{
    private readonly double[] _bandX;
    private readonly double[] _channelY;

    public TacticalZones(PitchGeometry pitch, ZoneBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        ArgumentNullException.ThrowIfNull(boundaries);
        Pitch = pitch;
        Boundaries = boundaries;
        _bandX = boundaries.BandSplits.Select(f => f * pitch.Length).ToArray();
        _channelY = boundaries.ChannelSplits.Select(f => f * pitch.Width).ToArray();
    }

    public PitchGeometry Pitch { get; }
    public ZoneBoundaries Boundaries { get; }

    /// <summary>X of the final-third line in team frame.</summary>
    public double FinalThirdX => Pitch.Length * 2 / 3;

    /// <summary>X of the defensive-third line in team frame: the end of a team's own third.</summary>
    public double DefensiveThirdX => Pitch.Length / 3;

    /// <summary>
    /// The zone containing <paramref name="point"/> (pitch coordinates) for <paramref name="side"/>. A point on a
    /// boundary belongs to the zone further from the team's own goal (bands) or further left (channels); points off
    /// the pitch take the zone of the nearest point on it.
    /// </summary>
    public TacticalZone ZoneOf(TeamSide side, Vec2 point)
    {
        var local = TeamFrame.Convert(Pitch, side, point);
        var band = CountAtOrBelow(_bandX, local.X);
        var fromRight = CountAtOrBelow(_channelY, local.Y);
        return new TacticalZone((LongitudinalBand)band, (VerticalChannel)(_channelY.Length - fromRight));
    }

    /// <summary>Every overlapping area containing <paramref name="point"/> (pitch coordinates) for <paramref name="side"/>.</summary>
    public TacticalAreas AreasOf(TeamSide side, Vec2 point)
    {
        var local = TeamFrame.Convert(Pitch, side, point);
        var areas = local.X <= Pitch.HalfwayX ? TacticalAreas.OwnHalf : TacticalAreas.OppositionHalf;
        if (local.X >= FinalThirdX)
            areas |= TacticalAreas.FinalThird;

        var lateral = Math.Abs(local.Y - Pitch.Width / 2);
        var onPitchWidth = local.Y >= 0 && local.Y <= Pitch.Width;
        var fromOwnLine = local.X;
        var fromOppositionLine = Pitch.Length - local.X;

        if (onPitchWidth && lateral <= PitchGeometry.PenaltyAreaWidth / 2)
        {
            if (fromOwnLine >= 0 && fromOwnLine <= PitchGeometry.PenaltyAreaDepth)
                areas |= TacticalAreas.OwnPenaltyArea;
            if (fromOppositionLine >= 0 && fromOppositionLine <= PitchGeometry.PenaltyAreaDepth)
                areas |= TacticalAreas.OppositionPenaltyArea
                         | (lateral <= PitchGeometry.GoalAreaWidth / 2 ? TacticalAreas.CentralPenaltyArea : TacticalAreas.WidePenaltyArea);
        }

        if (onPitchWidth && lateral <= PitchGeometry.GoalAreaWidth / 2)
        {
            if (fromOwnLine >= 0 && fromOwnLine <= PitchGeometry.GoalAreaDepth)
                areas |= TacticalAreas.OwnGoalArea;
            if (fromOppositionLine >= 0 && fromOppositionLine <= PitchGeometry.GoalAreaDepth)
                areas |= TacticalAreas.OppositionGoalArea;
        }

        return areas;
    }

    public bool IsIn(TeamSide side, Vec2 point, TacticalAreas area) => (AreasOf(side, point) & area) == area;

    /// <summary>
    /// The rectangle covered by <paramref name="zone"/> for <paramref name="side"/>, in pitch coordinates
    /// (min and max corners). For drawing overlays.
    /// </summary>
    public (Vec2 Min, Vec2 Max) BoundsOf(TeamSide side, TacticalZone zone)
    {
        var band = (int)zone.Band;
        var fromRight = _channelY.Length - (int)zone.Channel;
        var x0 = band == 0 ? 0 : _bandX[band - 1];
        var x1 = band == _bandX.Length ? Pitch.Length : _bandX[band];
        var y0 = fromRight == 0 ? 0 : _channelY[fromRight - 1];
        var y1 = fromRight == _channelY.Length ? Pitch.Width : _channelY[fromRight];
        var a = TeamFrame.Convert(Pitch, side, new Vec2(x0, y0));
        var b = TeamFrame.Convert(Pitch, side, new Vec2(x1, y1));
        return (new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    private static int CountAtOrBelow(double[] splits, double value)
    {
        var count = 0;
        while (count < splits.Length && value >= splits[count])
            count++;
        return count;
    }
}
