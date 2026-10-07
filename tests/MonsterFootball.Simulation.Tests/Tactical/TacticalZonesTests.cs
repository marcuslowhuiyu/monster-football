using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Tests.Tactical;

public class TacticalZonesTests
{
    private static readonly PitchGeometry Pitch = PitchGeometry.Standard;
    private readonly TacticalZones _zones = new(Pitch, ZoneBoundaries.Default);

    private static Vec2 Mirror(Vec2 p) => new(Pitch.Length - p.X, Pitch.Width - p.Y);

    [Fact]
    public void There_are_twenty_distinct_zones()
    {
        Assert.Equal(20, TacticalZone.All.Count);
        Assert.Equal(20, TacticalZone.All.Distinct().Count());
    }

    [Theory]
    [InlineData(5, 34, LongitudinalBand.Defensive, VerticalChannel.Centre)]
    [InlineData(40, 60, LongitudinalBand.OwnMidfield, VerticalChannel.LeftWide)]
    [InlineData(60, 20, LongitudinalBand.OppositionMidfield, VerticalChannel.RightHalfSpace)]
    [InlineData(100, 2, LongitudinalBand.Attacking, VerticalChannel.RightWide)]
    [InlineData(100, 45, LongitudinalBand.Attacking, VerticalChannel.LeftHalfSpace)]
    public void Home_zones_follow_the_team_frame(double x, double y, LongitudinalBand band, VerticalChannel channel)
    {
        Assert.Equal(new TacticalZone(band, channel), _zones.ZoneOf(TeamSide.Home, new Vec2(x, y)));
    }

    [Fact]
    public void The_same_point_reads_from_the_other_end_for_the_away_team()
    {
        var point = new Vec2(100, 2);

        Assert.Equal(new TacticalZone(LongitudinalBand.Attacking, VerticalChannel.RightWide), _zones.ZoneOf(TeamSide.Home, point));
        Assert.Equal(new TacticalZone(LongitudinalBand.Defensive, VerticalChannel.LeftWide), _zones.ZoneOf(TeamSide.Away, point));
    }

    [Fact]
    public void Home_and_away_mirror_exactly_across_the_pitch()
    {
        for (var x = 0.0; x <= Pitch.Length; x += 0.5)
            for (var y = 0.0; y <= Pitch.Width; y += 0.5)
            {
                var p = new Vec2(x, y);
                Assert.Equal(_zones.ZoneOf(TeamSide.Home, p), _zones.ZoneOf(TeamSide.Away, Mirror(p)));
                Assert.Equal(_zones.AreasOf(TeamSide.Home, p), _zones.AreasOf(TeamSide.Away, Mirror(p)));
            }
    }

    [Fact]
    public void Every_zone_is_reachable_and_every_point_has_a_valid_zone()
    {
        var seen = new HashSet<TacticalZone>();
        for (var x = -5.0; x <= Pitch.Length + 5; x += 1)
            for (var y = -5.0; y <= Pitch.Width + 5; y += 1)
            {
                var zone = _zones.ZoneOf(TeamSide.Home, new Vec2(x, y));
                Assert.True(Enum.IsDefined(zone.Band) && Enum.IsDefined(zone.Channel), $"invalid zone at ({x}, {y})");
                seen.Add(zone);
            }
        Assert.Equal(TacticalZone.All.ToHashSet(), seen);
    }

    [Fact]
    public void Boundary_points_belong_to_the_zone_further_forward_and_further_left()
    {
        Assert.Equal(LongitudinalBand.OwnMidfield, _zones.ZoneOf(TeamSide.Home, new Vec2(26.25, 34)).Band);
        Assert.Equal(LongitudinalBand.OppositionMidfield, _zones.ZoneOf(TeamSide.Home, new Vec2(52.5, 34)).Band);
        Assert.Equal(VerticalChannel.RightHalfSpace, _zones.ZoneOf(TeamSide.Home, new Vec2(50, 0.2 * Pitch.Width)).Channel);
        Assert.Equal(VerticalChannel.LeftWide, _zones.ZoneOf(TeamSide.Home, new Vec2(50, 68)).Channel);
    }

    [Fact]
    public void Zone_bounds_tile_the_pitch_and_contain_their_own_points()
    {
        foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
        {
            var area = 0.0;
            foreach (var zone in TacticalZone.All)
            {
                var (min, max) = _zones.BoundsOf(side, zone);
                area += (max.X - min.X) * (max.Y - min.Y);
                var centre = new Vec2((min.X + max.X) / 2, (min.Y + max.Y) / 2);
                Assert.Equal(zone, _zones.ZoneOf(side, centre));
            }
            Assert.Equal(Pitch.Length * Pitch.Width, area, 6);
        }
    }

