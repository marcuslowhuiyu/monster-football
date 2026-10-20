using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class ClearanceResolverTests
{
    private readonly ClearanceResolver _resolver = new();

    [Fact]
    public void Under_pressure_near_goal_the_clearance_goes_wide_towards_the_nearest_touchline()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 10, 20);
        s.Place(TeamSide.Away, PlayingPosition.ST, 11, 20); // 1 m away: pressure 2/3
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var (kind, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(ClearanceKind.Wide, kind);
        Assert.Equal(new Vec2(35, 0), target); // right touchline, 25 m upfield; within the 35 m range
    }

    [Fact]
    public void Wide_clearances_mirror_for_the_away_side()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Away, PlayingPosition.LCB, 95, 48); // team frame (10, 20)
        s.Place(TeamSide.Home, PlayingPosition.ST, 94, 48);
        s.GiveBall(TeamSide.Away, PlayingPosition.LCB);

        var (kind, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(ClearanceKind.Wide, kind);
        Assert.Equal(new Vec2(70, 68), target);
    }

    [Fact]
    public void Without_pressure_the_clearance_goes_long_towards_the_striker()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 60, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var (kind, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(ClearanceKind.Long, kind);
        Assert.Equal(55, target.X, precision: 9);
        Assert.Equal(34, target.Y, precision: 9);
    }

    [Fact]
    public void Far_from_goal_even_a_pressed_clearance_goes_long()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 30, 20); // 33 m from goal
        s.Place(TeamSide.Away, PlayingPosition.ST, 31, 20);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        Assert.Equal(ClearanceKind.Long, _resolver.Aim(s.State, kicker).Kind);
    }

    [Fact]
    public void A_long_clearance_goes_straight_upfield_when_the_striker_is_not_ahead()
    {
        var s = new Scenario(); // the Home ST is parked on Home's own goal line, behind the kicker
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 30);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var (_, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(new Vec2(55, 30), target);
    }

    [Fact]
    public void Stronger_players_clear_further()
    {
        double Length(int power)
        {
            var s = new Scenario(homeStats: new PlayerStats(5, 5, 5, 5, 5, power, 5, 5, 5));
            var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 30);
            s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
            return _resolver.Aim(s.State, kicker).Target.X - 20;
        }

        Assert.Equal(35 + 2 * 4, Length(9), precision: 9);
        Assert.Equal(35 - 2 * 4, Length(1), precision: 9);
    }

    [Fact]
    public void Scatter_moves_the_landing_point_within_its_bounds()
    {
        ClearanceResult Resolve(params double[] draws)
        {
            var s = new Scenario(random: new ScriptedRandom(draws));
            s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 30);
            var kicker = s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
            return _resolver.Resolve(s.State, new ClearanceIntent(kicker.PlayerId), s.Context.Random);
        }

        // Aim (55, 30): along = +X, across = +Y. Technique 5: lateral up to 4 + 0.8 × 5 = 8 m; length up to 5 m.
        var centre = Resolve(0.5, 0.5);
        Assert.Equal(new Vec2(55, 30), centre.TargetPosition);
        Assert.Equal(new Vec2(55, 30), centre.IntendedTarget);
        Assert.Equal(35 / ClearanceResolver.BallSpeed, centre.TravelDuration, precision: 9);

        var extreme = Resolve(0.0, 0.0);
        Assert.Equal(50, extreme.TargetPosition.X, precision: 9);
        Assert.Equal(22, extreme.TargetPosition.Y, precision: 9);
    }
}
