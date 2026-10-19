using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Tests.Positioning;

public class WideChannelTests
{
    private static readonly PitchGeometry Pitch = MatchConfig.Default.Pitch;

    [Theory]
    [InlineData(PlayingPosition.LW, true)]
    [InlineData(PlayingPosition.RW, true)]
    [InlineData(PlayingPosition.LB, true)]
    [InlineData(PlayingPosition.RB, true)]
    [InlineData(PlayingPosition.GK, false)]
    [InlineData(PlayingPosition.LCB, false)]
    [InlineData(PlayingPosition.RCB, false)]
    [InlineData(PlayingPosition.LCM, false)]
    [InlineData(PlayingPosition.CM, false)]
    [InlineData(PlayingPosition.RCM, false)]
    [InlineData(PlayingPosition.ST, false)]
    public void Wingers_and_full_backs_are_wide(PlayingPosition role, bool wide) =>
        Assert.Equal(wide, WideChannel.IsWide(role));

    [Fact]
    public void Left_roles_belong_to_the_left_touchline_and_right_roles_to_the_right()
    {
        Assert.Equal(Pitch.Width, WideChannel.TouchlineY(Pitch, PlayingPosition.LW));
        Assert.Equal(Pitch.Width, WideChannel.TouchlineY(Pitch, PlayingPosition.LB));
        Assert.Equal(0, WideChannel.TouchlineY(Pitch, PlayingPosition.RW));
        Assert.Equal(0, WideChannel.TouchlineY(Pitch, PlayingPosition.RB));
    }

    [Fact]
    public void Touchline_of_a_central_role_is_undefined() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WideChannel.TouchlineY(Pitch, PlayingPosition.CM));

    [Fact]
    public void Clamp_pulls_an_infield_point_into_the_channel_and_keeps_x()
    {
        Assert.Equal(new Vec2(50, 60), WideChannel.Clamp(Pitch, PlayingPosition.LW, new Vec2(50, 40), 8));
        Assert.Equal(new Vec2(50, 8), WideChannel.Clamp(Pitch, PlayingPosition.RW, new Vec2(50, 40), 8));
        Assert.Equal(new Vec2(30, 56), WideChannel.Clamp(Pitch, PlayingPosition.LB, new Vec2(30, 34), 12));
        Assert.Equal(new Vec2(30, 12), WideChannel.Clamp(Pitch, PlayingPosition.RB, new Vec2(30, 34), 12));
    }

    [Fact]
    public void Clamp_leaves_a_point_already_in_the_channel()
    {
        Assert.Equal(new Vec2(50, 65), WideChannel.Clamp(Pitch, PlayingPosition.LW, new Vec2(50, 65), 8));
        Assert.Equal(new Vec2(50, 3), WideChannel.Clamp(Pitch, PlayingPosition.RW, new Vec2(50, 3), 8));
    }

    [Fact]
    public void Clamp_leaves_central_roles_unchanged() =>
        Assert.Equal(new Vec2(50, 40), WideChannel.Clamp(Pitch, PlayingPosition.CM, new Vec2(50, 40), 8));
}
