using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Restarts;

/// <summary>A stoppage's dead-ball time, and the part of it caused by a leading side's slow-down (ADR-037).</summary>
public readonly record struct DeadBallTime(double Seconds, double WastedSeconds);

/// <summary>
/// How long the ball stays dead after play stops (ADR-037): the type's average × a random spread × game state ×
/// location, plus time for a card. One draw per stoppage; opening kickoffs and unknown stoppages take none.
/// </summary>
internal static class DeadBallDuration
{
    public static DeadBallTime Compute(MatchState state, Stoppage stoppage, MatchConfig config, IMatchRandom random)
    {
        var tuning = config.DeadBall;
        var average = Average(stoppage, tuning);
        if (average <= 0)
            return default;

        var spread = tuning.SpreadMin + (tuning.SpreadMax - tuning.SpreadMin) * random.NextDouble();
        var clock = state.Clock;
        var lateness = Lateness(clock.Period, (clock.Tick - clock.PeriodStartTick) * clock.TickSeconds, clock.RegularPeriodSeconds,
            config.Periods, tuning.GameStateFromFraction);
        var margin = state.Score.For(stoppage.AwardedTeam) - state.Score.For(stoppage.AwardedTeam.Opponent());
        var gameState = GameStateFactor(margin, lateness, tuning);
        var paced = average * spread * LocationFactor(state, stoppage, tuning);
        var card = stoppage.Discipline switch
        {
            DisciplinaryOutcome.Yellow => tuning.BookingSeconds,
            DisciplinaryOutcome.Red or DisciplinaryOutcome.SecondYellow => tuning.DismissalSeconds,
            _ => 0,
        };

        return new DeadBallTime(paced * gameState + card, gameState > 1 ? paced * (gameState - 1) : 0);
    }

    /// <summary>
    /// 0 until <paramref name="fromFraction"/> of regular time has been played, rising to 1 at the end of regular time
    /// and staying 1 in added time. Added time in an earlier period counts as that period's regular end.
    /// </summary>
    public static double Lateness(int period, double periodElapsedSeconds, double periodSeconds, int periods, double fromFraction)
    {
        var played = (period - 1) * periodSeconds + Math.Min(periodElapsedSeconds, periodSeconds);
        var total = periods * periodSeconds;
        return Math.Clamp((played - fromFraction * total) / ((1 - fromFraction) * total), 0, 1);
    }

    /// <summary>Pace multiplier for the side taking the restart: slower when leading, quicker when trailing, 1 when level.</summary>
    public static double GameStateFactor(int margin, double lateness, DeadBallTuning tuning)
    {
        var strength = lateness * Math.Min(Math.Abs(margin), tuning.GameStateFullMargin) / tuning.GameStateFullMargin;
        return margin switch
        {
            > 0 => 1 + tuning.MaxSlowdown * strength,
            < 0 => 1 - tuning.MaxHurry * strength,
            _ => 1,
        };
    }

    private static double Average(Stoppage stoppage, DeadBallTuning tuning) => stoppage.Reason switch
    {
        StoppageReason.Goal => tuning.GoalSeconds,
        StoppageReason.BallOutOfPlay => stoppage.Restart switch
        {
            RestartType.ThrowIn => tuning.ThrowInSeconds,
            RestartType.GoalKick => tuning.GoalKickSeconds,
            RestartType.Corner => tuning.CornerSeconds,
            _ => 0,
        },
        StoppageReason.Offside => tuning.IndirectFreeKickSeconds,
        StoppageReason.Foul => stoppage.Restart == RestartType.Penalty ? tuning.PenaltySeconds : tuning.DirectFreeKickSeconds,
        _ => 0,
    };

    private static double LocationFactor(MatchState state, Stoppage stoppage, DeadBallTuning tuning)
    {
        if (TeamFrame.Convert(state.Pitch, stoppage.AwardedTeam, stoppage.Location).X < state.Zones.FinalThirdX)
            return 1;

        return stoppage.Restart switch
        {
            RestartType.DirectFreeKick or RestartType.IndirectFreeKick => tuning.AttackingFreeKickFactor,
            RestartType.ThrowIn => tuning.AttackingThrowInFactor,
            _ => 1,
        };
    }
}
