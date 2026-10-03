using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.AI;

public class OffBallDecisionTests
{
    private static readonly OffBallCandidateGenerator Generator = new(AiTuning.Default.OffBall);
    private static readonly HashSet<PlayerId> NobodyMarked = [];

    /// <summary>Full match state with Home's CM holding the ball at <paramref name="ball"/>; shape targets assigned.</summary>
    private static Scenario HomeAttacking(Vec2 ball)
    {
        var s = new Scenario();
        foreach (var p in s.State.Players)
        {
            p.Position = Formation433.KickoffPosition(s.State.Pitch, p.Team, p.Definition.Position);
            p.MovementTarget = p.Position;
        }

        var carrier = s.Place(TeamSide.Home, PlayingPosition.CM, ball.X, ball.Y);
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        new TeamShapeSystem().Apply(s.State);
        return s;
    }

    private static IReadOnlyList<ActionCandidate> Candidates(Scenario s, PlayerMatchState p, bool attacking) =>
        Generator.Generate(s.State, p, attacking, supportAvailable: true, runAvailable: true, NobodyMarked);

    [Fact]
    public void Move_into_position_is_always_available_and_keeps_the_shape_target()
    {
        var s = HomeAttacking(new Vec2(60, 34));
        var player = s.State.Slot(TeamSide.Home, PlayingPosition.LB);

        var move = Assert.Single(Candidates(s, player, attacking: true), c => c.Kind == ActionKind.MoveIntoPosition);
        Assert.Equal(player.MovementTarget, move.Target);
    }

    [Fact]
    public void Forwards_can_run_beyond_the_ball_up_to_the_opponents_last_line()
    {
        var s = HomeAttacking(new Vec2(60, 34));
        var striker = s.State.Slot(TeamSide.Home, PlayingPosition.ST);
        var lastLine = TeamShapeSystem.OpponentLastLineX(s.State, TeamSide.Home);

        var run = Assert.Single(Candidates(s, striker, attacking: true), c => c.Kind == ActionKind.MakeForwardRun);
        Assert.True(run.Target!.Value.X > 60, "run should go beyond the ball");
        Assert.True(run.Target.Value.X <= lastLine + AiTuning.Default.OffBall.RunBeyondLine + 1e-9);
    }

    [Fact]
    public void Defenders_do_not_make_forward_runs_or_support()
    {
        var s = HomeAttacking(new Vec2(60, 34));
        var kinds = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.LCB), attacking: true).Select(c => c.Kind);
        Assert.DoesNotContain(ActionKind.MakeForwardRun, kinds);
        Assert.DoesNotContain(ActionKind.SupportBallCarrier, kinds);
    }

    [Fact]
    public void Support_spot_is_near_the_carrier_and_on_the_pitch()
    {
        var s = HomeAttacking(new Vec2(50, 34));
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var support = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.LCM), attacking: true)
            .Single(c => c.Kind == ActionKind.SupportBallCarrier);

        Assert.Equal(AiTuning.Default.OffBall.SupportDistance, support.Target!.Value.DistanceTo(carrier.Position), precision: 6);
        Assert.True(s.State.Pitch.Contains(support.Target.Value));
    }

    [Fact]
    public void Defenders_mark_goal_side_of_a_nearby_attacker()
    {
        var s = new Scenario();
        s.Place(TeamSide.Away, PlayingPosition.CM, 40, 34);
        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        var attacker = s.Place(TeamSide.Away, PlayingPosition.ST, 22, 30);
        var defender = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 36);
        s.Place(TeamSide.Home, PlayingPosition.CM, 38, 34); // nearest to the ball, so the CM presses
        new TeamShapeSystem().Apply(s.State);
        Assert.NotEqual(MovementIntensity.Sprint, defender.MovementIntensity);

        var mark = Candidates(s, defender, attacking: false).SingleOrDefault(c => c.Kind == ActionKind.DefendGoalSide);

        Assert.NotNull(mark);
        Assert.Equal(attacker.PlayerId, mark.Subject);
        Assert.True(mark.Target!.Value.X < attacker.Position.X, "mark point should be goal-side (Home defends X = 0)");
    }

    [Fact]
    public void Already_marked_attackers_are_not_marked_twice()
    {
        var s = new Scenario();
        s.Place(TeamSide.Away, PlayingPosition.CM, 40, 34);
        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        var attacker = s.Place(TeamSide.Away, PlayingPosition.ST, 22, 30);
        var defender = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 36);
        s.Place(TeamSide.Home, PlayingPosition.CM, 38, 34);
        new TeamShapeSystem().Apply(s.State);

        var candidates = Generator.Generate(s.State, defender, attacking: false, true, true, new HashSet<PlayerId> { attacker.PlayerId });

        Assert.DoesNotContain(candidates, c => c.Kind == ActionKind.DefendGoalSide && c.Subject == attacker.PlayerId);
    }

    [Fact]
    public void Players_far_out_of_shape_when_defending_can_recover()
    {
        var s = new Scenario();
        s.Place(TeamSide.Away, PlayingPosition.CM, 40, 34);
        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        var stranded = s.Place(TeamSide.Home, PlayingPosition.LCM, 90, 60);
        new TeamShapeSystem().Apply(s.State);

        var recover = Assert.Single(Candidates(s, stranded, attacking: false), c => c.Kind == ActionKind.RecoverPosition);
        var move = Assert.Single(Candidates(s, stranded, attacking: false), c => c.Kind == ActionKind.MoveIntoPosition);
        Assert.True(recover.Utility > move.Utility);
    }

    [Fact]
    public void Team_limits_cap_supporters_and_runners()
    {
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed: 5);
        for (var tick = 0; tick < 4 * 300; tick++)
        {
            simulation.Step();
            foreach (var side in Enum.GetValues<TeamSide>())
            {
                var plans = simulation.State.TeamPlayers(side).Select(p => p.OffBallPlan).Where(p => p is not null).ToList();
                Assert.True(plans.Count(p => p!.Kind == ActionKind.SupportBallCarrier) <= OffBallDecisionSystem.MaxSupporters);
                Assert.True(plans.Count(p => p!.Kind == ActionKind.MakeForwardRun) <= OffBallDecisionSystem.MaxRunners);
                var subjects = plans.Where(p => p!.Subject is not null).Select(p => p!.Subject).ToList();
                Assert.Equal(subjects.Count, subjects.Distinct().Count());
            }
        }
    }

    [Fact]
    public void Goalkeepers_and_the_ball_carrier_have_no_off_ball_plan()
    {
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed: 6);
        for (var tick = 0; tick < 4 * 120; tick++)
        {
            simulation.Step();
            foreach (var p in simulation.State.Players)
            {
                if (p.Definition.Position.IsGoalkeeper() || simulation.State.HasBall(p.PlayerId))
                    Assert.Null(p.OffBallPlan);
            }
        }
    }

    [Fact]
    public void Every_off_ball_behaviour_appears_over_a_match()
    {
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed: 7);
        var seen = new HashSet<ActionKind>();
        while (!simulation.IsFinished)
        {
            simulation.Step();
            foreach (var p in simulation.State.Players)
                if (p.OffBallPlan is { } plan)
                    seen.Add(plan.Kind);
        }

        Assert.Contains(ActionKind.MoveIntoPosition, seen);
        Assert.Contains(ActionKind.SupportBallCarrier, seen);
        Assert.Contains(ActionKind.MakeForwardRun, seen);
        Assert.Contains(ActionKind.DefendGoalSide, seen);
        Assert.Contains(ActionKind.RecoverPosition, seen);
    }
}
