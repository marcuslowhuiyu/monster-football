using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.AI;

public class ClearanceDecisionTests
{
    private static readonly OnBallCandidateGenerator Generator =
        new(AiTuning.Default.OnBall, new PassResolver(), new CarryResolver(), new ShotResolver());

    private static ActionCandidate? Clear(Scenario s, PlayerMatchState carrier) =>
        Generator.Generate(s.State, carrier, heldSeconds: 1).SingleOrDefault(c => c.Kind == ActionKind.Clear);

    [Fact]
    public void The_defensive_third_ends_a_third_of_the_way_up_the_pitch() =>
        Assert.Equal(35, new Scenario().State.Zones.DefensiveThirdX, precision: 9);

    [Fact]
    public void Clearing_is_offered_only_in_the_defensive_third()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
        Assert.NotNull(Clear(s, carrier));

        carrier.Position = new Vec2(50, 34);
        Assert.Null(Clear(s, carrier));
    }

    [Fact]
    public void Clearing_is_offered_to_the_away_side_in_its_own_third()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Away, PlayingPosition.LCB, 85, 34); // team frame X = 20
        s.GiveBall(TeamSide.Away, PlayingPosition.LCB);
        Assert.NotNull(Clear(s, carrier));
    }

    [Fact]
    public void Pressure_and_danger_make_clearing_more_attractive()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
        var calm = Clear(s, carrier)!.Utility;

        s.Place(TeamSide.Away, PlayingPosition.ST, 21, 34);
        var pressed = Clear(s, carrier)!.Utility;
        Assert.True(pressed > calm);

        s.Place(TeamSide.Away, PlayingPosition.ST, 104, 50); // away again
        carrier.Position = new Vec2(8, 34);
        var deep = Clear(s, carrier)!.Utility;
        Assert.True(deep > calm);
    }

    [Fact]
    public void Unpressed_with_a_safe_pass_the_pass_scores_higher()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Home, PlayingPosition.LCB, 25, 34);
        s.Place(TeamSide.Home, PlayingPosition.LCM, 35, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var candidates = Generator.Generate(s.State, carrier, heldSeconds: 1);
        var bestPass = candidates.Where(c => c.Kind == ActionKind.Pass).Max(c => c.Utility);
        Assert.True(bestPass > candidates.Single(c => c.Kind == ActionKind.Clear).Utility);
    }

    [Fact]
    public void The_nearest_outfield_player_of_each_side_chases_a_clearance()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        var homeChaser = s.Place(TeamSide.Home, PlayingPosition.ST, 50, 40);
        s.Place(TeamSide.Home, PlayingPosition.RW, 60, 10);
        var awayChaser = s.Place(TeamSide.Away, PlayingPosition.LCB, 58, 30);
        s.Place(TeamSide.Away, PlayingPosition.LB, 70, 60);
        var kicker = s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
        var result = new ClearanceResolver().Resolve(s.State, new ClearanceIntent(kicker.PlayerId), s.Context.Random);
        ClearanceExecution.Launch(s.Context, result);

        new TeamShapeSystem().Apply(s.State);

        Assert.Equal(result.TargetPosition, homeChaser.MovementTarget);
        Assert.Equal(MovementIntensity.Sprint, homeChaser.MovementIntensity);
        Assert.Equal(result.TargetPosition, awayChaser.MovementTarget);
        Assert.Equal(MovementIntensity.Sprint, awayChaser.MovementIntensity);
    }
}
