using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Laws;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tactical;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Laws;

public class OutOfPlayTests
{
    private static readonly PitchGeometry Pitch = PitchGeometry.Standard;
    private static readonly PlayerId Somebody = new(1);

    [Fact]
    public void A_path_that_stays_on_the_pitch_does_not_cross()
    {
        Assert.Null(OutOfPlay.Crossing(Pitch, new Vec2(50, 30), new Vec2(60, 68)));
    }

    [Theory]
    [InlineData(40, 60, 50, 72, 40 + 10 * (8.0 / 12), 68, false)]
    [InlineData(40, 10, 40, -5, 40, 0, false)]
    [InlineData(100, 30, 110, 40, 105, 35, true)]
    [InlineData(5, 30, -5, 30, 0, 30, true)]
    [InlineData(104, 67, 106, 69, 105, 68, true)] // through the corner: goal line
    public void Crossing_point_is_where_the_path_meets_the_line(double fx, double fy, double tx, double ty, double cx, double cy, bool goalLine)
    {
        var crossing = OutOfPlay.Crossing(Pitch, new Vec2(fx, fy), new Vec2(tx, ty))!.Value;

        Assert.Equal(cx, crossing.Point.X, 9);
        Assert.Equal(cy, crossing.Point.Y, 9);
        Assert.Equal(goalLine ? BoundaryLine.GoalLine : BoundaryLine.Touchline, crossing.Line);
    }

    [Fact]
    public void Path_leaving_through_the_side_first_crosses_the_touchline()
    {
        var crossing = OutOfPlay.Crossing(Pitch, new Vec2(100, 66), new Vec2(108, 72))!.Value;

        Assert.Equal(BoundaryLine.Touchline, crossing.Line);
        Assert.Equal(new Vec2(102.66666666666667, 68), crossing.Point);
    }

    [Theory]
    [InlineData(TeamSide.Home, TeamSide.Away)]
    [InlineData(TeamSide.Away, TeamSide.Home)]
    public void Over_a_touchline_is_a_throw_in_to_the_other_team_where_it_crossed(TeamSide touched, TeamSide awarded)
    {
        var (restart, team, location) = OutOfPlay.Decide(Pitch, new Vec2(33, 68), BoundaryLine.Touchline, new BallTouch(Somebody, touched));

        Assert.Equal((RestartType.ThrowIn, awarded, new Vec2(33, 68)), (restart, team, location));
    }

    [Fact]
    public void Over_the_goal_line_off_an_attacker_is_a_goal_kick_from_the_goal_area()
    {
        var (restart, team, location) = OutOfPlay.Decide(Pitch, new Vec2(105, 50), BoundaryLine.GoalLine, new BallTouch(Somebody, TeamSide.Home));

        Assert.Equal((RestartType.GoalKick, TeamSide.Away), (restart, team));
        Assert.True(new TacticalZones(Pitch, ZoneBoundaries.Default).IsIn(TeamSide.Away, location, TacticalAreas.OwnGoalArea));
        Assert.True(location.Y > 34, "goal kick from the side the ball went out");
    }

    [Theory]
    [InlineData(0, 10, TeamSide.Away, 0, 0)]
    [InlineData(0, 60, TeamSide.Away, 0, 68)]
    [InlineData(105, 20, TeamSide.Home, 105, 0)]
    public void Over_the_goal_line_off_a_defender_is_a_corner_at_the_nearest_corner(double x, double y, TeamSide attackers, double cx, double cy)
    {
        var defenders = attackers.Opponent();
        var (restart, team, location) = OutOfPlay.Decide(Pitch, new Vec2(x, y), BoundaryLine.GoalLine, new BallTouch(Somebody, defenders));

        Assert.Equal((RestartType.Corner, attackers, new Vec2(cx, cy)), (restart, team, location));
    }

