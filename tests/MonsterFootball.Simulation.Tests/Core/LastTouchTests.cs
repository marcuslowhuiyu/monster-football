using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Core;

public class LastTouchTests
{
    [Fact]
    public void Nobody_has_touched_the_ball_before_the_kickoff()
    {
        Assert.Null(TestTeams.Match().LastTouch);
    }

    [Fact]
    public void Gaining_possession_is_a_touch()
    {
        var s = new Scenario();
        var cm = s.GiveBall(TeamSide.Away, PlayingPosition.CM);

        Assert.Equal(new BallTouch(cm.PlayerId, TeamSide.Away), s.State.LastTouch);
    }

    [Fact]
    public void A_misplaced_pass_was_last_touched_by_the_passer()
    {
        var s = new Scenario(random: new ScriptedRandom(0.9999999, 0.75, 0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        s.Place(TeamSide.Home, PlayingPosition.LCM, 55, 40);
        var passer = s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        var receiver = s.State.Slot(TeamSide.Home, PlayingPosition.LCM);

        var result = new PassResolver().Resolve(s.State, new PassIntent(passer.PlayerId, receiver.PlayerId), s.Context.Random);
        PassExecution.Launch(s.Context, result);
        var flight = new BallFlightSystem();
        while (s.State.Ball.IsInFlight)
        {
            s.State.Clock.Advance();
            flight.Apply(s.Context);
        }

        Assert.Equal(PassOutcome.Misplaced, result.Outcome);
        Assert.Equal(passer.PlayerId, s.State.LastTouch?.Player);
    }

    [Fact]
    public void A_tackle_that_knocks_the_ball_loose_was_last_touched_by_the_tackler()
    {
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.0, 0.9999, 0.9999, 0.75, 0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var tackler = s.Place(TeamSide.Away, PlayingPosition.CM, 41, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        for (var i = 0; i < 8; i++)
            s.State.Clock.Advance();

        new TackleSystem(new TackleResolver(), AiTuning.Default.OffBall).Apply(s.Context);

        Assert.True(s.State.Ball.IsLoose);
        Assert.Equal(new BallTouch(tackler.PlayerId, TeamSide.Away), s.State.LastTouch);
    }
}
