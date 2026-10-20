using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Restarts;
using MonsterFootball.Simulation.Tactical;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Restarts;

/// <summary>Throw-ins, goal kicks and corners, run from the moment play stops until the restart is taken.</summary>
public class SetPieceRestartTests
{
    private static readonly RestartTuning Tuning = RestartTuning.Default;

    /// <summary>Players start in the kickoff layout; Home has the ball when play stops.</summary>
    private static Scenario Stopped(RestartType restart, TeamSide awarded, Vec2 location, MatchConfig? config = null, IMatchRandom? random = null)
    {
        var s = new Scenario(config: config, random: random);
        foreach (var player in s.State.Players)
        {
            player.Position = TestTeams.KickoffLayout(player.Team, player.Definition);
            player.MovementTarget = player.Position;
        }

        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        s.Context.StopPlay(StoppageReason.BallOutOfPlay, restart, awarded, location);
        return s;
    }

    /// <summary>Runs the stoppage loop; returns the state of every player on the tick the restart was taken.</summary>
    private static (double Seconds, Dictionary<PlayerId, Vec2> AtRestart) RunUntilTaken(Scenario s, RestartSystem? system = null)
    {
        var restarts = system ?? new RestartSystem(new PassResolver(), new ShotResolver(), AiTuning.Default.OnBall);
        var movement = new MovementSystem();
        var stoppedAt = s.State.Clock.Tick;
        var stoppage = s.State.CurrentStoppage!;
        for (var guard = 0; guard < 2000; guard++)
        {
            restarts.Position(s.Context);
            var before = s.State.Players.ToDictionary(p => p.PlayerId, p => p.Position);
            restarts.Advance(s.Context);
            if (s.State.Phase == MatchPhase.OpenPlay)
            {
                MatchStateInvariants.Verify(s.State);
                var atRestart = s.State.Players.ToDictionary(p => p.PlayerId, p => p.Position);
                return ((s.State.Clock.Tick - stoppedAt) * s.Context.Config.TickSeconds, atRestart);
            }

            movement.Apply(s.State, s.Context.Config.TickSeconds);
            MatchStateInvariants.Verify(s.State);
            s.State.Clock.Advance();
        }

        throw new InvalidOperationException($"Restart {stoppage} never taken.");
    }

    private static MatchEvent Taken(Scenario s) => s.Log.Events.Single(e => e.Type == MatchEventType.RestartTaken);

    [Fact]
    public void Throw_in_is_taken_by_the_nearest_wide_player_after_the_dead_ball_time()
    {
        var spot = new Vec2(30, 0);
        var s = Stopped(RestartType.ThrowIn, TeamSide.Away, spot);

        var (seconds, at) = RunUntilTaken(s);

        var taken = Taken(s);
        var taker = s.State.Player(taken.ActorPlayerId!.Value);
        Assert.Equal(TeamSide.Away, taker.Team);
        Assert.Contains(taker.Definition.Position, new[] { PlayingPosition.LB, PlayingPosition.RB, PlayingPosition.LW, PlayingPosition.RW });
        Assert.Equal("ThrowIn", taken.Metadata["restart"]);
        Assert.Equal(spot, taken.Position);
        Assert.True(seconds >= Tuning.ThrowInDeadBallSeconds - 1e-9);
        Assert.All(s.State.TeamPlayers(TeamSide.Home), p => Assert.True(at[p.PlayerId].DistanceTo(spot) >= Tuning.ThrowInExclusionRadius - 1e-9, $"{p} too close"));
    }

    [Fact]
    public void Throw_in_is_thrown_to_a_teammate_within_range()
    {
        var s = Stopped(RestartType.ThrowIn, TeamSide.Away, new Vec2(30, 0));

        RunUntilTaken(s);

        var pass = s.Log.Events.Single(e => e.Type == MatchEventType.PassAttempted);
        var receiver = s.State.Player(pass.TargetPlayerId!.Value);
        Assert.Equal(TeamSide.Away, receiver.Team);
        Assert.True(s.State.Ball.IsInFlight);
        Assert.True(pass.Position!.Value.DistanceTo(receiver.Position) <= Tuning.ThrowInMaxRange + 1e-9);
    }

