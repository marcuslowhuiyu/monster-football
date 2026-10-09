using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Laws;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Laws;

public class OffsideTests
{
    private static readonly MatchConfig WithOffside = MatchConfig.Default with { OffsideEnabled = true };

    /// <summary>
    /// Home attacks +X. Away's outfielders hold a line at X = 70 (their keeper deeper at 103), so the second-last
    /// opponent is at 70. Home CM has the ball at (55, 34).
    /// </summary>
    private static Scenario Attack(MatchConfig? config = null)
    {
        var s = new Scenario(config: config ?? WithOffside);
        var y = 4.0;
        foreach (var away in s.State.TeamPlayers(TeamSide.Away))
        {
            away.Position = away.Definition.Position.IsGoalkeeper() ? new Vec2(103, 34) : new Vec2(70, y += 5.5);
            away.MovementTarget = away.Position;
        }

        s.Place(TeamSide.Home, PlayingPosition.CM, 55, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        return s;
    }

    private static PlayerMatchState P(Scenario s, TeamSide side, PlayingPosition slot) => s.State.Slot(side, slot);

    [Fact]
    public void The_line_is_the_second_last_opponent_counting_the_keeper()
    {
        var s = Attack();

        Assert.Equal(70, Offside.LineX(s.State, TeamSide.Home, new Vec2(55, 34)));
        s.Place(TeamSide.Away, PlayingPosition.GK, 60, 34); // keeper up the pitch: now the last defender is second-last
        Assert.Equal(70, Offside.SecondLastOpponentX(s.State, TeamSide.Home));
    }

    [Fact]
    public void The_line_is_never_behind_the_ball_or_the_halfway_line()
    {
        var s = Attack();
        foreach (var away in s.State.TeamPlayers(TeamSide.Away))
            away.Position = new Vec2(40, away.Position.Y);

        Assert.Equal(52.5, Offside.LineX(s.State, TeamSide.Home, new Vec2(30, 34)));
        Assert.Equal(60, Offside.LineX(s.State, TeamSide.Home, new Vec2(60, 34)));
    }

    [Theory]
    [InlineData(71, true)]
    [InlineData(70, false)]   // level is onside
    [InlineData(65, false)]
    public void Offside_position_is_beyond_the_line(double x, bool offside)
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, x, 30);

