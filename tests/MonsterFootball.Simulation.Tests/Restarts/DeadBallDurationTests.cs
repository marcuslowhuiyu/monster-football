using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Restarts;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Restarts;

/// <summary>How long the ball stays dead after each kind of stoppage (ADR-037).</summary>
public class DeadBallDurationTests
{
    private static readonly DeadBallTuning Tuning = DeadBallTuning.Default;

    private static Stoppage Stop(StoppageReason reason, RestartType restart, TeamSide awarded, Vec2 location,
        DisciplinaryOutcome discipline = DisciplinaryOutcome.None) =>
        new(reason, restart, awarded, location, 0) { Discipline = discipline };

    private static DeadBallTime Compute(Scenario s, Stoppage stoppage, params double[] draws) =>
        DeadBallDuration.Compute(s.State, stoppage, s.Context.Config, new ScriptedRandom(draws));

    private static double Spread(double draw) => Tuning.SpreadMin + (Tuning.SpreadMax - Tuning.SpreadMin) * draw;

    public static TheoryData<StoppageReason, RestartType, double> Averages => new()
    {
        { StoppageReason.BallOutOfPlay, RestartType.ThrowIn, DeadBallTuning.Default.ThrowInSeconds },
        { StoppageReason.BallOutOfPlay, RestartType.GoalKick, DeadBallTuning.Default.GoalKickSeconds },
        { StoppageReason.BallOutOfPlay, RestartType.Corner, DeadBallTuning.Default.CornerSeconds },
        { StoppageReason.Foul, RestartType.DirectFreeKick, DeadBallTuning.Default.DirectFreeKickSeconds },
        { StoppageReason.Offside, RestartType.IndirectFreeKick, DeadBallTuning.Default.IndirectFreeKickSeconds },
        { StoppageReason.Goal, RestartType.Kickoff, DeadBallTuning.Default.GoalSeconds },
        { StoppageReason.Foul, RestartType.Penalty, DeadBallTuning.Default.PenaltySeconds },
    };

    [Theory]
    [MemberData(nameof(Averages))]
    public void Each_stoppage_type_draws_around_its_average(StoppageReason reason, RestartType restart, double average)
    {
        var s = new Scenario();
        // Middle of the pitch (no location factor), level score, first minute (no game-state effect).
        var stoppage = Stop(reason, restart, TeamSide.Home, s.State.Pitch.Centre);

        Assert.Equal(average * Spread(0), Compute(s, stoppage, 0).Seconds, 9);
        Assert.Equal(average * Spread(0.9999), Compute(s, stoppage, 0.9999).Seconds, 9);
        Assert.Equal(0, Compute(s, stoppage, 0.5).WastedSeconds);
    }

    [Theory]
    [InlineData(StoppageReason.MatchStart)]
    [InlineData(StoppageReason.PeriodStart)]
    public void Opening_kickoffs_have_no_dead_ball_time_and_draw_nothing(StoppageReason reason)
    {
        var s = new Scenario();

        var time = Compute(s, Stop(reason, RestartType.Kickoff, TeamSide.Home, s.State.Pitch.Centre)); // throws on any draw

        Assert.Equal(default, time);
    }