    [Fact]
    public void A_restart_for_the_team_without_the_ball_is_a_possession_change_but_not_a_ball_won()
    {
        var s = Stopped(RestartType.ThrowIn, TeamSide.Away, new Vec2(30, 0));

        RunUntilTaken(s);

        var change = s.Log.Events.Single(e => e.Type == MatchEventType.PossessionChanged && e.Team == TeamSide.Away);
        Assert.Equal(PossessionCause.ThrowIn, change.GetPossessionCause());
        Assert.False(PossessionCause.ThrowIn.IsOpenPlay());
    }

    [Fact]
    public void A_taker_sent_off_during_the_stoppage_is_replaced()
    {
        var spot = new Vec2(PitchGeometry.GoalAreaDepth, 34 + PitchGeometry.GoalAreaWidth / 2);
        var s = Stopped(RestartType.GoalKick, TeamSide.Home, spot);
        var restarts = new RestartSystem(new PassResolver(), new ShotResolver(), AiTuning.Default.OnBall);
        restarts.Position(s.Context); // the keeper is chosen to take it
        var keeper = s.State.Slot(TeamSide.Home, PlayingPosition.GK);
        s.State.SendOff(keeper.PlayerId);

        RunUntilTaken(s, restarts); // invariants are checked every tick: the keeper never comes back on

        var taker = s.State.Player(Taken(s).ActorPlayerId!.Value);
        Assert.NotEqual(keeper.PlayerId, taker.PlayerId);
        Assert.True(taker.IsActive);
    }

    [Fact]
    public void Goal_kick_is_taken_by_the_keeper_with_opponents_outside_the_area()
    {
        var spot = new Vec2(PitchGeometry.GoalAreaDepth, 34 + PitchGeometry.GoalAreaWidth / 2);
        var s = Stopped(RestartType.GoalKick, TeamSide.Home, spot);
        // Two Away forwards start inside the Home penalty area and must leave it.
        s.Place(TeamSide.Away, PlayingPosition.ST, 8, 34);
        s.Place(TeamSide.Away, PlayingPosition.LW, 12, 25);

        var (seconds, at) = RunUntilTaken(s);

        var taker = s.State.Player(Taken(s).ActorPlayerId!.Value);
        Assert.Equal(PlayingPosition.GK, taker.Definition.Position);
        Assert.Equal(TeamSide.Home, taker.Team);
        Assert.True(seconds >= Tuning.GoalKickDeadBallSeconds - 1e-9);
        Assert.All(s.State.TeamPlayers(TeamSide.Away),
            p => Assert.False(s.State.Zones.IsIn(TeamSide.Home, at[p.PlayerId], TacticalAreas.OwnPenaltyArea), $"{p} in the area"));
        Assert.Equal(MatchEventType.PassAttempted, s.Log.Events[^1].Type);
    }

    [Fact]
    public void Corner_is_delivered_into_the_box_with_defenders_back_the_full_distance()
    {
        var corner = new Vec2(105, 68);
        var s = Stopped(RestartType.Corner, TeamSide.Home, corner);

        var (seconds, at) = RunUntilTaken(s);

        var taker = s.State.Player(Taken(s).ActorPlayerId!.Value);
        Assert.Equal(TeamSide.Home, taker.Team);
        Assert.DoesNotContain(taker.Definition.Position, new[] { PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.ST, PlayingPosition.GK });
        Assert.True(seconds >= Tuning.CornerDeadBallSeconds - 1e-9);
        Assert.All(s.State.TeamPlayers(TeamSide.Away), p => Assert.True(at[p.PlayerId].DistanceTo(corner) >= Tuning.CornerExclusionRadius - 1e-9, $"{p} too close"));

        Assert.DoesNotContain(s.Log.Events, e => e.Type == MatchEventType.PassAttempted);
        var delivery = s.Log.Events.Single(e => e.Type == MatchEventType.Cross);
        Assert.Equal("Corner", delivery.Metadata["kind"]);
        var cross = s.State.Ball.Flight!.Cross!;
        Assert.True(s.State.Zones.IsIn(TeamSide.Home, cross.IntendedTarget, TacticalAreas.OppositionPenaltyArea));
        Assert.Null(s.State.Offside); // no offside from a corner
        Assert.True(s.State.Zones.IsIn(TeamSide.Home, at[s.State.Slot(TeamSide.Home, PlayingPosition.ST).PlayerId], TacticalAreas.OppositionPenaltyArea));
        Assert.True(s.State.TeamPlayers(TeamSide.Away).Count(p => s.State.Zones.IsIn(TeamSide.Away, at[p.PlayerId], TacticalAreas.OwnPenaltyArea)) >= 8);
    }

