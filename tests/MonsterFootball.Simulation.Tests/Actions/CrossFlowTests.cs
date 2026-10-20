using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Crossing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Observation;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Statistics;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class CrossFlowTests
{
    private static CrossResult Strike(Scenario s, PlayingPosition slot, CrossKind kind = CrossKind.OpenPlay)
    {
        var crosser = s.GiveBall(TeamSide.Home, slot);
        var result = new CrossResolver().Resolve(s.State, new CrossIntent(crosser.PlayerId, kind), s.Context.Random);
        CrossExecution.Launch(s.Context, result);
        return result;
    }

    private static void FlyCross(Scenario s)
    {
        var flight = new BallFlightSystem();
        while (s.State.Ball.Flight?.Cross is not null && s.State.Phase == MatchPhase.OpenPlay)
        {
            s.State.Clock.Advance();
            flight.Apply(s.Context);
            MatchStateInvariants.Verify(s.State);
        }
    }

    private static string Outcome(Scenario s) =>
        s.Log.Events.Single(e => e.Type == MatchEventType.CrossContested).Metadata["outcome"];

    [Fact]
    public void An_attacker_winning_the_cross_takes_a_first_time_shot()
    {
        // Draws: cross scatter (centre), then the shot's own draws.
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.0, 0.99, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        var striker = s.Place(TeamSide.Home, PlayingPosition.ST, 94, 34); // the only attacker in the box
        var result = Strike(s, PlayingPosition.LW);

        var frame = MatchFrame.Capture(s.State);
        Assert.Equal(BallFlightKind.Cross, frame.Ball.Flight!.Kind);

        FlyCross(s);

        Assert.Equal("Shot", Outcome(s));
        var shot = s.Log.Events.Single(e => e.Type == MatchEventType.ShotTaken);
        Assert.Equal(striker.PlayerId, shot.ActorPlayerId);
        Assert.Equal("true", shot.Metadata["cross"]);
        var stats = MatchStatistics.FromEvents(s.Log.Events).Home;
        Assert.Equal(1, stats.Crosses);
        Assert.Equal(1, stats.ShotsFromCrosses);
        Assert.Equal(TeamSide.Home, s.Log.Events.Single(e => e.Type == MatchEventType.Cross).Team);
        _ = result;
    }

    [Fact]
    public void A_defender_winning_the_cross_can_head_it_behind_for_a_corner()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.1)); // header behind: 0.1 < 0.25
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        s.Place(TeamSide.Away, PlayingPosition.LCB, 94, 35); // alone at the penalty spot
        Strike(s, PlayingPosition.LW);

        FlyCross(s);

        Assert.Equal("Cleared", Outcome(s));
        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(RestartType.Corner, stoppage.Restart);
        Assert.Equal(TeamSide.Home, stoppage.AwardedTeam);
    }

    [Fact]
    public void Otherwise_the_defender_heads_it_away_from_goal()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.9, 0.5)); // not behind; 20 m away
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        var defender = s.Place(TeamSide.Away, PlayingPosition.LCB, 94, 35);
        Strike(s, PlayingPosition.LW);

        FlyCross(s);

        Assert.True(s.State.Ball.IsLoose);
        Assert.NotNull(s.State.Ball.Roll);
        Assert.True(s.State.Ball.Roll!.Direction.X < 0); // away from the Away goal at x = 105
        Assert.Equal(new BallTouch(defender.PlayerId, TeamSide.Away), s.State.LastTouch);
    }

    [Fact]
    public void The_keeper_claims_a_cross_into_the_goal_area()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LW, 100, 64);
        s.Place(TeamSide.Home, PlayingPosition.ST, 102, 34);
        var keeper = s.Place(TeamSide.Away, PlayingPosition.GK, 103, 34);
        Strike(s, PlayingPosition.LW);

        FlyCross(s);

        Assert.Equal("Claimed", Outcome(s));
        Assert.True(s.State.HasBall(keeper.PlayerId));
    }

    [Fact]
    public void Nobody_near_the_landing_point_leaves_the_ball_loose()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        Strike(s, PlayingPosition.LW);

        FlyCross(s);

        Assert.Equal("Loose", Outcome(s));
        Assert.True(s.State.Ball.IsLoose);
    }
}
