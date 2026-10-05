using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Movement;

public class MovementSystemTests
{
    private const double Tick = 0.25;

    private static PlayerStats WithPace(int pace) => new(5, 5, 5, 5, pace, 5, 5, 5, 5);

    /// <summary>Sends the home CM 50 m up the pitch and returns the distance covered in one tick.</summary>
    private static double OneTickDistance(MatchState state, MovementIntensity intensity = MovementIntensity.Jog, TeamSide side = TeamSide.Home)
    {
        var player = state.Slot(side, PlayingPosition.CM);
        var start = player.Position;
        player.MovementTarget = start + new Vec2(side == TeamSide.Home ? 20 : -20, 0);
        player.MovementIntensity = intensity;

        new MovementSystem().Apply(state, Tick);
        return start.DistanceTo(player.Position);
    }

    [Theory]
    [InlineData(1, 5.4)]
    [InlineData(5, 7.0)]
    [InlineData(10, 9.0)]
    public void Top_speed_scales_with_pace(int pace, double expected)
    {
        var state = TestTeams.Match(homeStats: WithPace(pace));
        Assert.Equal(expected, MovementSystem.TopSpeed(state.Slot(TeamSide.Home, PlayingPosition.CM)), precision: 9);
    }

    [Fact]
    public void Player_moves_speed_times_tick_toward_target()
    {
        var state = TestTeams.Match();
        var player = state.Slot(TeamSide.Home, PlayingPosition.CM);
        player.MovementTarget = player.Position + new Vec2(20, 0);
        var expected = MovementSystem.Speed(player) * Tick;
        var start = player.Position;

        new MovementSystem().Apply(state, Tick);

        Assert.Equal(expected, start.DistanceTo(player.Position), precision: 9);
    }

    [Fact]
    public void Jogging_slows_on_arrival_but_sprinting_does_not()
    {
        var state = TestTeams.Match();
        var player = state.Slot(TeamSide.Home, PlayingPosition.CM);

        player.MovementTarget = player.Position + new Vec2(1, 0);
        player.MovementIntensity = MovementIntensity.Jog;
        var nearJog = MovementSystem.Speed(player);
        player.MovementIntensity = MovementIntensity.Easy;
        var nearEasy = MovementSystem.Speed(player);
        player.MovementIntensity = MovementIntensity.Sprint;
        var nearSprint = MovementSystem.Speed(player);

        player.MovementTarget = player.Position + new Vec2(30, 0);
        player.MovementIntensity = MovementIntensity.Jog;
        var farJog = MovementSystem.Speed(player);
        player.MovementIntensity = MovementIntensity.Easy;
        var farEasy = MovementSystem.Speed(player);

        Assert.True(nearJog < farJog);
        Assert.True(nearEasy < farEasy);
        Assert.Equal(farJog, farEasy, precision: 9);           // both full jog when far away
        Assert.Equal(MovementSystem.TopSpeed(player), nearSprint, precision: 9);
        Assert.True(nearEasy <= nearJog, "the team in possession ambles more than defenders");
    }

    [Fact]
    public void Higher_pace_covers_more_ground()
    {
        var fast = OneTickDistance(TestTeams.Match(homeStats: WithPace(9)));
        var slow = OneTickDistance(TestTeams.Match(homeStats: WithPace(3)));
        Assert.True(fast > slow);
    }

    [Fact]
    public void Sprinting_is_faster_than_jogging()
    {
        var sprint = OneTickDistance(TestTeams.Match(), MovementIntensity.Sprint);
        var jog = OneTickDistance(TestTeams.Match(), MovementIntensity.Jog);
        Assert.True(sprint > jog);
    }

    [Fact]
    public void Fatigue_reduces_speed()
    {
        var fresh = TestTeams.Match();
        var tired = TestTeams.Match();
        tired.Slot(TeamSide.Home, PlayingPosition.CM).CurrentStamina = 20;

        Assert.True(OneTickDistance(tired) < OneTickDistance(fresh));
        Assert.Equal(1 - MonsterFootball.Simulation.Fitness.FatigueModel.MaxSpeedPenalty, MonsterFootball.Simulation.Fitness.FatigueModel.SpeedFactor(0), precision: 9);
        Assert.Equal(1, MonsterFootball.Simulation.Fitness.FatigueModel.SpeedFactor(PlayerMatchState.MaxStamina), precision: 9);
    }

    [Fact]
    public void Player_does_not_overshoot_target()
    {
        var state = TestTeams.Match();
        var player = state.Slot(TeamSide.Home, PlayingPosition.CM);
        var target = player.Position + new Vec2(0.1, 0);
        player.MovementTarget = target;

        new MovementSystem().Apply(state, Tick);

        Assert.Equal(target, player.Position);
    }

    [Fact]
    public void Player_is_kept_on_the_pitch()
    {
        var state = TestTeams.Match();
        var player = state.Slot(TeamSide.Home, PlayingPosition.LB);
        player.Position = new Vec2(30, 67.9);
        player.MovementTarget = new Vec2(30, 90);

        new MovementSystem().Apply(state, Tick);

        Assert.True(state.Pitch.Contains(player.Position));
    }
}
