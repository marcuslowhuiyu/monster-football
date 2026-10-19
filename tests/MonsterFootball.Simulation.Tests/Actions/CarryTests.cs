using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class CarryTests
{
    private readonly CarryResolver _resolver = new();

    private static Scenario CarrierAt(double x, double y, PlayerStats? stats = null, TeamSide side = TeamSide.Home)
    {
        var s = side == TeamSide.Home ? new Scenario(homeStats: stats) : new Scenario(awayStats: stats);
        s.Place(side, PlayingPosition.CM, x, y);
        s.GiveBall(side, PlayingPosition.CM);
        return s;
    }

    private static PlayerMatchState Carrier(Scenario s, TeamSide side = TeamSide.Home) => s.State.Slot(side, PlayingPosition.CM);

    [Fact]
    public void Carry_destination_progresses_toward_goal()
    {
        var home = CarrierAt(40, 34);
        Assert.True(_resolver.ChooseDestination(home.State, Carrier(home)).X > 40);

        var away = CarrierAt(60, 34, side: TeamSide.Away);
        Assert.True(_resolver.ChooseDestination(away.State, Carrier(away, TeamSide.Away)).X < 60);
    }

    [Fact]
    public void Carry_is_limited_to_one_segment_and_stays_on_the_pitch()
    {
        var s = CarrierAt(100, 66);
        var result = _resolver.Resolve(s.State, new CarryIntent(Carrier(s).PlayerId, new Vec2(200, 200)));

        Assert.True(result.StartPosition.DistanceTo(result.TargetPosition) <= CarryResolver.SegmentLength + 1e-9);
        Assert.True(s.State.Pitch.Contains(result.TargetPosition));
    }

    [Fact]
    public void Aware_carriers_steer_further_away_from_a_nearby_defender()
    {
        double Deviation(int awareness)
        {
            var s = CarrierAt(40, 34, new PlayerStats(5, 5, 5, 5, 5, 5, 5, awareness, 5));
            s.Place(TeamSide.Away, PlayingPosition.CM, 44, 36); // ahead and to the left
            return 34 - _resolver.ChooseDestination(s.State, Carrier(s)).Y; // positive = steered right, away
        }

        Assert.True(Deviation(9) > Deviation(2));
        Assert.True(Deviation(2) > 0);
    }

    [Fact]
    public void Technique_and_pace_make_carries_faster()
    {
        double Speed(PlayerStats stats)
        {
            var s = CarrierAt(40, 34, stats);
            return _resolver.Resolve(s.State, new CarryIntent(Carrier(s).PlayerId, new Vec2(50, 34))).Speed;
        }

        var baseline = Speed(PlayerStats.Uniform(5));
        Assert.True(Speed(new PlayerStats(9, 5, 5, 5, 5, 5, 5, 5, 5)) > baseline);
        Assert.True(Speed(new PlayerStats(5, 5, 5, 5, 9, 5, 5, 5, 5)) > baseline);
    }

    [Fact]
    public void Carrying_is_slower_than_sprinting()
    {
        var s = CarrierAt(40, 34);
        Assert.True(MovementSystem.CarrySpeed(Carrier(s)) < MovementSystem.TopSpeed(Carrier(s)));
    }

    [Fact]
    public void Cannot_carry_without_the_ball()
    {
        var s = new Scenario();
        var player = s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(s.State, new CarryIntent(player.PlayerId, new Vec2(50, 34))));
    }

    [Fact]
    public void Carry_moves_the_carrier_with_the_ball_and_keeps_possession_until_it_ends()
    {
        var s = CarrierAt(40, 34);
        var carrier = Carrier(s);
        var intent = new CarryIntent(carrier.PlayerId, new Vec2(50, 34));
        var result = _resolver.Resolve(s.State, intent);

        CarrySystem.Start(s.Context, intent, result);
        Assert.Equal(MatchEventType.CarryStarted, s.EventTypes[^1]);
        Assert.Same(intent, carrier.CurrentIntent);

        var shape = new TeamShapeSystem();
        var movement = new MovementSystem();
        var carry = new CarrySystem();
        var previousX = carrier.Position.X;

        while (carrier.ActiveCarry is not null)
        {
            shape.Apply(s.State);
            movement.Apply(s.State, MatchConfig.Default.TickSeconds);
            s.State.SyncBallToPossessor();
            s.State.Clock.Advance();
            carry.Apply(s.Context);

            Assert.True(s.State.HasBall(carrier.PlayerId));
            Assert.Equal(carrier.Position, s.State.Ball.Position);
            Assert.True(carrier.Position.X >= previousX);
            previousX = carrier.Position.X;
            MatchStateInvariants.Verify(s.State);
        }

        Assert.True(carrier.Position.X > 45, $"carrier only reached {carrier.Position}");
        Assert.Null(carrier.CurrentIntent);
    }

    [Fact]
    public void Losing_the_ball_ends_the_carry()
    {
        var s = CarrierAt(40, 34);
        var carrier = Carrier(s);
        var intent = new CarryIntent(carrier.PlayerId, new Vec2(50, 34));
        CarrySystem.Start(s.Context, intent, _resolver.Resolve(s.State, intent));

        s.State.SetBallLoose(new Vec2(41, 34));

        Assert.Null(carrier.ActiveCarry);
        MatchStateInvariants.Verify(s.State);
    }

    private static (Scenario Scenario, PlayerMatchState Carrier) WithBall(TeamSide side, PlayingPosition slot, double x, double y)
    {
        var s = new Scenario();
        var carrier = s.Place(side, slot, x, y);
        s.GiveBall(side, slot);
        return (s, carrier);
    }

    [Fact]
    public void Wide_players_carry_straight_down_the_line_outside_the_final_third()
    {
        var (home, homeWinger) = WithBall(TeamSide.Home, PlayingPosition.LW, 30, 64);
        Assert.Equal(64, _resolver.ChooseDestination(home.State, homeWinger).Y, precision: 9);

        // Away: team-frame (30, 64) is pitch (75, 4).
        var (away, awayWinger) = WithBall(TeamSide.Away, PlayingPosition.LW, 75, 4);
        Assert.Equal(4, _resolver.ChooseDestination(away.State, awayWinger).Y, precision: 9);
    }

    [Fact]
    public void Central_players_still_drift_towards_the_middle()
    {
        var (s, midfielder) = WithBall(TeamSide.Home, PlayingPosition.LCM, 30, 50);
        Assert.True(_resolver.ChooseDestination(s.State, midfielder).Y < 50);
    }

    [Fact]
    public void Wide_players_bend_towards_goal_in_the_final_third()
    {
        var (s, winger) = WithBall(TeamSide.Home, PlayingPosition.LW, 80, 60);
        Assert.True(_resolver.ChooseDestination(s.State, winger).Y < 60);
    }
}
