using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Tests.Core;

public class PitchGeometryTests
{
    private readonly PitchGeometry _pitch = PitchGeometry.Standard;

    [Fact]
    public void Standard_pitch_is_105_by_68()
    {
        Assert.Equal(105, _pitch.Length);
        Assert.Equal(68, _pitch.Width);
        Assert.Equal(new Vec2(52.5, 34), _pitch.Centre);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(105, 68, true)]
    [InlineData(52.5, 34, true)]
    [InlineData(-0.1, 34, false)]
    [InlineData(52.5, 68.1, false)]
    public void Contains_respects_boundaries(double x, double y, bool expected)
    {
        Assert.Equal(expected, _pitch.Contains(new Vec2(x, y)));
    }

    [Fact]
    public void Clamp_pulls_points_onto_pitch()
    {
        Assert.Equal(new Vec2(0, 68), _pitch.Clamp(new Vec2(-5, 80)));
    }

    [Fact]
    public void Home_defends_x_zero_and_attacks_increasing_x()
    {
        Assert.Equal(new Vec2(0, 34), _pitch.OwnGoalCentre(TeamSide.Home));
        Assert.Equal(new Vec2(105, 34), _pitch.OpponentGoalCentre(TeamSide.Home));
        Assert.Equal(1, PitchGeometry.AttackDirection(TeamSide.Home));
        Assert.Equal(-1, PitchGeometry.AttackDirection(TeamSide.Away));
    }

    [Fact]
    public void Penalty_areas_are_at_each_end()
    {
        Assert.True(_pitch.IsInPenaltyArea(new Vec2(10, 34), TeamSide.Home));
        Assert.False(_pitch.IsInPenaltyArea(new Vec2(10, 34), TeamSide.Away));
        Assert.True(_pitch.IsInPenaltyArea(new Vec2(95, 34), TeamSide.Away));
        Assert.False(_pitch.IsInPenaltyArea(new Vec2(20, 34), TeamSide.Home));
        Assert.False(_pitch.IsInPenaltyArea(new Vec2(10, 5), TeamSide.Home));
    }

    [Fact]
    public void Own_half_depends_on_side()
    {
        Assert.True(_pitch.IsInOwnHalf(new Vec2(30, 34), TeamSide.Home));
        Assert.False(_pitch.IsInOwnHalf(new Vec2(30, 34), TeamSide.Away));
    }
}
