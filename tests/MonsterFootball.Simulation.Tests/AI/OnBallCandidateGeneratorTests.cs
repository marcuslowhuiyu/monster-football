using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.AI;

public class OnBallCandidateGeneratorTests
{
    private static readonly OnBallWeights Weights = AiTuning.Default.OnBall;

    private static OnBallCandidateGenerator Generator() =>
        new(Weights, new PassResolver(), new CarryResolver(), new ShotResolver());

    private static Scenario CarrierAt(TeamSide side, PlayingPosition slot, double x, double y)
    {
        var s = new Scenario();
        s.Place(side, slot, x, y);
        s.GiveBall(side, slot);
        return s;
    }

    private static IReadOnlyList<ActionCandidate> Candidates(Scenario s, PlayerMatchState carrier, double heldSeconds = 1.0) =>
        Generator().Generate(s.State, carrier, heldSeconds);

    private static double Best(IEnumerable<ActionCandidate> candidates, ActionKind kind) =>
        candidates.Where(c => c.Kind == kind).Select(c => c.Utility).DefaultIfEmpty(double.NegativeInfinity).Max();

    [Fact]
    public void Generates_hold_pass_carry_and_shoot_for_an_attacker_near_goal()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.ST, 90, 34);
        s.Place(TeamSide.Home, PlayingPosition.LW, 85, 50);
        s.Place(TeamSide.Away, PlayingPosition.GK, 103, 34);

        var kinds = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.ST)).Select(c => c.Kind).ToHashSet();

        Assert.Equal([ActionKind.Hold, ActionKind.Pass, ActionKind.Carry, ActionKind.Shoot], kinds.Order());
    }

    [Fact]
    public void One_pass_candidate_per_teammate_far_enough_away()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var tooClose = s.Place(TeamSide.Home, PlayingPosition.LCM, 41, 35);

        var passes = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.CM))
            .Where(c => c.Kind == ActionKind.Pass)
            .Select(c => ((PassIntent)c.Intent!).Receiver)
            .ToList();

        Assert.Equal(9, passes.Count); // 10 teammates minus the one too close
        Assert.DoesNotContain(tooClose.PlayerId, passes);
    }

    [Fact]
    public void Goalkeepers_hold_pass_or_clear_but_never_shoot_or_carry()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.GK, 5, 34);
        var kinds = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.GK)).Select(c => c.Kind).Distinct();
        Assert.Equal([ActionKind.Hold, ActionKind.Pass, ActionKind.Clear], kinds.Order());
    }

    [Fact]
    public void Hopeless_shots_are_not_offered()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        Assert.DoesNotContain(Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.CM)), c => c.Kind == ActionKind.Shoot);
    }

    [Fact]
    public void Shot_utility_rises_smoothly_with_expected_goals()
    {
        double ShotUtility(double x)
        {
            var s = CarrierAt(TeamSide.Home, PlayingPosition.ST, x, 34);
            s.Place(TeamSide.Away, PlayingPosition.GK, 103, 34);
            return Best(Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.ST)), ActionKind.Shoot);
        }

        var utilities = new[] { 82.0, 86, 90, 94, 98 }.Select(ShotUtility).ToList();
        for (var i = 1; i < utilities.Count; i++)
            Assert.True(utilities[i] > utilities[i - 1], $"closer shot should score higher: {string.Join(", ", utilities)}");
    }

    [Fact]
    public void Holding_loses_value_with_time_and_under_pressure()
    {
        var calm = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var carrier = calm.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var early = Best(Candidates(calm, carrier, 0.5), ActionKind.Hold);
        var late = Best(Candidates(calm, carrier, 3.0), ActionKind.Hold);

        var pressed = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        pressed.Place(TeamSide.Away, PlayingPosition.CM, 41, 34);
        var underPressure = Best(Candidates(pressed, pressed.State.Slot(TeamSide.Home, PlayingPosition.CM), 0.5), ActionKind.Hold);

        Assert.True(late < early);
        Assert.True(underPressure < early);
    }

    [Fact]
    public void Open_receiver_is_preferred_to_a_marked_one()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var open = s.Place(TeamSide.Home, PlayingPosition.LCM, 52, 44);
        var marked = s.Place(TeamSide.Home, PlayingPosition.RCM, 52, 24);
        s.Place(TeamSide.Away, PlayingPosition.CM, 52.5, 23.5);

        var passes = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.CM)).Where(c => c.Kind == ActionKind.Pass).ToList();
        double To(PlayerMatchState r) => passes.Single(c => ((PassIntent)c.Intent!).Receiver == r.PlayerId).Utility;

        Assert.True(To(open) > To(marked));
    }

    [Fact]
    public void Forward_pass_is_preferred_to_an_equally_safe_backward_one()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var forward = s.Place(TeamSide.Home, PlayingPosition.ST, 55, 34);
        var backward = s.Place(TeamSide.Home, PlayingPosition.LCB, 25, 34);

        var passes = Candidates(s, s.State.Slot(TeamSide.Home, PlayingPosition.CM)).Where(c => c.Kind == ActionKind.Pass).ToList();
        double To(PlayerMatchState r) => passes.Single(c => ((PassIntent)c.Intent!).Receiver == r.PlayerId).Utility;

        Assert.True(To(forward) > To(backward));
    }

    [Fact]
    public void Away_team_progression_is_toward_decreasing_x()
    {
        var s = CarrierAt(TeamSide.Away, PlayingPosition.CM, 60, 34);
        var forward = s.Place(TeamSide.Away, PlayingPosition.ST, 45, 34);
        var backward = s.Place(TeamSide.Away, PlayingPosition.LCB, 75, 34);

        var passes = Candidates(s, s.State.Slot(TeamSide.Away, PlayingPosition.CM)).Where(c => c.Kind == ActionKind.Pass).ToList();
        double To(PlayerMatchState r) => passes.Single(c => ((PassIntent)c.Intent!).Receiver == r.PlayerId).Utility;

        Assert.True(To(forward) > To(backward));
    }

    [Fact]
    public void Carrying_into_space_beats_carrying_into_a_defender()
    {
        var open = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var blocked = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        blocked.Place(TeamSide.Away, PlayingPosition.CM, 42, 34);

        Assert.True(
            Best(Candidates(open, open.State.Slot(TeamSide.Home, PlayingPosition.CM)), ActionKind.Carry) >
            Best(Candidates(blocked, blocked.State.Slot(TeamSide.Home, PlayingPosition.CM)), ActionKind.Carry));
    }

    [Fact]
    public void No_new_carry_is_offered_while_already_carrying()
    {
        var s = CarrierAt(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var intent = new CarryIntent(carrier.PlayerId, new Vec2(50, 34));
        CarrySystem.Start(s.Context, intent, new CarryResolver().Resolve(s.State, intent));

        Assert.DoesNotContain(Candidates(s, carrier), c => c.Kind == ActionKind.Carry);
    }
}
