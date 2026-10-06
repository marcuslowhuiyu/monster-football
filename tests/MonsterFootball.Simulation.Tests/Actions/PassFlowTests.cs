using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

/// <summary>Launch → flight → landing, with possession and events checked at each stage.</summary>
public class PassFlowTests
{
    private static (Scenario Scenario, PassIntent Intent) Setup(params double[] draws)
    {
        var s = new Scenario(random: new ScriptedRandom(draws));
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        s.Place(TeamSide.Home, PlayingPosition.LCM, 55, 40);
        s.Place(TeamSide.Away, PlayingPosition.CM, 47, 37);   // in the lane
        var passer = s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        var receiver = s.State.Slot(TeamSide.Home, PlayingPosition.LCM);
        return (s, new PassIntent(passer.PlayerId, receiver.PlayerId));
    }

    private static PassResult LaunchAndFly(Scenario s, PassIntent intent)
    {
        var result = new PassResolver().Resolve(s.State, intent, s.Context.Random);
        PassExecution.Launch(s.Context, result);

        var flight = new BallFlightSystem();
        while (s.State.Ball.IsInFlight)
        {
            Assert.Null(s.State.PossessionPlayerId);
            Assert.Equal(TeamSide.Home, s.State.AttackingTeam);
            s.State.Clock.Advance();
            flight.Apply(s.Context);
            MatchStateInvariants.Verify(s.State);
        }

        return result;
    }

    [Fact]
    public void Completed_pass_transfers_possession_to_the_receiver_on_arrival()
    {
        var (s, intent) = Setup(0.0, 0.999);

        LaunchAndFly(s, intent);

        Assert.True(s.State.HasBall(intent.Receiver));
        Assert.Equal(TeamSide.Home, s.State.PossessionTeam);
        Assert.Equal([MatchEventType.PassAttempted, MatchEventType.PassCompleted], s.EventTypes);
    }

    [Fact]
    public void Ball_travels_for_the_resolved_duration()
    {
        var (s, intent) = Setup(0.0, 0.999);
        var launchTick = s.State.Clock.Tick;

        var result = LaunchAndFly(s, intent);

        var expectedTicks = (long)Math.Ceiling(result.TravelDuration / MatchConfig.Default.TickSeconds - 1e-9);
        Assert.Equal(launchTick + Math.Max(1, expectedTicks), s.State.Clock.Tick);
    }

    [Fact]
    public void Intercepted_pass_transfers_possession_to_the_interceptor()
    {
        var (s, intent) = Setup(0.0, 0.0);
        var interceptor = s.State.Slot(TeamSide.Away, PlayingPosition.CM);

        LaunchAndFly(s, intent);

        Assert.True(s.State.HasBall(interceptor.PlayerId));
        Assert.Equal(TeamSide.Away, s.State.PossessionTeam);
        Assert.Equal(
            [MatchEventType.PassAttempted, MatchEventType.PassIntercepted, MatchEventType.PossessionChanged],
            s.EventTypes);

        var changed = s.Log.Events[^1];
        Assert.Equal(TeamSide.Away, changed.Team);
        Assert.Equal(interceptor.PlayerId, changed.ActorPlayerId);
        Assert.Equal(PossessionCause.Interception, changed.GetPossessionCause());
    }

    [Fact]
    public void Misplaced_pass_leaves_a_loose_ball_that_the_nearest_player_collects()
    {
        var (s, intent) = Setup(0.9999999, 0.75, 0.5, 0.5);

        var result = LaunchAndFly(s, intent);

        Assert.True(s.State.Ball.IsLoose);
        Assert.Equal(result.TargetPosition, s.State.Ball.Position);
        Assert.Equal(MatchEventType.PassMisplaced, s.EventTypes[^1]);

        var collector = s.Place(TeamSide.Away, PlayingPosition.LW, result.TargetPosition.X + 1, result.TargetPosition.Y);
        new LooseBallSystem().Apply(s.Context);

        Assert.True(s.State.HasBall(collector.PlayerId));
        Assert.Equal(MatchEventType.PossessionChanged, s.EventTypes[^1]);
        Assert.Equal(PossessionCause.LooseBall, s.Log.Events[^1].GetPossessionCause());
    }

    [Fact]
    public void Loose_ball_is_not_collected_from_outside_control_radius()
    {
        var s = new Scenario();
        s.State.SetBallLoose(new Vec2(52.5, 34));
        s.Place(TeamSide.Home, PlayingPosition.CM, 52.5 + LooseBallSystem.ControlRadius + 0.1, 34);

        new LooseBallSystem().Apply(s.Context);

        Assert.True(s.State.Ball.IsLoose);
    }

    [Fact]
    public void Same_team_recovering_a_loose_ball_is_not_a_possession_change()
    {
        var s = new Scenario();
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        s.State.SetBallLoose(new Vec2(60, 34));
        s.Place(TeamSide.Home, PlayingPosition.ST, 60.5, 34);

        new LooseBallSystem().Apply(s.Context);

        Assert.DoesNotContain(MatchEventType.PossessionChanged, s.EventTypes);
    }
}