    [Theory]
    [InlineData(1, 0, 0)]          // first half, kickoff
    [InlineData(1, 2700, 0)]       // half time
    [InlineData(2, 900, 0)]        // minute 60
    [InlineData(2, 1800, 0.5)]     // minute 75
    [InlineData(2, 2700, 1)]       // minute 90
    [InlineData(2, 3000, 1)]       // added time
    [InlineData(1, 3000, 0)]       // first-half added time counts as minute 45
    public void Lateness_rises_from_minute_60_to_minute_90(int period, double elapsed, double expected)
    {
        Assert.Equal(expected, DeadBallDuration.Lateness(period, elapsed, 2700, 2, Tuning.GameStateFromFraction), 9);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 1.3)]
    [InlineData(2, 1, 1.6)]
    [InlineData(4, 1, 1.6)]
    [InlineData(-1, 1, 0.8)]
    [InlineData(-2, 1, 0.6)]
    [InlineData(-3, 1, 0.6)]
    [InlineData(2, 0.5, 1.3)]
    [InlineData(2, 0, 1)]
    public void Game_state_slows_a_leading_side_and_hurries_a_trailing_one(int margin, double lateness, double expected)
    {
        Assert.Equal(expected, DeadBallDuration.GameStateFactor(margin, lateness, Tuning), 9);
    }

    /// <summary>A match run to the end of regular time in the second half: lateness 1.</summary>
    private static Scenario LateInTheMatch()
    {
        var s = new Scenario();
        for (var i = 0; i < s.State.Clock.RegularPeriodTicks; i++)
            s.State.Clock.Advance();
        s.State.Clock.StartNextPeriod();
        for (var i = 0; i < s.State.Clock.RegularPeriodTicks; i++)
            s.State.Clock.Advance();
        return s;
    }

    [Fact]
    public void A_side_two_up_late_on_slows_its_restarts_and_the_slow_down_is_wasted_time()
    {
        var s = LateInTheMatch();
        s.State.Score.AddGoal(TeamSide.Home);
        s.State.Score.AddGoal(TeamSide.Home);

        var time = Compute(s, Stop(StoppageReason.BallOutOfPlay, RestartType.GoalKick, TeamSide.Home, new Vec2(5.5, 34)), 0.5);

        var paced = Tuning.GoalKickSeconds * Spread(0.5);
        Assert.Equal(paced * (1 + Tuning.MaxSlowdown), time.Seconds, 9);
        Assert.Equal(paced * Tuning.MaxSlowdown, time.WastedSeconds, 9);
    }

    [Fact]
    public void Game_state_is_judged_for_the_side_taking_the_restart()
    {
        var s = LateInTheMatch();
        s.State.Score.AddGoal(TeamSide.Home);
        s.State.Score.AddGoal(TeamSide.Home);

        var time = Compute(s, Stop(StoppageReason.BallOutOfPlay, RestartType.GoalKick, TeamSide.Away, new Vec2(99.5, 34)), 0.5);

        Assert.Equal(Tuning.GoalKickSeconds * Spread(0.5) * (1 - Tuning.MaxHurry), time.Seconds, 9);
        Assert.Equal(0, time.WastedSeconds);
    }

    [Theory]
    [InlineData(TeamSide.Home, 80, 34, true)]
    [InlineData(TeamSide.Home, 50, 34, false)]
    [InlineData(TeamSide.Away, 25, 34, true)]
    [InlineData(TeamSide.Away, 55, 34, false)]
    public void Free_kicks_in_the_attacking_third_take_longer(TeamSide awarded, double x, double y, bool attackingThird)
    {
        var s = new Scenario();
        var location = new Vec2(x, y);

        var direct = Compute(s, Stop(StoppageReason.Foul, RestartType.DirectFreeKick, awarded, location), 0.5).Seconds;
        var indirect = Compute(s, Stop(StoppageReason.Offside, RestartType.IndirectFreeKick, awarded, location), 0.5).Seconds;

        var factor = attackingThird ? Tuning.AttackingFreeKickFactor : 1;
        Assert.Equal(Tuning.DirectFreeKickSeconds * Spread(0.5) * factor, direct, 9);
        Assert.Equal(Tuning.IndirectFreeKickSeconds * Spread(0.5) * factor, indirect, 9);
    }

    [Theory]
    [InlineData(TeamSide.Home, 80, true)]
    [InlineData(TeamSide.Home, 50, false)]
    [InlineData(TeamSide.Away, 25, true)]
    [InlineData(TeamSide.Away, 55, false)]
    public void Throw_ins_in_the_attacking_third_take_longer(TeamSide awarded, double x, bool attackingThird)
    {
        var s = new Scenario();

        var seconds = Compute(s, Stop(StoppageReason.BallOutOfPlay, RestartType.ThrowIn, awarded, new Vec2(x, 0)), 0.5).Seconds;

        Assert.Equal(Tuning.ThrowInSeconds * Spread(0.5) * (attackingThird ? Tuning.AttackingThrowInFactor : 1), seconds, 9);
    }

    [Theory]
    [InlineData(DisciplinaryOutcome.None, 0)]
    [InlineData(DisciplinaryOutcome.Yellow, 30)]
    [InlineData(DisciplinaryOutcome.SecondYellow, 45)]
    [InlineData(DisciplinaryOutcome.Red, 45)]
    public void Cards_add_time_to_the_stoppage(DisciplinaryOutcome card, double extra)
    {
        var s = new Scenario();

        var seconds = Compute(s, Stop(StoppageReason.Foul, RestartType.DirectFreeKick, TeamSide.Home, s.State.Pitch.Centre, card), 0.5).Seconds;

        Assert.Equal(Tuning.DirectFreeKickSeconds * Spread(0.5) + extra, seconds, 9);
    }

    [Fact]
    public void Settings_that_would_break_the_formula_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeadBallTuning.Default with { GameStateFullMargin = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => DeadBallTuning.Default with { GameStateFromFraction = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => DeadBallTuning.Default with { SpreadMax = 0.5 });
    }
}