        Assert.Equal(offside, Offside.IsOffsidePosition(s.State, st, new Vec2(55, 34)));
    }

    [Fact]
    public void Own_half_and_behind_the_ball_are_never_offside()
    {
        var s = Attack();
        foreach (var away in s.State.TeamPlayers(TeamSide.Away))
            away.Position = new Vec2(100, away.Position.Y);
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 50, 30);
        var lw = s.Place(TeamSide.Home, PlayingPosition.LW, 80, 30);

        Assert.False(Offside.IsOffsidePosition(s.State, st, new Vec2(20, 34)));  // own half
        Assert.False(Offside.IsOffsidePosition(s.State, lw, new Vec2(85, 34)));  // behind the ball
    }

    [Fact]
    public void Away_is_judged_in_its_own_frame()
    {
        var s = new Scenario(config: WithOffside);
        foreach (var home in s.State.TeamPlayers(TeamSide.Home))
            home.Position = home.Definition.Position.IsGoalkeeper() ? new Vec2(2, 34) : new Vec2(35, home.Position.Y);
        var st = s.Place(TeamSide.Away, PlayingPosition.ST, 34, 30);

        Assert.True(Offside.IsOffsidePosition(s.State, st, new Vec2(50, 34)));
    }

    private static void Fly(Scenario s)
    {
        var flight = new BallFlightSystem();
        while (s.State.Ball.IsInFlight && s.State.Phase == MatchPhase.OpenPlay)
        {
            s.State.Clock.Advance();
            flight.Apply(s.Context);
            MatchStateInvariants.Verify(s.State);
        }
    }

    private static void Pass(Scenario s, PlayerMatchState to, PassOutcome outcome = PassOutcome.Completed, Vec2? landing = null)
    {
        var passer = s.State.Player(s.State.PossessionPlayerId!.Value);
        PassExecution.Launch(s.Context, new PassResult(outcome, passer.Team, passer.PlayerId, to.PlayerId, null,
            passer.Position, to.Position, landing ?? to.Position, 1.0, 0.9));
        Fly(s);
    }

    [Fact]
    public void Receiving_a_pass_from_an_offside_position_is_an_offence()
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 75, 30);

        Pass(s, st);

        var stoppage = s.State.CurrentStoppage!;
        Assert.Equal((StoppageReason.Offside, RestartType.IndirectFreeKick, TeamSide.Away), (stoppage.Reason, stoppage.Restart, stoppage.AwardedTeam));
        Assert.Equal(st.PlayerId, stoppage.Offender);
        Assert.Equal(new Vec2(75, 30), stoppage.Location);
        Assert.Equal([MatchEventType.PassAttempted, MatchEventType.PlayStopped], s.EventTypes);
        Assert.Equal(st.PlayerId, s.Log.Events[^1].ActorPlayerId);
        Assert.Null(s.State.PossessionPlayerId);
    }

    [Fact]
    public void Position_is_judged_when_the_ball_is_played_not_when_it_arrives()
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 68, 30);
        var passer = s.State.Player(s.State.PossessionPlayerId!.Value);
        PassExecution.Launch(s.Context, new PassResult(PassOutcome.Completed, TeamSide.Home, passer.PlayerId, st.PlayerId, null,
            passer.Position, new Vec2(80, 30), new Vec2(80, 30), 1.0, 0.9));
        st.Position = new Vec2(80, 30); // runs on to it after the pass: onside

        Fly(s);

        Assert.Equal(MatchPhase.OpenPlay, s.State.Phase);
        Assert.True(s.State.HasBall(st.PlayerId));
    }

    [Fact]
    public void An_offside_teammate_is_not_penalised_when_an_onside_teammate_receives()
    {
        var s = Attack();
        s.Place(TeamSide.Home, PlayingPosition.ST, 75, 30);
        var lw = s.Place(TeamSide.Home, PlayingPosition.LW, 65, 50);

        Pass(s, lw);

        Assert.True(s.State.HasBall(lw.PlayerId));
        Assert.Null(s.State.Offside);
    }

    [Fact]
    public void Collecting_a_misplaced_pass_from_an_offside_position_is_an_offence()
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 75, 30);
        var lw = s.Place(TeamSide.Home, PlayingPosition.LW, 65, 50);

        Pass(s, lw, PassOutcome.Misplaced, landing: new Vec2(75, 31));
        new LooseBallSystem().Apply(s.Context);

        Assert.Equal(StoppageReason.Offside, s.State.CurrentStoppage?.Reason);
        Assert.Equal(st.PlayerId, s.State.CurrentStoppage!.Offender);
    }

    [Fact]
    public void An_opponent_touching_the_ball_resets_the_judgement()
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 75, 30);
        var lw = s.Place(TeamSide.Home, PlayingPosition.LW, 65, 50);
        Pass(s, lw, PassOutcome.Misplaced, landing: new Vec2(70, 20));
        var defender = s.State.TeamPlayers(TeamSide.Away).First(p => p.Position.DistanceTo(new Vec2(70, 20)) < 1.5);

        s.Context.GiveBall(defender.PlayerId, PossessionCause.LooseBall);
        Assert.Null(s.State.Offside);
        s.Context.GiveBall(st.PlayerId, PossessionCause.Tackle);

        Assert.Equal(MatchPhase.OpenPlay, s.State.Phase);
        Assert.True(s.State.HasBall(st.PlayerId));
    }

    [Fact]
    public void No_judgement_when_offside_is_disabled()
    {
        var s = Attack(MatchConfig.Default with { OffsideEnabled = false });
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 75, 30);

        Pass(s, st);

        Assert.True(s.State.HasBall(st.PlayerId));
    }

    [Fact]
    public void A_restart_with_no_offside_is_not_judged()
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 75, 30);
        var passer = s.State.Player(s.State.PossessionPlayerId!.Value);

        PassExecution.Launch(s.Context, new PassResult(PassOutcome.Completed, TeamSide.Home, passer.PlayerId, st.PlayerId, null,
            passer.Position, st.Position, st.Position, 1.0, 0.9), offsideApplies: false);
        Fly(s);

        Assert.True(s.State.HasBall(st.PlayerId));
    }
}
