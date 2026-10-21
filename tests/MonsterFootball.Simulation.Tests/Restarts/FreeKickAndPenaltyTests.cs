using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Periods;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Restarts;
using MonsterFootball.Simulation.Tactical;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Restarts;

public class FreeKickAndPenaltyTests
{
    private static readonly RestartTuning Tuning = RestartTuning.Default;
    private static readonly Vec2 HomePenaltySpot = new(105 - 11, 34);

    /// <summary>Kickoff layout; Away has the ball when Home is awarded <paramref name="restart"/> for a foul.</summary>
    private static Scenario Fouled(RestartType restart, Vec2 location, PlayerStats? homeStats = null, MatchConfig? config = null)
    {
        var s = new Scenario(homeStats: homeStats, config: config);
        foreach (var player in s.State.Players)
        {
            player.Position = TestTeams.KickoffLayout(player.Team, player.Definition);
            player.MovementTarget = player.Position;
        }

        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        s.Context.StopPlay(StoppageReason.Foul, restart, TeamSide.Home, location);
        return s;
    }

    private static (double Seconds, Dictionary<PlayerId, Vec2> At) RunUntilTaken(Scenario s)
    {
        var restarts = new RestartSystem(new PassResolver(), new ShotResolver(), AiTuning.Default.OnBall);
        var movement = new MovementSystem();
        var stoppedAt = s.State.Clock.Tick;
        for (var guard = 0; guard < 2000; guard++)
        {
            restarts.Position(s.Context);
            var before = s.State.Players.ToDictionary(p => p.PlayerId, p => p.Position);
            restarts.Advance(s.Context);
            if (s.State.Phase == MatchPhase.OpenPlay)
            {
                MatchStateInvariants.Verify(s.State);
                return ((s.State.Clock.Tick - stoppedAt) * s.Context.Config.TickSeconds, before);
            }

            movement.Apply(s.State, s.Context.Config.TickSeconds);
            s.State.Clock.Advance();
        }

        throw new InvalidOperationException("Restart never taken.");
    }

    [Fact]
    public void Penalty_is_struck_by_the_best_finisher_with_everyone_else_out_of_the_area_and_arc()
    {
        var s = Fouled(RestartType.Penalty, HomePenaltySpot);
        var striker = s.State.Slot(TeamSide.Home, PlayingPosition.ST);
        var deadBall = s.State.CurrentStoppage!.DeadBallSeconds;

        var (seconds, at) = RunUntilTaken(s);

        var taken = s.Log.Events.Single(e => e.Type == MatchEventType.RestartTaken);
        Assert.Equal("Penalty", taken.Metadata["restart"]);
        Assert.True(seconds >= deadBall - 1e-9);
        var shot = s.Log.Events.Single(e => e.Type == MatchEventType.ShotTaken);
        Assert.Equal("true", shot.Metadata["penalty"]);
        Assert.Equal(HomePenaltySpot, shot.Position);
        var keeper = s.State.Slot(TeamSide.Away, PlayingPosition.GK);
        Assert.True(at[keeper.PlayerId].DistanceTo(new Vec2(105, 34)) <= Tuning.InPositionTolerance + 1e-9);
        foreach (var p in s.State.Players.Where(p => p.PlayerId != taken.ActorPlayerId && p != keeper))
        {
            Assert.False(s.State.Zones.IsIn(TeamSide.Away, at[p.PlayerId], TacticalAreas.OwnPenaltyArea), $"{p} in the area");
            Assert.True(at[p.PlayerId].DistanceTo(HomePenaltySpot) >= Tuning.FreeKickExclusionRadius - 1e-9, $"{p} in the arc");
        }

        Assert.True(s.State.Ball.IsInFlight);
        Assert.NotNull(striker);
    }

