using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Tests.Positioning;

public class Formation433Tests
{
    private static readonly PitchGeometry Pitch = PitchGeometry.Standard;

    [Theory]
    [InlineData(TeamSide.Home)]
    [InlineData(TeamSide.Away)]
    public void Every_slot_starts_on_the_pitch_in_own_half(TeamSide side)
    {
        foreach (var slot in Enum.GetValues<PlayingPosition>())
        {
            var position = Formation433.KickoffPosition(Pitch, side, slot);
            Assert.True(Pitch.Contains(position), $"{side} {slot} at {position}");
            Assert.True(Pitch.IsInOwnHalf(position, side), $"{side} {slot} at {position}");
        }
    }

    [Fact]
    public void Slots_occupy_distinct_positions()
    {
        var positions = Enum.GetValues<PlayingPosition>()
            .Select(slot => Formation433.KickoffPosition(Pitch, TeamSide.Home, slot))
            .ToList();
        Assert.Equal(positions.Count, positions.Distinct().Count());
    }

    [Fact]
    public void Away_layout_is_home_rotated_180_degrees()
    {
        foreach (var slot in Enum.GetValues<PlayingPosition>())
        {
            var home = Formation433.KickoffPosition(Pitch, TeamSide.Home, slot);
            var away = Formation433.KickoffPosition(Pitch, TeamSide.Away, slot);
            Assert.Equal(new Vec2(Pitch.Length - home.X, Pitch.Width - home.Y), away);
        }
    }

    [Fact]
    public void Goalkeeper_starts_inside_own_penalty_area()
    {
        foreach (var side in Enum.GetValues<TeamSide>())
            Assert.True(Pitch.IsInPenaltyArea(Formation433.KickoffPosition(Pitch, side, PlayingPosition.GK), side));
    }

    [Fact]
    public void Left_back_is_on_the_left_when_facing_the_attack()
    {
        // Home faces +X, so left is +Y; Away faces -X, so left is -Y.
        var homeLb = Formation433.KickoffPosition(Pitch, TeamSide.Home, PlayingPosition.LB);
        var awayLb = Formation433.KickoffPosition(Pitch, TeamSide.Away, PlayingPosition.LB);
        Assert.True(homeLb.Y > Pitch.Width / 2);
        Assert.True(awayLb.Y < Pitch.Width / 2);
    }
}
