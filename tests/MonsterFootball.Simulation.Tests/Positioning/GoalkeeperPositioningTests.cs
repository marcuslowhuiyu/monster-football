using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Tests.Positioning;

public class GoalkeeperPositioningTests
{
    private static readonly PitchGeometry Pitch = PitchGeometry.Standard;
    private static readonly Vec2 Goal = new(0, 34);

    [Fact]
    public void Keeper_never_leaves_own_penalty_area()
    {
        foreach (var inPossession in new[] { true, false })
            for (var x = 0.0; x <= 105; x += 5)
                for (var y = 0.0; y <= 68; y += 4)
                {
                    var target = GoalkeeperPositioning.Target(Pitch, new Vec2(x, y), awareness: 10, inPossession);
                    Assert.True(Pitch.IsInPenaltyArea(target, TeamSide.Home), $"ball ({x},{y}) -> {target}");
                }
    }

    [Fact]
    public void Keeper_shifts_toward_the_ball_side()
    {
        Assert.True(GoalkeeperPositioning.Target(Pitch, new Vec2(20, 60), 5, inPossession: false).Y > 34);
        Assert.True(GoalkeeperPositioning.Target(Pitch, new Vec2(20, 8), 5, inPossession: false).Y < 34);
    }

    [Fact]
    public void Keeper_stands_on_the_line_between_goal_and_ball()
    {
        var ball = new Vec2(25, 44);
        var target = GoalkeeperPositioning.Target(Pitch, ball, 7, inPossession: false);

        var toBall = ball - Goal;
        var toKeeper = target - Goal;
        var cross = toBall.X * toKeeper.Y - toBall.Y * toKeeper.X;
        Assert.Equal(0, cross, precision: 6);
    }

    [Fact]
    public void Higher_awareness_keeper_comes_further_off_the_line()
    {
        var ball = new Vec2(30, 34);
        var low = GoalkeeperPositioning.Target(Pitch, ball, awareness: 1, inPossession: false);
        var high = GoalkeeperPositioning.Target(Pitch, ball, awareness: 10, inPossession: false);

        Assert.True(high.X > low.X);
    }

    [Fact]
    public void Keeper_advances_further_when_ball_is_far_away()
    {
        var near = GoalkeeperPositioning.Target(Pitch, new Vec2(15, 34), 5, inPossession: false);
        var far = GoalkeeperPositioning.Target(Pitch, new Vec2(60, 34), 5, inPossession: false);
        Assert.True(far.X > near.X);
    }

    [Fact]
    public void In_possession_keeper_provides_deep_support()
    {
        var target = GoalkeeperPositioning.Target(Pitch, new Vec2(80, 34), 5, inPossession: true);
        Assert.InRange(target.X, 0, PitchGeometry.PenaltyAreaDepth);
    }
}
