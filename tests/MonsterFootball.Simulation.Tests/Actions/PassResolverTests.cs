using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class PassResolverTests
{
    private readonly PassResolver _resolver = new();

    private static PassIntent Intent(Scenario s, PlayingPosition from, PlayingPosition to) =>
        new(s.State.Slot(TeamSide.Home, from).PlayerId, s.State.Slot(TeamSide.Home, to).PlayerId);

    private static PlayerStats WithPassing(int passing) => new(5, passing, 5, 5, 5, 5, 5, 5, 5);

    /// <summary>Short, unpressured pass from CM to LCM.</summary>
    private static Scenario EasyPass(PlayerStats? homeStats = null)
    {
        var s = new Scenario(homeStats);
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        s.Place(TeamSide.Home, PlayingPosition.LCM, 50, 40);
        return s;
    }

    /// <summary>Long pass from a pressed CM to the ST, with a defender near the lane.</summary>
    private static Scenario HardPass()
    {
        var s = new Scenario();
        s.Place(TeamSide.Home, PlayingPosition.CM, 30, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 75, 40);
        s.Place(TeamSide.Away, PlayingPosition.ST, 31, 35);   // pressing the passer
        s.Place(TeamSide.Away, PlayingPosition.CM, 55, 39);   // near the lane
        return s;
    }

    private double CompletionRate(Scenario s, PassIntent intent, int trials = 5000)
    {
        var random = new MatchRng(2024);
        var completed = Enumerable.Range(0, trials)
            .Count(_ => _resolver.Resolve(s.State, intent, random).Outcome == PassOutcome.Completed);
        return completed / (double)trials;
    }

    [Fact]
    public void Easy_passes_complete_more_often_than_difficult_ones()
    {
        var easy = EasyPass();
        var hard = HardPass();

        var easyRate = CompletionRate(easy, Intent(easy, PlayingPosition.CM, PlayingPosition.LCM));
        var hardRate = CompletionRate(hard, Intent(hard, PlayingPosition.CM, PlayingPosition.ST));

        Assert.True(easyRate > 0.9, $"easy {easyRate:P1}");
        Assert.True(easyRate - hardRate > 0.2, $"easy {easyRate:P1} vs hard {hardRate:P1}");
    }

    [Fact]
    public void Better_passers_are_more_accurate()
    {
        var good = EasyPass(WithPassing(9));
        var poor = EasyPass(WithPassing(2));

        var goodAccuracy = _resolver.Estimate(good.State, Intent(good, PlayingPosition.CM, PlayingPosition.LCM)).AccuracyProbability;
        var poorAccuracy = _resolver.Estimate(poor.State, Intent(poor, PlayingPosition.CM, PlayingPosition.LCM)).AccuracyProbability;

        Assert.True(goodAccuracy > poorAccuracy);
    }

    [Fact]
    public void Technique_and_awareness_improve_accuracy()
    {
        var skilled = EasyPass(new PlayerStats(9, 5, 5, 5, 5, 5, 5, 9, 5));
        var raw = EasyPass(new PlayerStats(2, 5, 5, 5, 5, 5, 5, 2, 5));

        Assert.True(
            _resolver.Estimate(skilled.State, Intent(skilled, PlayingPosition.CM, PlayingPosition.LCM)).Ability >
            _resolver.Estimate(raw.State, Intent(raw, PlayingPosition.CM, PlayingPosition.LCM)).Ability);
    }

    [Fact]
    public void Longer_passes_are_less_accurate()
    {
        var s = new Scenario();
        s.Place(TeamSide.Home, PlayingPosition.CM, 30, 34);
        s.Place(TeamSide.Home, PlayingPosition.LCM, 40, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 80, 34);

        var shortPass = _resolver.Estimate(s.State, Intent(s, PlayingPosition.CM, PlayingPosition.LCM));
        var longPass = _resolver.Estimate(s.State, Intent(s, PlayingPosition.CM, PlayingPosition.ST));

        Assert.True(shortPass.AccuracyProbability > longPass.AccuracyProbability);
    }

    [Fact]
    public void Pressure_on_the_passer_reduces_accuracy()
    {
        var calm = EasyPass();
        var pressed = EasyPass();
        pressed.Place(TeamSide.Away, PlayingPosition.ST, 41, 34);

        var calmEstimate = _resolver.Estimate(calm.State, Intent(calm, PlayingPosition.CM, PlayingPosition.LCM));
        var pressedEstimate = _resolver.Estimate(pressed.State, Intent(pressed, PlayingPosition.CM, PlayingPosition.LCM));

        Assert.True(pressedEstimate.PressurePenalty > 0);
        Assert.True(pressedEstimate.AccuracyProbability < calmEstimate.AccuracyProbability);
    }

    [Fact]
    public void Opponent_in_the_lane_is_a_threat_but_opponents_behind_or_far_away_are_not()
    {
        var s = EasyPass();
        var inLane = s.Place(TeamSide.Away, PlayingPosition.CM, 45, 37);
        s.Place(TeamSide.Away, PlayingPosition.ST, 35, 34);   // behind the passer
        s.Place(TeamSide.Away, PlayingPosition.LW, 45, 60);   // far from the lane

        var threats = _resolver.Estimate(s.State, Intent(s, PlayingPosition.CM, PlayingPosition.LCM)).Threats;

        var threat = Assert.Single(threats);
        Assert.Equal(inLane.PlayerId, threat.Opponent);
    }

    [Fact]
    public void Marked_receiver_is_riskier_than_open_receiver()
    {
        var open = EasyPass();
        var marked = EasyPass();
        marked.Place(TeamSide.Away, PlayingPosition.RB, 51, 41);

        Assert.True(
            _resolver.Estimate(marked.State, Intent(marked, PlayingPosition.CM, PlayingPosition.LCM)).SuccessProbability <
            _resolver.Estimate(open.State, Intent(open, PlayingPosition.CM, PlayingPosition.LCM)).SuccessProbability);
    }

    [Fact]
    public void Better_defenders_are_likelier_to_intercept()
    {
        double ThreatWith(int defending)
        {
            var s = new Scenario(awayStats: new PlayerStats(5, 5, 5, defending, 5, 5, 5, 5, 5));
            s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
            s.Place(TeamSide.Home, PlayingPosition.LCM, 50, 40);
            s.Place(TeamSide.Away, PlayingPosition.CM, 45, 38);
            return new PassResolver().Estimate(s.State, Intent(s, PlayingPosition.CM, PlayingPosition.LCM)).Threats.Single().Probability;
        }

        Assert.True(ThreatWith(9) > ThreatWith(2));
    }

    [Fact]
    public void Threats_are_ordered_earliest_along_the_lane()
    {
        var s = new Scenario();
        s.Place(TeamSide.Home, PlayingPosition.CM, 30, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 60, 34);
        s.Place(TeamSide.Away, PlayingPosition.CM, 50, 35);
        s.Place(TeamSide.Away, PlayingPosition.LCM, 38, 35);

        var threats = _resolver.Estimate(s.State, Intent(s, PlayingPosition.CM, PlayingPosition.ST)).Threats;

        Assert.Equal(2, threats.Count);
        Assert.True(threats[0].AlongLane < threats[1].AlongLane);
    }

    [Fact]
    public void Scripted_rolls_produce_each_outcome_with_presentation_data()
    {
        var s = EasyPass();
        s.Place(TeamSide.Away, PlayingPosition.CM, 45, 37);
        var intent = Intent(s, PlayingPosition.CM, PlayingPosition.LCM);
        var start = s.State.Player(intent.Passer).Position;
        var receiverAt = s.State.Player(intent.Receiver).Position;

        var completed = _resolver.Resolve(s.State, intent, new ScriptedRandom(0.0, 0.999));
        Assert.Equal(PassOutcome.Completed, completed.Outcome);
        Assert.Equal(intent.Receiver, completed.FinalReceiver);
        Assert.Equal(start, completed.StartPosition);
        Assert.Equal(receiverAt, completed.TargetPosition);
        Assert.True(completed.TravelDuration > 0);
        Assert.InRange(completed.Quality, 0, 1);

        var intercepted = _resolver.Resolve(s.State, intent, new ScriptedRandom(0.0, 0.0));
        Assert.Equal(PassOutcome.Intercepted, intercepted.Outcome);
        Assert.Equal(s.State.Slot(TeamSide.Away, PlayingPosition.CM).PlayerId, intercepted.FinalReceiver);
        Assert.True(intercepted.TravelDuration < completed.TravelDuration);

        var misplaced = _resolver.Resolve(s.State, intent, new ScriptedRandom(0.9999999, 0.75, 0.5, 0.5));
        Assert.Equal(PassOutcome.Misplaced, misplaced.Outcome);
        Assert.Null(misplaced.FinalReceiver);
        Assert.NotEqual(receiverAt, misplaced.TargetPosition);
        Assert.True(s.State.Pitch.Contains(misplaced.TargetPosition));
    }

    [Fact]
    public void Passing_to_an_opponent_is_rejected()
    {
        var s = EasyPass();
        var intent = new PassIntent(s.State.Slot(TeamSide.Home, PlayingPosition.CM).PlayerId,
            s.State.Slot(TeamSide.Away, PlayingPosition.CM).PlayerId);

        Assert.Throws<ArgumentException>(() => _resolver.Estimate(s.State, intent));
    }

    [Fact]
    public void Same_seed_resolves_identically()
    {
        var s = HardPass();
        var intent = Intent(s, PlayingPosition.CM, PlayingPosition.ST);
        var a = new MatchRng(5);
        var b = new MatchRng(5);

        for (var i = 0; i < 200; i++)
            Assert.Equal(_resolver.Resolve(s.State, intent, a), _resolver.Resolve(s.State, intent, b));
    }
}
