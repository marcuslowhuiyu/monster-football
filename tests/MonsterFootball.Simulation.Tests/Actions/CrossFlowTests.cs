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
    private static CrossResult Strike(Scenario s, PlayingPosition slot, CrossKind kind = CrossKind.OpenPlay, TeamSide side = TeamSide.Home)
    {
        var crosser = s.GiveBall(side, slot);
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
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.05)); // header behind: 0.05 < 0.12
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

    [Fact]
    public void An_attacker_winning_the_cross_from_an_offside_position_stops_play_without_a_shot()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        var line = 0;
        foreach (var away in s.State.TeamPlayers(TeamSide.Away).Where(p => !p.Role.IsGoalkeeper()))
            away.Position = new Vec2(90, 6 + 5 * line++); // a line at x = 90, well away from the landing point
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        var striker = s.Place(TeamSide.Home, PlayingPosition.ST, 97, 34); // beyond the line: offside at the strike
        Strike(s, PlayingPosition.LW);

        FlyCross(s);

        Assert.Equal("Shot", Outcome(s));
        Assert.DoesNotContain(MatchEventType.ShotTaken, s.EventTypes);
        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(StoppageReason.Offside, stoppage.Reason);
        Assert.Equal(striker.PlayerId, stoppage.Offender);
    }

    [Fact]
    public void A_cross_landing_beyond_the_goal_line_goes_out_for_a_goal_kick()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.9999)); // no sideways scatter; the full 3 m long
        s.Place(TeamSide.Home, PlayingPosition.LW, 90, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 103.5, 34); // the target: lands at x ≈ 106.5, over the line
        Strike(s, PlayingPosition.LW);

        FlyCross(s);

        Assert.DoesNotContain(MatchEventType.CrossContested, s.EventTypes);
        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(RestartType.GoalKick, stoppage.Restart);
        Assert.Equal(TeamSide.Away, stoppage.AwardedTeam);
        Assert.True(stoppage.Location.X >= 105 - PitchGeometry.GoalAreaDepth); // the kick is taken from the Away goal area
    }

    [Fact]
    public void The_home_keeper_claims_an_away_cross_into_the_home_goal_area()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Away, PlayingPosition.LW, 11, 64);
        s.Place(TeamSide.Away, PlayingPosition.ST, 3, 34);
        var keeper = s.Place(TeamSide.Home, PlayingPosition.GK, 2, 34);
        Strike(s, PlayingPosition.LW, side: TeamSide.Away);

        FlyCross(s);

        Assert.Equal("Claimed", Outcome(s));
        Assert.True(s.State.HasBall(keeper.PlayerId));
    }

    [Fact]
    public void A_home_defender_winning_an_away_cross_heads_it_away_from_the_home_goal()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.9, 0.5));
        s.Place(TeamSide.Away, PlayingPosition.LW, 11, 64);
        var defender = s.Place(TeamSide.Home, PlayingPosition.LCB, 11, 35); // alone at the penalty spot
        Strike(s, PlayingPosition.LW, side: TeamSide.Away);

        FlyCross(s);

        Assert.Equal("Cleared", Outcome(s));
        Assert.NotNull(s.State.Ball.Roll);
        Assert.True(s.State.Ball.Roll!.Direction.X > 0); // away from the Home goal at x = 0
        Assert.Equal(new BallTouch(defender.PlayerId, TeamSide.Home), s.State.LastTouch);
    }

    [Fact]
    public void A_home_defender_heading_an_away_cross_behind_concedes_a_corner_on_the_home_goal_line()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.05));
        s.Place(TeamSide.Away, PlayingPosition.LW, 11, 64);
        s.Place(TeamSide.Home, PlayingPosition.LCB, 11, 35);
        Strike(s, PlayingPosition.LW, side: TeamSide.Away);

        FlyCross(s);

        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(RestartType.Corner, stoppage.Restart);
        Assert.Equal(TeamSide.Away, stoppage.AwardedTeam);
        Assert.Equal(0, stoppage.Location.X, 6);
    }

    [Theory]
    [InlineData(0.3)] // lands just on one side of centre
    [InlineData(0.7)] // and just on the other
    public void A_header_behind_from_the_centre_never_gives_a_corner_between_the_posts(double lengthDraw)
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, lengthDraw, 0.05));
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        s.Place(TeamSide.Away, PlayingPosition.LCB, 94, 35);
        Strike(s, PlayingPosition.LW); // aimed at the penalty spot (94, 34)

        FlyCross(s);

        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(RestartType.Corner, stoppage.Restart);
        Assert.True(Math.Abs(stoppage.Location.Y - 34) >= PitchGeometry.GoalWidth / 2);
    }
}
