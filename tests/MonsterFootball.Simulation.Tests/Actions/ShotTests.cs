using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class ShotTests
{
    private readonly ShotResolver _resolver = new();

    /// <summary>Home ST with the ball at (x, y); Away keeper on their ideal spot near the goal.</summary>
    private static Scenario Chance(double x, double y, PlayerStats? homeStats = null, PlayerStats? awayStats = null, IMatchRandom? random = null)
    {
        var s = new Scenario(homeStats, awayStats, random);
        s.Place(TeamSide.Home, PlayingPosition.ST, x, y);
        s.Place(TeamSide.Away, PlayingPosition.GK, 103, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.ST);
        return s;
    }

    private static ShotIntent Intent(Scenario s) => new(s.State.Slot(TeamSide.Home, PlayingPosition.ST).PlayerId);

    private ShotEstimate Estimate(Scenario s) => _resolver.Estimate(s.State, Intent(s));

    private double GoalRate(Scenario s, int trials = 5000)
    {
        var random = new MatchRng(11);
        return Enumerable.Range(0, trials).Count(_ => _resolver.Resolve(s.State, Intent(s), random).Outcome == ShotOutcome.Goal) / (double)trials;
    }

    [Fact]
    public void Close_shots_score_more_often_than_long_shots()
    {
        Assert.True(GoalRate(Chance(97, 34)) - GoalRate(Chance(77, 34)) > 0.15);
    }

    [Fact]
    public void Distance_lowers_on_target_probability()
    {
        Assert.True(Estimate(Chance(95, 34)).OnTargetProbability > Estimate(Chance(80, 34)).OnTargetProbability);
    }

    [Fact]
    public void Tight_angles_lower_on_target_probability()
    {
        var central = Estimate(Chance(95, 34));
        var tight = Estimate(Chance(103, 52));
        Assert.True(tight.AnglePenalty > central.AnglePenalty);
        Assert.True(tight.OnTargetProbability < central.OnTargetProbability);
    }

    [Fact]
    public void Pressure_lowers_on_target_probability()
    {
        var calm = Chance(90, 34);
        var pressed = Chance(90, 34);
        pressed.Place(TeamSide.Away, PlayingPosition.LCB, 91, 34);

        Assert.True(Estimate(pressed).PressurePenalty > 0);
        Assert.True(Estimate(pressed).OnTargetProbability < Estimate(calm).OnTargetProbability);
    }

    [Fact]
    public void Finishing_technique_and_power_improve_the_shooter()
    {
        var baseline = Estimate(Chance(88, 34)).Ability;
        Assert.True(Estimate(Chance(88, 34, new PlayerStats(5, 5, 8, 5, 5, 5, 5, 5, 5))).Ability > baseline);
        Assert.True(Estimate(Chance(88, 34, new PlayerStats(8, 5, 5, 5, 5, 5, 5, 5, 5))).Ability > baseline);
        Assert.True(Estimate(Chance(88, 34, new PlayerStats(5, 5, 5, 5, 5, 8, 5, 5, 5))).Ability > baseline);
    }

    [Fact]
    public void Better_keepers_save_more()
    {
        var good = Estimate(Chance(90, 34, awayStats: new PlayerStats(5, 5, 5, 5, 5, 5, 5, 5, 9)));
        var poor = Estimate(Chance(90, 34, awayStats: new PlayerStats(5, 5, 5, 5, 5, 5, 5, 5, 2)));
        Assert.True(good.SaveProbability > poor.SaveProbability);
    }

    [Fact]
    public void Keeper_awareness_helps_saves()
    {
        var aware = Estimate(Chance(90, 34, awayStats: new PlayerStats(5, 5, 5, 5, 5, 5, 5, 9, 5)));
        var unaware = Estimate(Chance(90, 34, awayStats: new PlayerStats(5, 5, 5, 5, 5, 5, 5, 2, 5)));
        Assert.True(aware.SaveProbability > unaware.SaveProbability);
    }

    [Fact]
    public void Out_of_position_keeper_saves_less()
    {
        var set = Chance(90, 34);
        var stranded = Chance(90, 34);
        stranded.Place(TeamSide.Away, PlayingPosition.GK, 100, 50);

        Assert.True(Estimate(stranded).SaveProbability < Estimate(set).SaveProbability);
    }

    [Fact]
    public void Goal_increments_the_score_exactly_once_and_starts_a_restart()
    {
        var s = Chance(95, 34, random: new ScriptedRandom(0.0, 0.5, 0.9999));
        var result = _resolver.Resolve(s.State, Intent(s), s.Context.Random);
        Assert.Equal(ShotOutcome.Goal, result.Outcome);

        ShotExecution.Apply(s.Context, result);

        Assert.Equal(1, s.State.Score.Home);
        Assert.Equal(0, s.State.Score.Away);
        Assert.Equal(MatchPhase.GoalRestart, s.State.Phase);
        Assert.Equal(TeamSide.Away, s.State.KickoffTeam);
        Assert.Equal([MatchEventType.ShotTaken, MatchEventType.GoalScored], s.EventTypes);
        Assert.Equal(s.State.Pitch.Length, result.TargetPosition.X);
        Assert.InRange(result.TargetPosition.Y, 34 - PitchGeometry.GoalWidth / 2, 34 + PitchGeometry.GoalWidth / 2);
    }

    [Fact]
    public void Saved_shot_does_not_score_and_the_keeper_takes_the_ball()
    {
        var s = Chance(95, 34, random: new ScriptedRandom(0.0, 0.5, 0.0));
        var keeper = s.State.Slot(TeamSide.Away, PlayingPosition.GK);

        ShotExecution.Apply(s.Context, _resolver.Resolve(s.State, Intent(s), s.Context.Random));

        Assert.Equal(0, s.State.Score.Home);
        Assert.True(s.State.HasBall(keeper.PlayerId));
        Assert.Equal([MatchEventType.ShotTaken, MatchEventType.ShotSaved, MatchEventType.PossessionChanged], s.EventTypes);
    }

    [Fact]
    public void Missed_shot_does_not_score_goes_wide_and_the_keeper_restarts()
    {
        var s = Chance(95, 34, random: new ScriptedRandom(0.9999, 0.2, 0.5));
        var keeper = s.State.Slot(TeamSide.Away, PlayingPosition.GK);
        var result = _resolver.Resolve(s.State, Intent(s), s.Context.Random);

        ShotExecution.Apply(s.Context, result);

        Assert.Equal(ShotOutcome.Missed, result.Outcome);
        Assert.Equal(0, s.State.Score.Home);
        Assert.True(Math.Abs(result.TargetPosition.Y - 34) > PitchGeometry.GoalWidth / 2);
        Assert.True(s.State.HasBall(keeper.PlayerId));
        Assert.Equal([MatchEventType.ShotTaken, MatchEventType.ShotMissed, MatchEventType.PossessionChanged], s.EventTypes);
    }

    [Fact]
    public void Shot_result_carries_presentation_data()
    {
        var s = Chance(90, 30);
        var result = _resolver.Resolve(s.State, Intent(s), new MatchRng(3));

        Assert.Equal(new Vec2(90, 30), result.StartPosition);
        Assert.True(result.TravelDuration > 0);
        Assert.InRange(result.Quality, 0, 1);
        Assert.InRange(result.GoalProbability, 0, 1);
    }

    [Fact]
    public void Cannot_shoot_without_the_ball()
    {
        var s = Chance(95, 34);
        var other = s.State.Slot(TeamSide.Home, PlayingPosition.LW);
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(s.State, new ShotIntent(other.PlayerId), new MatchRng(1)));
    }
}
