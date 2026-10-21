using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Observation;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Statistics;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

/// <summary>Strike → flight → landing or leaving play, with events and state checked.</summary>
public class ClearanceFlowTests
{
    private static ClearanceResult Strike(Scenario s, PlayingPosition slot)
    {
        var kicker = s.GiveBall(TeamSide.Home, slot);
        var result = new ClearanceResolver().Resolve(s.State, new ClearanceIntent(kicker.PlayerId), s.Context.Random);
        ClearanceExecution.Launch(s.Context, result);
        return result;
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

    [Fact]
    public void A_clearance_flies_and_lands_loose_and_rolling()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        var result = Strike(s, PlayingPosition.LCB);

        Assert.Equal(new BallTouch(result.Kicker, TeamSide.Home), s.State.LastTouch);
        var frame = MatchFrame.Capture(s.State);
        Assert.Equal(BallFlightKind.Clearance, frame.Ball.Flight!.Kind);
        Assert.Null(frame.Ball.Flight.IntendedReceiver);

        Fly(s);

        Assert.True(s.State.Ball.IsLoose);
        Assert.NotNull(s.State.Ball.Roll);
        Assert.Equal(new Vec2(55, 34), s.State.Ball.Position);
        Assert.Equal([MatchEventType.Clearance], s.EventTypes);
        var clearance = s.Log.Events.Single();
        Assert.Equal("Long", clearance.Metadata["kind"]);
        Assert.Equal(1, MatchStatistics.FromEvents(s.Log.Events).Home.Clearances);
    }

    [Fact]
    public void A_clearance_over_the_touchline_gives_the_opponents_a_throw_in()
    {
        // Wide: aim (35, 0); a lateral draw of 0 pushes the landing ~8 m sideways, over the line at (30, -6.25).
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.5, 0.5)); // the last draw is the dead-ball time
        s.Place(TeamSide.Home, PlayingPosition.LCB, 10, 20);
        s.Place(TeamSide.Away, PlayingPosition.ST, 11, 20);
        Strike(s, PlayingPosition.LCB);

        Fly(s);

        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(RestartType.ThrowIn, stoppage.Restart);
        Assert.Equal(TeamSide.Away, stoppage.AwardedTeam);
    }

    [Fact]
    public void An_attacker_offside_when_the_clearance_is_struck_is_penalised_for_collecting_it()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5, 0.5)); // the last draw is the dead-ball time
        foreach (var p in s.State.TeamPlayers(TeamSide.Away).Where(p => !p.Role.IsGoalkeeper()).Select((p, i) => (p, i)))
            s.Place(TeamSide.Away, p.p.Role, 45, 4 + 6 * p.i); // the Away line at x = 45
        s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        var runner = s.Place(TeamSide.Home, PlayingPosition.RW, 56, 34); // beyond the line in the opponents' half
        Strike(s, PlayingPosition.LCB);

        Fly(s);
        new LooseBallSystem().Apply(s.Context);

        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(StoppageReason.Offside, stoppage.Reason);
        Assert.Equal(runner.PlayerId, stoppage.Offender);
    }
}
