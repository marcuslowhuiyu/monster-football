using MonsterFootball.Simulation.Actions.Crossing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class AerialContestTests
{
    [Fact]
    public void The_keeper_claims_a_cross_into_the_goal_area_within_reach()
    {
        var s = new Scenario(random: new ScriptedRandom());
        var keeper = s.Place(TeamSide.Away, PlayingPosition.GK, 103, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 102, 35);

        var (outcome, winner) = AerialContest.Decide(s.State, TeamSide.Home, new Vec2(102, 34), s.Context.Random);

        Assert.Equal(AerialOutcome.Claimed, outcome);
        Assert.Same(keeper, winner);
    }

    [Fact]
    public void Outside_the_goal_area_the_keeper_does_not_claim()
    {
        var s = new Scenario(random: new ScriptedRandom());
        s.Place(TeamSide.Away, PlayingPosition.GK, 96, 34);

        Assert.Equal(AerialOutcome.Loose, AerialContest.Decide(s.State, TeamSide.Home, new Vec2(95, 34), s.Context.Random).Outcome);
    }

    [Fact]
    public void A_lone_attacker_or_defender_wins_without_a_draw()
    {
        var attacking = new Scenario(random: new ScriptedRandom());
        var striker = attacking.Place(TeamSide.Home, PlayingPosition.ST, 95, 35);
        Assert.Equal((AerialOutcome.AttackerWins, striker), AerialContest.Decide(attacking.State, TeamSide.Home, new Vec2(95, 34), attacking.Context.Random));

        var defending = new Scenario(random: new ScriptedRandom());
        var centreBack = defending.Place(TeamSide.Away, PlayingPosition.LCB, 95, 35);
        Assert.Equal((AerialOutcome.DefenderWins, centreBack), AerialContest.Decide(defending.State, TeamSide.Home, new Vec2(95, 34), defending.Context.Random));
    }

    [Theory]
    [InlineData(0.0, AerialOutcome.AttackerWins)]
    [InlineData(0.99, AerialOutcome.DefenderWins)]
    public void Attacker_and_defender_contest_it_with_one_draw(double draw, AerialOutcome expected)
    {
        var s = new Scenario(random: new ScriptedRandom(draw));
        s.Place(TeamSide.Home, PlayingPosition.ST, 95, 35);
        s.Place(TeamSide.Away, PlayingPosition.LCB, 95, 33);

        Assert.Equal(expected, AerialContest.Decide(s.State, TeamSide.Home, new Vec2(95, 34), s.Context.Random).Outcome);
    }

    [Fact]
    public void Equal_players_give_the_defender_a_clear_edge_and_ratings_matter()
    {
        var s = new Scenario();
        var attacker = s.State.Slot(TeamSide.Home, PlayingPosition.ST);
        var defender = s.State.Slot(TeamSide.Away, PlayingPosition.LCB);
        Assert.InRange(AerialContest.AttackerWinChance(attacker, defender), 0.21, 0.23);

        var strong = new Scenario(homeStats: new PlayerStats(5, 5, 5, 5, 5, 9, 5, 5, 5));
        Assert.True(AerialContest.AttackerWinChance(strong.State.Slot(TeamSide.Home, PlayingPosition.ST), strong.State.Slot(TeamSide.Away, PlayingPosition.LCB)) > 0.5);
    }
}