    [Fact]
    public void A_misplaced_pass_that_flies_over_the_touchline_stops_play_where_it_crossed()
    {
        var s = new Scenario();
        var passer = s.Place(TeamSide.Home, PlayingPosition.LB, 40, 60);
        var receiver = s.Place(TeamSide.Home, PlayingPosition.LW, 60, 64);
        s.GiveBall(TeamSide.Home, PlayingPosition.LB);
        var pass = new PassResult(PassOutcome.Misplaced, TeamSide.Home, passer.PlayerId, receiver.PlayerId, null,
            passer.Position, receiver.Position, new Vec2(62, 74), 2.0, 0.5);
        PassExecution.Launch(s.Context, pass);

        var flight = new BallFlightSystem();
        while (s.State.Phase == MatchPhase.OpenPlay && s.State.Ball.IsInFlight)
        {
            s.State.Clock.Advance();
            flight.Apply(s.Context);
            MatchStateInvariants.Verify(s.State);
        }

        Assert.Equal(MatchPhase.Stopped, s.State.Phase);
        Assert.Equal([MatchEventType.PassAttempted, MatchEventType.PassMisplaced, MatchEventType.PlayStopped], s.EventTypes);
        var stoppage = s.State.CurrentStoppage!;
        Assert.Equal((StoppageReason.BallOutOfPlay, RestartType.ThrowIn, TeamSide.Away), (stoppage.Reason, stoppage.Restart, stoppage.AwardedTeam));
        Assert.Equal(68, stoppage.Location.Y);
        Assert.Equal(stoppage.Location, s.State.Ball.Position);
    }

    [Fact]
    public void A_misplaced_pass_that_lands_rolls_on_and_can_run_out()
    {
        var s = new Scenario();
        var passer = s.Place(TeamSide.Home, PlayingPosition.LB, 40, 50);
        var receiver = s.Place(TeamSide.Home, PlayingPosition.LW, 70, 60);
        s.GiveBall(TeamSide.Home, PlayingPosition.LB);
        // Lands 1 m inside the touchline after a 30 m flight; rolls on 4.5 m and crosses.
        var pass = new PassResult(PassOutcome.Misplaced, TeamSide.Home, passer.PlayerId, receiver.PlayerId, null,
            passer.Position, receiver.Position, new Vec2(40, 67), 1.5, 0.5);
        PassExecution.Launch(s.Context, pass);

        var flight = new BallFlightSystem();
        var loose = new LooseBallSystem();
        for (var i = 0; i < 40 && s.State.Phase == MatchPhase.OpenPlay; i++)
        {
            s.State.Clock.Advance();
            flight.Apply(s.Context);
            if (s.State.Phase == MatchPhase.OpenPlay)
                loose.Apply(s.Context);
            MatchStateInvariants.Verify(s.State);
        }

        Assert.Equal(RestartType.ThrowIn, s.State.CurrentStoppage?.Restart);
        Assert.Equal(new Vec2(40, 68), s.State.CurrentStoppage!.Location);
    }

    [Fact]
    public void A_rolling_ball_slows_to_a_stop_at_its_distance()
    {
        var roll = LooseBallMotion.Covering(new Vec2(10, 10), new Vec2(3, 4), 6, startTick: 100);

        Assert.Equal(new Vec2(10, 10), roll.PositionAt(100, 0.25));
        Assert.Equal(6, roll.RollDistance, 9);
        var end = roll.PositionAt(100 + 1000, 0.25);
        Assert.Equal(new Vec2(13.6, 14.8), new Vec2(Math.Round(end.X, 9), Math.Round(end.Y, 9)));
        var halfway = roll.PositionAt(100 + (long)(roll.StopSeconds / 0.25 / 2), 0.25).DistanceTo(new Vec2(10, 10));
        Assert.True(halfway > 3, "decelerating: more than half the distance in the first half of the time");
        Assert.True(roll.HasStoppedBy(100 + (long)Math.Ceiling(roll.StopSeconds / 0.25), 0.25));
    }

    [Fact]
    public void A_tackle_can_knock_the_ball_out_off_the_tackler()
    {
        // contact, clean fails, direction (+y: toward the touchline), 5 m.
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.0, 0.9999, 0.9999, 0.5, 0.9999, 0.9999, 0.5)); // the last draw is the dead-ball time
        s.Place(TeamSide.Home, PlayingPosition.LW, 40, 66);
        var tackler = s.Place(TeamSide.Away, PlayingPosition.RB, 41, 66);
        s.GiveBall(TeamSide.Home, PlayingPosition.LW);
        for (var i = 0; i < 8; i++)
            s.State.Clock.Advance();

        new TackleSystem(new TackleResolver(), AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Equal(MatchEventType.PlayStopped, s.EventTypes[^1]);
        Assert.Equal((RestartType.ThrowIn, TeamSide.Home), (s.State.CurrentStoppage!.Restart, s.State.CurrentStoppage.AwardedTeam));
        Assert.Equal(tackler.PlayerId, s.State.LastTouch?.Player);
        Assert.True(s.State.Pitch.Contains(s.State.Ball.Position));
    }
}