    [Fact]
    public void Corner_layout_mirrors_for_the_other_corner_and_team()
    {
        var home = Stopped(RestartType.Corner, TeamSide.Home, new Vec2(105, 0));
        var away = Stopped(RestartType.Corner, TeamSide.Away, new Vec2(0, 68));

        var (_, homeAt) = RunUntilTaken(home);
        var (_, awayAt) = RunUntilTaken(away);

        foreach (var slot in new[] { PlayingPosition.ST, PlayingPosition.LCB, PlayingPosition.RCB })
        {
            var h = homeAt[home.State.Slot(TeamSide.Home, slot).PlayerId];
            var a = awayAt[away.State.Slot(TeamSide.Away, slot).PlayerId];
            Assert.Equal(h.X, 105 - a.X, 6);
            Assert.Equal(h.Y, 68 - a.Y, 6);
        }
    }

    [Fact]
    public void Offside_free_kick_is_taken_by_the_nearest_defender_and_judged_for_offside()
    {
        var spot = new Vec2(35, 30);
        var config = MatchConfig.Default with { OffsideEnabled = true };
        var s = new Scenario(config: config);
        foreach (var player in s.State.Players)
        {
            player.Position = TestTeams.KickoffLayout(player.Team, player.Definition);
            player.MovementTarget = player.Position;
        }

        s.GiveBall(TeamSide.Away, PlayingPosition.ST);
        s.Context.StopPlay(StoppageReason.Offside, RestartType.IndirectFreeKick, TeamSide.Home, spot);

        var (seconds, at) = RunUntilTaken(s);

        var taken = Taken(s);
        var taker = s.State.Player(taken.ActorPlayerId!.Value);
        Assert.Equal(PositionGroup.Defender, taker.Definition.Position.Group());
        Assert.Equal("IndirectFreeKick", taken.Metadata["restart"]);
        Assert.True(seconds >= Tuning.IndirectFreeKickDeadBallSeconds - 1e-9);
        Assert.All(s.State.TeamPlayers(TeamSide.Away), p => Assert.True(at[p.PlayerId].DistanceTo(spot) >= Tuning.FreeKickExclusionRadius - 1e-9, $"{p} too close"));
        Assert.Equal(PossessionCause.FreeKick, s.Log.Events.Single(e => e.Type == MatchEventType.PossessionChanged && e.Team == TeamSide.Home).GetPossessionCause());
        Assert.Equal(MatchEventType.PassAttempted, s.Log.Events[^1].Type);
        Assert.NotNull(s.State.Offside);
    }

    [Fact]
    public void A_throw_in_to_a_player_beyond_the_line_is_not_offside()
    {
        var config = MatchConfig.Default with { OffsideEnabled = true };
        var s = Stopped(RestartType.ThrowIn, TeamSide.Home, new Vec2(80, 68), config);

        RunUntilTaken(s);

        Assert.Null(s.State.Offside);
    }

    [Fact]
    public void After_the_setup_timeout_stragglers_are_placed_and_the_restart_is_taken()
    {
        var config = MatchConfig.Default with { Restarts = RestartTuning.Default with { SetupTimeoutSeconds = 0 } };
        var spot = new Vec2(100, 68);
        var s = Stopped(RestartType.ThrowIn, TeamSide.Home, spot, config);

        var (seconds, _) = RunUntilTaken(s);

        Assert.Equal(Tuning.ThrowInDeadBallSeconds, seconds, 6);
        Assert.Equal(spot, s.Log.Events.Single(e => e.Type == MatchEventType.RestartTaken).Position);
    }

    [Fact]
    public void Nothing_random_or_eventful_happens_until_the_restart_is_taken()
    {
        var s = Stopped(RestartType.Corner, TeamSide.Home, new Vec2(105, 68), random: new ScriptedRandom()); // throws on any draw
        var restarts = new RestartSystem(new PassResolver(), new ShotResolver(), AiTuning.Default.OnBall);
        var movement = new MovementSystem();
        var events = s.Log.Events.Count;

        for (var i = 0; i < 40; i++)
        {
            restarts.Position(s.Context);
            restarts.Advance(s.Context);
            movement.Apply(s.State, s.Context.Config.TickSeconds);
            s.State.Clock.Advance();
        }

        Assert.NotEqual(MatchPhase.OpenPlay, s.State.Phase);
        Assert.Equal(events, s.Log.Events.Count);
    }
}
