using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Crossing;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.AI;

public class CrossDecisionTests
{
    private static readonly OnBallCandidateGenerator Generator =
        new(AiTuning.Default.OnBall, new PassResolver(), new CarryResolver(), new ShotResolver());

    private static ActionCandidate? Cross(Scenario s, PlayerMatchState carrier) =>
        Generator.Generate(s.State, carrier, heldSeconds: 1).SingleOrDefault(c => c.Kind == ActionKind.Cross);

    [Fact]
    public void Crossing_is_offered_only_out_wide_in_the_final_third()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Home, PlayingPosition.LW, 90, 62);
        s.GiveBall(TeamSide.Home, PlayingPosition.LW);
        Assert.NotNull(Cross(s, carrier));

        carrier.Position = new Vec2(90, 40); // inside the width of the box
        Assert.Null(Cross(s, carrier));

        carrier.Position = new Vec2(60, 62); // before the final third
        Assert.Null(Cross(s, carrier));
    }

    [Fact]
    public void Crossing_is_offered_to_the_away_side_out_wide_in_its_final_third()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Away, PlayingPosition.LW, 15, 6); // team frame (90, 62)
        s.GiveBall(TeamSide.Away, PlayingPosition.LW);
        Assert.NotNull(Cross(s, carrier));
    }

    [Fact]
    public void Attackers_in_the_box_make_a_cross_more_attractive_and_defenders_less()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Home, PlayingPosition.LW, 90, 62);
        s.GiveBall(TeamSide.Home, PlayingPosition.LW);
        foreach (var away in s.State.TeamPlayers(TeamSide.Away).Where(p => !p.Role.IsGoalkeeper()))
            away.Position = new Vec2(104, 2); // the parked defenders already crowd the box (the count is capped)
        var empty = Cross(s, carrier)!.Utility;

        s.Place(TeamSide.Home, PlayingPosition.ST, 95, 34);
        var withStriker = Cross(s, carrier)!.Utility;
        Assert.True(withStriker > empty);

        s.Place(TeamSide.Away, PlayingPosition.LCB, 98, 30);
        Assert.True(Cross(s, carrier)!.Utility < withStriker);
    }

    [Fact]
    public void An_attacker_and_a_defender_chase_a_cross()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LW, 94, 64);
        var striker = s.Place(TeamSide.Home, PlayingPosition.ST, 90, 30);
        var defender = s.Place(TeamSide.Away, PlayingPosition.LCB, 99, 36);
        var crosser = s.GiveBall(TeamSide.Home, PlayingPosition.LW);
        var result = new CrossResolver().Resolve(s.State, new CrossIntent(crosser.PlayerId), s.Context.Random);
        CrossExecution.Launch(s.Context, result);

        new TeamShapeSystem().Apply(s.State);

        Assert.Equal(result.TargetPosition, striker.MovementTarget);
        Assert.Equal(MovementIntensity.Sprint, striker.MovementIntensity);
        Assert.Equal(result.TargetPosition, defender.MovementTarget);
        Assert.Equal(MovementIntensity.Sprint, defender.MovementIntensity);
    }
}
