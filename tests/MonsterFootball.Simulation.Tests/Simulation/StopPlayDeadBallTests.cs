using System.Globalization;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Restarts;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

/// <summary>Stopping play sets the stoppage's dead-ball time and adds wasted time (ADR-037).</summary>
public class StopPlayDeadBallTests
{
    private static readonly DeadBallTuning Tuning = DeadBallTuning.Default;

    private static double Spread(double draw) => Tuning.SpreadMin + (Tuning.SpreadMax - Tuning.SpreadMin) * draw;

    [Fact]
    public void Stopping_play_stores_the_dead_ball_time_and_reports_it()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5));
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);

        s.Context.StopPlay(StoppageReason.BallOutOfPlay, RestartType.GoalKick, TeamSide.Away, new Vec2(99.5, 34));

        var expected = Tuning.GoalKickSeconds * Spread(0.5);
        Assert.Equal(expected, s.State.CurrentStoppage!.DeadBallSeconds, 9);
        var stopped = s.Log.Events.Single(e => e.Type == MatchEventType.PlayStopped);
        Assert.Equal(expected, double.Parse(stopped.Metadata["deadBall"], CultureInfo.InvariantCulture), 9);
        Assert.Equal(0, double.Parse(stopped.Metadata["wasted"], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void A_leading_side_slowing_down_late_on_adds_its_wasted_time_to_the_allowance()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5));
        var half = s.State.Clock.RegularPeriodTicks;
        for (var i = 0; i < half; i++)
            s.State.Clock.Advance();
        s.State.Clock.StartNextPeriod();
        for (var i = 0; i < half - 1; i++) // the last regular tick: still regular time, lateness ≈ 1
            s.State.Clock.Advance();
        s.State.Score.AddGoal(TeamSide.Home);
        s.State.Score.AddGoal(TeamSide.Home);
        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        var before = s.State.AddedTimeAllowanceSeconds;

        s.Context.StopPlay(StoppageReason.BallOutOfPlay, RestartType.GoalKick, TeamSide.Home, new Vec2(5.5, 34));

        var wasted = double.Parse(s.Log.Events.Single(e => e.Type == MatchEventType.PlayStopped).Metadata["wasted"], CultureInfo.InvariantCulture);
        Assert.True(wasted > 0);
        Assert.Equal(before + wasted, s.State.AddedTimeAllowanceSeconds, 9);
    }

    [Fact]
    public void Wasted_time_in_added_time_is_not_added_again()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5));
        var half = s.State.Clock.RegularPeriodTicks;
        for (var i = 0; i < half; i++)
            s.State.Clock.Advance();
        s.State.Clock.StartNextPeriod();
        for (var i = 0; i < half + 20; i++) // past the regular end
            s.State.Clock.Advance();
        s.State.Score.AddGoal(TeamSide.Home);
        s.State.Score.AddGoal(TeamSide.Home);
        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        var before = s.State.AddedTimeAllowanceSeconds;

        s.Context.StopPlay(StoppageReason.BallOutOfPlay, RestartType.GoalKick, TeamSide.Home, new Vec2(5.5, 34));

        Assert.True(double.Parse(s.Log.Events.Single(e => e.Type == MatchEventType.PlayStopped).Metadata["wasted"], CultureInfo.InvariantCulture) > 0);
        Assert.Equal(before, s.State.AddedTimeAllowanceSeconds);
    }
}