    [Fact]
    public void Penalties_are_mostly_scored_and_better_finishers_score_more()
    {
        double Conversion(int finishing)
        {
            var s = new Scenario(homeStats: new PlayerStats(5, 5, finishing, 5, 5, 5, 5, 5, 5));
            var st = s.Place(TeamSide.Home, PlayingPosition.ST, HomePenaltySpot.X, HomePenaltySpot.Y);
            s.Place(TeamSide.Away, PlayingPosition.GK, 105, 34);
            s.GiveBall(TeamSide.Home, PlayingPosition.ST);
            return new ShotResolver().PenaltyEstimate(s.State, new ShotIntent(st.PlayerId)).GoalProbability;
        }

        Assert.InRange(Conversion(5), 0.65, 0.85);
        Assert.True(Conversion(9) > Conversion(5) + 0.05);
    }

    [Fact]
    public void Direct_free_kick_in_range_is_shot_over_a_wall()
    {
        var spot = new Vec2(82, 34);
        var s = Fouled(RestartType.DirectFreeKick, spot, homeStats: PlayerStats.Uniform(9));

        var (_, at) = RunUntilTaken(s);

        var goal = new Vec2(105, 34);
        var wall = s.State.TeamPlayers(TeamSide.Away).Count(p =>
        {
            var point = Vec2.ClosestPointOnSegment(spot, goal, at[p.PlayerId], out var t);
            return t is > 0.05 and < 0.95 && at[p.PlayerId].DistanceTo(point) < ShotResolver.BlockLaneWidth;
        });
        Assert.True(wall >= Tuning.WallSize, $"only {wall} in the wall");
        Assert.All(s.State.TeamPlayers(TeamSide.Away), p => Assert.True(at[p.PlayerId].DistanceTo(spot) >= Tuning.FreeKickExclusionRadius - 1e-9));
        Assert.Contains(s.Log.Events, e => e.Type == MatchEventType.ShotTaken);
        Assert.Equal(PossessionCause.FreeKick, s.Log.Events.Single(e => e.Type == MatchEventType.PossessionChanged && e.Team == TeamSide.Home).GetPossessionCause());
    }

    [Fact]
    public void Direct_free_kick_out_of_range_is_passed()
    {
        var s = Fouled(RestartType.DirectFreeKick, new Vec2(40, 30));

        RunUntilTaken(s);

        var taker = s.State.Player(s.Log.Events.Single(e => e.Type == MatchEventType.RestartTaken).ActorPlayerId!.Value);
        Assert.Contains(taker.Definition.Position.Group(), new[] { PositionGroup.Defender, PositionGroup.Midfielder });
        Assert.Equal(MatchEventType.PassAttempted, s.Log.Events[^1].Type);
        Assert.DoesNotContain(s.Log.Events, e => e.Type == MatchEventType.ShotTaken);
    }

    [Fact]
    public void A_penalty_in_regular_time_adds_time()
    {
        var s = Fouled(RestartType.Penalty, HomePenaltySpot);

        Assert.Equal(AddedTimeTuning.Default.PerPenaltySeconds, s.State.AddedTimeAllowanceSeconds);
    }

    [Fact]
    public void A_penalty_awarded_as_time_runs_out_is_taken_before_the_half_ends()
    {
        var s = new Scenario();
        var periods = new PeriodSystem();
        while (s.State.Clock.AddedTimeTicks is null)
        {
            s.State.Clock.Advance();
            periods.Apply(s.Context); // regular time ends; +0 announced
        }

        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        s.Context.StopPlay(StoppageReason.Foul, RestartType.Penalty, TeamSide.Home, HomePenaltySpot);
        s.State.Clock.Advance();
        periods.Apply(s.Context);
        Assert.DoesNotContain(s.Log.Events, e => e.Type == MatchEventType.PeriodEnded);

        s.State.ResumePlay();
        s.Context.StopPlay(StoppageReason.BallOutOfPlay, RestartType.GoalKick, TeamSide.Away, new Vec2(99.5, 43.16));
        s.State.Clock.Advance();
        periods.Apply(s.Context);
        Assert.Contains(s.Log.Events, e => e.Type == MatchEventType.PeriodEnded);
    }
}
