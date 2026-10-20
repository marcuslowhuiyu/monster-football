using MonsterFootball.Simulation.Actions.Crossing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class CrossResolverTests
{
    private readonly CrossResolver _resolver = new();

    [Fact]
    public void The_cross_aims_at_the_attacker_in_the_box_with_the_most_space()
    {
        var s = new Scenario();
        var crosser = s.Place(TeamSide.Home, PlayingPosition.LW, 95, 60);
        s.Place(TeamSide.Home, PlayingPosition.ST, 95, 34);   // ≈ 9.2 m from the nearest parked Away player
        s.Place(TeamSide.Home, PlayingPosition.LCM, 92, 40);  // ≈ 14.4 m: more space
        s.GiveBall(TeamSide.Home, PlayingPosition.LW);

        Assert.Equal(new Vec2(92, 40), _resolver.Aim(s.State, crosser));
    }

    [Fact]
    public void Without_an_attacker_in_the_box_the_cross_aims_at_the_penalty_spot()
    {
        var s = new Scenario();
        var crosser = s.Place(TeamSide.Home, PlayingPosition.LW, 95, 60);
        s.GiveBall(TeamSide.Home, PlayingPosition.LW);

        Assert.Equal(new Vec2(94, 34), _resolver.Aim(s.State, crosser));
    }

    [Fact]
    public void Crosses_mirror_for_the_away_side()
    {
        var s = new Scenario();
        var crosser = s.Place(TeamSide.Away, PlayingPosition.LW, 10, 8);   // team frame (95, 60)
        s.Place(TeamSide.Away, PlayingPosition.ST, 10, 34);                // ≈ 9.2 m from the nearest parked Home player
        s.Place(TeamSide.Away, PlayingPosition.LCM, 13, 28);               // ≈ 12 m: more space
        s.GiveBall(TeamSide.Away, PlayingPosition.LW);

        Assert.Equal(new Vec2(13, 28), _resolver.Aim(s.State, crosser));
    }

    [Fact]
    public void Scatter_moves_the_landing_point_within_its_bounds()
    {
        CrossResult Resolve(params double[] draws)
        {
            var s = new Scenario(random: new ScriptedRandom(draws));
            s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
            var crosser = s.GiveBall(TeamSide.Home, PlayingPosition.LW);
            return _resolver.Resolve(s.State, new CrossIntent(crosser.PlayerId), s.Context.Random);
        }

        // Aim: the penalty spot (94, 34), straight across: along = (0, −1), across = (1, 0).
        // Passing 5: sideways up to 3 + 0.5 × 5 = 5.5 m; lengthways up to 3 m.
        var centre = Resolve(0.5, 0.5);
        Assert.Equal(new Vec2(94, 34), centre.TargetPosition);
        Assert.Equal(CrossKind.OpenPlay, centre.Kind);
        Assert.Equal(30 / CrossResolver.BallSpeed, centre.TravelDuration, precision: 9);

        var extreme = Resolve(0.0, 0.0);
        Assert.Equal(88.5, extreme.TargetPosition.X, precision: 9);
        Assert.Equal(37, extreme.TargetPosition.Y, precision: 9);
    }
}
