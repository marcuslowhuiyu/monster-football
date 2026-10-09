using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.AI;

/// <summary>The AI's side of offside (MILESTONE_2 §10.5, D8): passers avoid offside teammates, runners hold the line.</summary>
public class OffsideAwarenessTests
{
    /// <summary>Away's outfielders at X = 70 (keeper at 103); Home CM on the ball at (55, 34) with the given stats.</summary>
    private static Scenario Attack(PlayerStats? homeStats = null)
    {
        var s = new Scenario(homeStats: homeStats);
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

    private static bool OffersPassTo(Scenario s, PlayerMatchState receiver)
    {
        var generator = new OnBallCandidateGenerator(AiTuning.Default.OnBall, new PassResolver(), new CarryResolver(), new ShotResolver());
        var carrier = s.State.Player(s.State.PossessionPlayerId!.Value);
        return generator.Generate(s.State, carrier, 1.0)
            .Any(c => c.Kind == ActionKind.Pass && ((PassIntent)c.Intent!).Receiver == receiver.PlayerId);
    }

    [Fact]
    public void No_pass_is_offered_to_a_clearly_offside_teammate()
    {
        var s = Attack();
        var st = s.Place(TeamSide.Home, PlayingPosition.ST, 74, 30);
        var lw = s.Place(TeamSide.Home, PlayingPosition.LW, 66, 50);

        Assert.False(OffersPassTo(s, st));
        Assert.True(OffersPassTo(s, lw));
    }

    [Fact]
    public void Only_a_less_aware_passer_misjudges_a_marginal_offside()
    {
        var careless = Attack(PlayerStats.Uniform(1));
        var aware = Attack(PlayerStats.Uniform(10));
        careless.Place(TeamSide.Home, PlayingPosition.ST, 71, 30);
        aware.Place(TeamSide.Home, PlayingPosition.ST, 71, 30);

        Assert.True(OffersPassTo(careless, careless.State.Slot(TeamSide.Home, PlayingPosition.ST)));
        Assert.False(OffersPassTo(aware, aware.State.Slot(TeamSide.Home, PlayingPosition.ST)));
    }

    [Fact]
    public void Misjudgement_and_eagerness_fall_with_awareness()
    {
        var w = AiTuning.Default;
        Assert.Equal(0, w.OnBall.OffsideMisjudgement(10), 9);
        Assert.True(w.OnBall.OffsideMisjudgement(1) > w.OnBall.OffsideMisjudgement(5));
        Assert.True(w.OffBall.RunHoldOffset(10) < 0, "an aware runner waits onside");
        Assert.True(w.OffBall.RunHoldOffset(1) > w.OffBall.RunHoldOffset(5));
    }

    private static (Scenario Scenario, PlayerMatchState Runner) Runner(int awareness)
    {
        var s = Attack(PlayerStats.Uniform(awareness));
        var runner = s.Place(TeamSide.Home, PlayingPosition.ST, 65, 30);
        runner.MovementIntensity = MovementIntensity.Jog;
        runner.OffBallPlan = new OffBallPlan(ActionKind.MakeForwardRun, new Vec2(85, 30), null, TeamSide.Home, UntilTick: 1000);
        return (s, runner);
    }

    private static void Decide(Scenario s) =>
        new OffBallDecisionSystem(new OffBallCandidateGenerator(AiTuning.Default.OffBall), new UtilitySelector()).Apply(s.Context);

    [Fact]
    public void A_runner_waits_at_the_line_until_the_pass_is_played()
    {
        var (s, runner) = Runner(awareness: 10);

        Decide(s);

        Assert.Equal(70 + AiTuning.Default.OffBall.RunHoldOffset(10), runner.MovementTarget.X, 9);
        Assert.True(runner.MovementTarget.X < 70, "an aware runner waits onside");
    }

    [Fact]
    public void A_runner_goes_beyond_the_line_once_a_teammate_has_passed()
    {
        var (s, runner) = Runner(awareness: 10);
        var passer = s.State.Player(s.State.PossessionPlayerId!.Value);
        PassExecution.Launch(s.Context, new PassResult(PassOutcome.Completed, TeamSide.Home, passer.PlayerId,
            s.State.Slot(TeamSide.Home, PlayingPosition.LW).PlayerId, null, passer.Position, new Vec2(60, 50), new Vec2(60, 50), 2.0, 0.9));
        runner.MovementIntensity = MovementIntensity.Jog;

        Decide(s);

        Assert.Equal(new Vec2(85, 30), runner.MovementTarget);
    }

    [Fact]
    public void Attacking_shape_stops_at_the_second_last_opponent()
    {
        var s = Attack();
        s.Place(TeamSide.Away, PlayingPosition.GK, 60, 34); // keeper out: the deepest defender is now second-last

        Assert.Equal(70, TeamShapeSystem.OffsideLineX(s.State, TeamSide.Home));
    }
}