    [Fact]
    public void Custom_boundaries_move_the_splits()
    {
        var zones = new TacticalZones(Pitch, new ZoneBoundaries { BandSplits = [0.2, 0.5, 0.8] });

        Assert.Equal(LongitudinalBand.OwnMidfield, zones.ZoneOf(TeamSide.Home, new Vec2(22, 34)).Band);
        Assert.Equal(LongitudinalBand.Defensive, _zones.ZoneOf(TeamSide.Home, new Vec2(22, 34)).Band);
    }

    [Theory]
    [InlineData(new[] { 0.5, 0.25, 0.75 })]
    [InlineData(new[] { 0.0, 0.5, 0.75 })]
    [InlineData(new[] { 0.25, 0.5, 1.0 })]
    [InlineData(new[] { 0.25, 0.5 })]
    public void Invalid_band_splits_are_rejected(double[] splits)
    {
        Assert.Throws<ArgumentException>(() => new ZoneBoundaries { BandSplits = splits });
    }

    [Fact]
    public void Halves_split_at_halfway_with_the_line_in_each_teams_own_half()
    {
        var onLine = Pitch.Centre;

        Assert.True(_zones.IsIn(TeamSide.Home, onLine, TacticalAreas.OwnHalf));
        Assert.True(_zones.IsIn(TeamSide.Away, onLine, TacticalAreas.OwnHalf));
        Assert.True(_zones.IsIn(TeamSide.Home, new Vec2(53, 34), TacticalAreas.OppositionHalf));
        Assert.False(_zones.IsIn(TeamSide.Home, new Vec2(53, 34), TacticalAreas.OwnHalf));
    }

    [Theory]
    [InlineData(69, false)]
    [InlineData(71, true)]
    public void Final_third_starts_two_thirds_up_the_pitch(double x, bool expected)
    {
        Assert.Equal(expected, _zones.IsIn(TeamSide.Home, new Vec2(x, 34), TacticalAreas.FinalThird));
        Assert.Equal(expected, _zones.IsIn(TeamSide.Away, new Vec2(Pitch.Length - x, 34), TacticalAreas.FinalThird));
    }

    [Fact]
    public void Areas_overlap()
    {
        var penaltySpot = new Vec2(Pitch.Length - 11, 34);

        Assert.Equal(
            TacticalAreas.OppositionHalf | TacticalAreas.FinalThird | TacticalAreas.OppositionPenaltyArea | TacticalAreas.CentralPenaltyArea,
            _zones.AreasOf(TeamSide.Home, penaltySpot));
        Assert.Equal(TacticalAreas.OwnHalf | TacticalAreas.OwnPenaltyArea, _zones.AreasOf(TeamSide.Away, penaltySpot));
    }

    [Theory]
    [InlineData(102, 34, true, true, false)]
    [InlineData(95, 34, false, true, false)]
    [InlineData(95, 15, false, true, true)]
    [InlineData(88.5, 13.84, false, true, true)]
    [InlineData(88.4, 34, false, false, false)]
    [InlineData(95, 13.7, false, false, false)]
    [InlineData(106, 34, false, false, false)]
    public void Penalty_and_goal_areas_include_their_lines(double x, double y, bool goalArea, bool penaltyArea, bool wide)
    {
        var areas = _zones.AreasOf(TeamSide.Home, new Vec2(x, y));

        Assert.Equal(goalArea, areas.HasFlag(TacticalAreas.OppositionGoalArea));
        Assert.Equal(penaltyArea, areas.HasFlag(TacticalAreas.OppositionPenaltyArea));
        Assert.Equal(penaltyArea && wide, areas.HasFlag(TacticalAreas.WidePenaltyArea));
        Assert.Equal(penaltyArea && !wide, areas.HasFlag(TacticalAreas.CentralPenaltyArea));
    }

    [Fact]
    public void Penalty_areas_agree_with_pitch_geometry()
    {
        for (var x = -1.0; x <= Pitch.Length + 1; x += 0.5)
            for (var y = -1.0; y <= Pitch.Width + 1; y += 0.5)
            {
                var p = new Vec2(x, y);
                Assert.Equal(Pitch.IsInPenaltyArea(p, TeamSide.Away), _zones.IsIn(TeamSide.Home, p, TacticalAreas.OppositionPenaltyArea));
                Assert.Equal(Pitch.IsInPenaltyArea(p, TeamSide.Home), _zones.IsIn(TeamSide.Home, p, TacticalAreas.OwnPenaltyArea));
            }
    }

    [Fact]
    public void Match_state_exposes_zones_built_from_the_config()
    {
        var config = MatchConfig.Default with { Zones = new ZoneBoundaries { ChannelSplits = [0.1, 0.3, 0.7, 0.9] } };
        var state = new MatchSimulation(config, TestSupport.TestTeams.Team("Blue", 1), TestSupport.TestTeams.Team("Red", 101), 1).State;

        Assert.Same(config.Zones, state.Zones.Boundaries);
        Assert.Equal(VerticalChannel.Centre, state.Zones.ZoneOf(TeamSide.Home, new Vec2(50, 25)).Channel);
    }
}
