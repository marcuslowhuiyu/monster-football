# Dead-Ball Times Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each stoppage gets its own dead-ball time (real average × random spread × game state × location + card time), and the referee adds a leading side's slow-down to the added time.

**Architecture:** A pure calculator `Restarts/DeadBallDuration` (one random draw) runs inside `MatchContext.StopPlay`; the result is stored on the `Stoppage` (`DeadBallSeconds`) and `RestartSystem` waits for it. Values live in a new `DeadBallTuning` record on `MatchConfig`. Diagnostics read the new `PlayStopped` metadata.

**Tech Stack:** C# / .NET 10, xUnit. Spec: `docs/superpowers/specs/2026-10-21-m2-7b-dead-ball-times-design.md`.

**Environment (every task):** `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`, repo root `/Users/sgcccodie1/Monster Football`, branch `feat/m2-7b-dead-ball-times`. Before each commit: `dotnet format MonsterFootball.slnx --verify-no-changes`, `dotnet build MonsterFootball.slnx`, `dotnet test MonsterFootball.slnx`. Every commit ends with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Never add `logs/`.

**Fingerprint check:** `dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3` before and after a commit; save outputs to the scratchpad and `diff` them.

---

## File structure

- Create `src/MonsterFootball.Simulation/Restarts/DeadBallTuning.cs` — all dead-ball values (public record).
- Create `src/MonsterFootball.Simulation/Restarts/DeadBallDuration.cs` — `DeadBallTime` and the calculator (internal static).
- Create `tests/MonsterFootball.Simulation.Tests/Restarts/DeadBallDurationTests.cs`.
- Modify `Core/MatchConfig.cs` (`DeadBall` property), `Core/Stoppage.cs` (`DeadBallSeconds`), `MatchContext.cs` (`StopPlay`), `Restarts/RestartSystem.cs` (wait time), `Restarts/RestartTuning.cs` (remove the seven `*DeadBallSeconds`).
- Modify `Diagnostics/MatchSummary.cs`, `Diagnostics/BatchSummary.cs`, `src/MonsterFootball.DebugRunner/Program.cs`.
- Modify tests: `Restarts/SetPieceRestartTests.cs`, `Restarts/FreeKickAndPenaltyTests.cs`, `Simulation/GoalAndRestartTests.cs`, `Simulation/MatchSimulationTests.cs`, `Statistics/MatchStatisticsTests.cs`, `Diagnostics/BatchTests.cs`; new `Simulation/StopPlayDeadBallTests.cs`.
- Docs: `docs/DECISIONS.md` (ADR-037), `docs/MATCH_SIMULATION.md`, `docs/LAWS_OF_THE_GAME.md`, `docs/GLOSSARY.md`, `docs/ARCHITECTURE.md`, `docs/CURRENT_STATE.md`, `docs/milestones/MILESTONE_2_PLAN.md`.

---

### Task 1: Dead-ball duration model (fingerprints identical)

**Files:**
- Create: `src/MonsterFootball.Simulation/Restarts/DeadBallTuning.cs`
- Create: `src/MonsterFootball.Simulation/Restarts/DeadBallDuration.cs`
- Modify: `src/MonsterFootball.Simulation/Core/MatchConfig.cs`
- Test: `tests/MonsterFootball.Simulation.Tests/Restarts/DeadBallDurationTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
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

    /// <summary>A short match (two 60 s periods) run to the last regular second of the second half: lateness 1.</summary>
    private static Scenario LateInTheMatch()
    {
        var s = new Scenario(config: MatchConfig.Default with { PeriodSeconds = 60 });
        for (var i = 0; i < 240; i++)
            s.State.Clock.Advance();
        s.State.Clock.StartNextPeriod();
        for (var i = 0; i < 240; i++)
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
}
```

Check the enum member names before running: `DisciplinaryOutcome` lives in `Core/Stoppage.cs` (members include `None`, `Yellow`, `SecondYellow`, `Red` — use whatever the file declares). `Clock.Advance`, `Clock.StartNextPeriod` and `Score.AddGoal` are internal and visible to the test project. If `StartNextPeriod` does more than set the period and its start tick (check `Core/MatchClock.cs`), keep the test as written as long as lateness comes out 1 — otherwise advance the clock the same way `PeriodSystem` does.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test MonsterFootball.slnx --filter FullyQualifiedName~DeadBallDurationTests`
Expected: build FAILS — `DeadBallTuning`, `DeadBallDuration`, `DeadBallTime` and `MatchConfig.DeadBall` do not exist.

- [ ] **Step 3: Write `DeadBallTuning`**

`src/MonsterFootball.Simulation/Restarts/DeadBallTuning.cs`:

```csharp
namespace MonsterFootball.Simulation.Restarts;

/// <summary>
/// How long the ball stays dead after play stops (ADR-037), as data (<see cref="Core.MatchConfig.DeadBall"/>).
/// Averages approximate real matches; all values are provisional until the M2.7b calibration pass.
/// </summary>
public sealed record DeadBallTuning
{
    public static DeadBallTuning Default { get; } = new();

    /// <summary>Average time from the ball going out to the throw-in.</summary>
    public double ThrowInSeconds { get; init; } = 15;

    /// <summary>Average time from the ball going out to the goal kick.</summary>
    public double GoalKickSeconds { get; init; } = 25;

    /// <summary>Average time from the ball going out to the corner (players go up for it).</summary>
    public double CornerSeconds { get; init; } = 35;

    /// <summary>Average time from a foul to the direct free kick.</summary>
    public double DirectFreeKickSeconds { get; init; } = 30;

    /// <summary>Average time from an offside to the indirect free kick.</summary>
    public double IndirectFreeKickSeconds { get; init; } = 20;

    /// <summary>Average time from a goal to the kickoff (celebration, walking back).</summary>
    public double GoalSeconds { get; init; } = 60;

    /// <summary>Average time from a foul in the area to the penalty.</summary>
    public double PenaltySeconds { get; init; } = 90;

    /// <summary>Smallest multiple of the average one stoppage can take (one uniform draw between this and <see cref="SpreadMax"/>).</summary>
    public double SpreadMin { get; init; } = 0.65;

    /// <summary>Largest multiple of the average one stoppage can take.</summary>
    public double SpreadMax { get; init; } = 1.35;

    /// <summary>Share of regular time after which the score starts to change the pace of restarts (2/3 = minute 60).</summary>
    public double GameStateFromFraction { get; init; } = 2.0 / 3;

    /// <summary>Goal margin at which the game-state effect is full.</summary>
    public double GameStateFullMargin { get; init; } = 2;

    /// <summary>At full strength, a leading side takes this much longer over its restarts (× 1 + this).</summary>
    public double MaxSlowdown { get; init; } = 0.6;

    /// <summary>At full strength, a trailing side takes this much less time over its restarts (× 1 − this).</summary>
    public double MaxHurry { get; init; } = 0.4;

    /// <summary>A free kick in the attacking third takes this many times longer (wall, players go up).</summary>
    public double AttackingFreeKickFactor { get; init; } = 1.3;

    /// <summary>A throw-in in the attacking third takes this many times longer.</summary>
    public double AttackingThrowInFactor { get; init; } = 1.2;

    /// <summary>Extra time when the foul brings a yellow card (the referee books the player).</summary>
    public double BookingSeconds { get; init; } = 30;

    /// <summary>Extra time when the foul brings a red card or second yellow (instead of <see cref="BookingSeconds"/>).</summary>
    public double DismissalSeconds { get; init; } = 45;
}
```

In `Core/MatchConfig.cs`, after the `Restarts` property:

```csharp
    /// <summary>Dead-ball times after each stoppage and the wasted time the referee adds (ADR-037).</summary>
    public DeadBallTuning DeadBall { get; init; } = DeadBallTuning.Default;
```

- [ ] **Step 4: Write `DeadBallDuration`**

`src/MonsterFootball.Simulation/Restarts/DeadBallDuration.cs`:

```csharp
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
```

Check: `Opponent()` is the existing `TeamSide` extension used in `MatchContext`; `TeamFrame` is in `MonsterFootball.Simulation.Positioning`; `IMatchRandom` is in `MonsterFootball.Simulation.Random`. Remove any `using` the compiler reports unused (IDE0005 is an error).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test MonsterFootball.slnx --filter FullyQualifiedName~DeadBallDurationTests`
Expected: all PASS. Then the full suite, format and build.

- [ ] **Step 6: Fingerprints identical, then commit**

Run the fingerprint check (nothing calls the new code yet): the diff must be empty.

```bash
git add src/MonsterFootball.Simulation/Restarts/DeadBallTuning.cs src/MonsterFootball.Simulation/Restarts/DeadBallDuration.cs \
  src/MonsterFootball.Simulation/Core/MatchConfig.cs tests/MonsterFootball.Simulation.Tests/Restarts/DeadBallDurationTests.cs
git commit -m "feat(restarts): dead-ball duration model

Average per stoppage type x random spread x game state x location, plus card
time; the slow-down part is wasted time. Unused; fingerprints identical.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Stoppages use their own dead-ball time (fingerprints change, ledger #14)

**Files:**
- Modify: `src/MonsterFootball.Simulation/Core/Stoppage.cs`
- Modify: `src/MonsterFootball.Simulation/MatchContext.cs` (`StopPlay`, `BeginStoppage`)
- Modify: `src/MonsterFootball.Simulation/Restarts/RestartSystem.cs`
- Modify: `src/MonsterFootball.Simulation/Restarts/RestartTuning.cs`
- Create: `tests/MonsterFootball.Simulation.Tests/Simulation/StopPlayDeadBallTests.cs`
- Modify tests listed in Step 6.

- [ ] **Step 1: Write the failing tests**

`tests/MonsterFootball.Simulation.Tests/Simulation/StopPlayDeadBallTests.cs`:

```csharp
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
        var s = new Scenario(random: new ScriptedRandom(0.5), config: MatchConfig.Default with { PeriodSeconds = 60 });
        for (var i = 0; i < 240; i++)
            s.State.Clock.Advance();
        s.State.Clock.StartNextPeriod();
        for (var i = 0; i < 239; i++) // the last regular tick: still regular time, lateness ≈ 1
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
        var s = new Scenario(random: new ScriptedRandom(0.5), config: MatchConfig.Default with { PeriodSeconds = 60 });
        for (var i = 0; i < 240; i++)
            s.State.Clock.Advance();
        s.State.Clock.StartNextPeriod();
        for (var i = 0; i < 260; i++) // past the regular end
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
```

In `tests/MonsterFootball.Simulation.Tests/Restarts/SetPieceRestartTests.cs` add:

```csharp
    [Fact]
    public void The_restart_waits_for_the_stoppages_own_dead_ball_time()
    {
        var s = Stopped(RestartType.ThrowIn, TeamSide.Away, new Vec2(30, 0), random: new MatchRng(5));
        var deadBall = s.State.CurrentStoppage!.DeadBallSeconds;

        var (seconds, _) = RunUntilTaken(s);

        Assert.True(deadBall > 0);
        Assert.True(seconds >= deadBall - 1e-9, $"taken after {seconds} s, dead-ball time {deadBall} s");
    }
```

(`MatchRng` is in `MonsterFootball.Simulation.Random`, already imported there.)

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test MonsterFootball.slnx --filter "FullyQualifiedName~StopPlayDeadBallTests|FullyQualifiedName~The_restart_waits"`
Expected: build FAILS — `Stoppage.DeadBallSeconds` does not exist.

- [ ] **Step 3: `Stoppage.DeadBallSeconds`**

In `Core/Stoppage.cs`, inside the `Stoppage` record after `Discipline`:

```csharp

    /// <summary>How long the ball stays dead before the restart setup begins (ADR-037); 0 for opening kickoffs.</summary>
    public double DeadBallSeconds { get; init; }
```

- [ ] **Step 4: `MatchContext.StopPlay` computes it**

Replace `StopPlay` and `BeginStoppage` in `src/MonsterFootball.Simulation/MatchContext.cs` with:

```csharp
    /// <summary>
    /// Stops play (ADR-023): records the stoppage with its dead-ball time (ADR-037), clears every player's ongoing
    /// action and plan, and emits PlayStopped (team = awarded team, position = restart location, metadata
    /// <c>reason</c>, <c>restart</c>, <c>deadBall</c> and <c>wasted</c> in seconds). In regular time, goals, penalties
    /// and a leading side's wasted time add to the added-time allowance.
    /// </summary>
    public void StopPlay(StoppageReason reason, RestartType restart, TeamSide awardedTeam, Vec2 location, PlayerId? offender = null, PlayerId? fouled = null,
        DisciplinaryOutcome discipline = DisciplinaryOutcome.None)
    {
        var stoppage = NewStoppage(reason, restart, awardedTeam, location, offender, fouled, discipline);
        var deadBall = DeadBallDuration.Compute(State, stoppage, Config, Random);
        Begin(stoppage with { DeadBallSeconds = deadBall.Seconds });
        if (State.Clock.Tick < State.Clock.RegularEndTick)
        {
            if (reason == StoppageReason.Goal)
                State.AddedTimeAllowanceSeconds += Config.AddedTime.PerGoalSeconds;
            if (restart == RestartType.Penalty)
                State.AddedTimeAllowanceSeconds += Config.AddedTime.PerPenaltySeconds;
            State.AddedTimeAllowanceSeconds += deadBall.WastedSeconds;
        }

        Emit(MatchEventType.PlayStopped, actor: offender, target: fouled, team: awardedTeam, position: location,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("reason", reason.ToString()),
                new KeyValuePair<string, string>("restart", restart.ToString()),
                new KeyValuePair<string, string>("deadBall", deadBall.Seconds.ToString("R", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("wasted", deadBall.WastedSeconds.ToString("R", CultureInfo.InvariantCulture)),
            ]));
    }
```

```csharp
    /// <summary>Records a stoppage and clears every player's ongoing action and plan, without emitting an event.</summary>
    public void BeginStoppage(StoppageReason reason, RestartType restart, TeamSide awardedTeam, Vec2 location, PlayerId? offender = null, PlayerId? fouled = null,
        DisciplinaryOutcome discipline = DisciplinaryOutcome.None) =>
        Begin(NewStoppage(reason, restart, awardedTeam, location, offender, fouled, discipline));

    private Stoppage NewStoppage(StoppageReason reason, RestartType restart, TeamSide awardedTeam, Vec2 location, PlayerId? offender, PlayerId? fouled,
        DisciplinaryOutcome discipline) =>
        new(reason, restart, awardedTeam, location, State.Clock.Tick) { Offender = offender, Fouled = fouled, Discipline = discipline };

    private void Begin(Stoppage stoppage)
    {
        State.StopPlay(stoppage);
        foreach (var player in State.ActivePlayers)
        {
            player.ClearIntent();
            player.OffBallPlan = null;
            player.TackleCooldownUntilTick = 0;
        }
    }
```

Add `using System.Globalization;` and `using MonsterFootball.Simulation.Restarts;` if not already present. The random property on `MatchContext` is `Random` (as used by `context.Random` elsewhere) — use the actual name.

- [ ] **Step 5: `RestartSystem` waits for it; remove the fixed minimums**

In `Restarts/RestartSystem.cs` `Advance`, replace `var deadBall = DeadBallSeconds(stoppage, tuning);` with `var deadBall = stoppage.DeadBallSeconds;` and delete the private `DeadBallSeconds(Stoppage, RestartTuning)` method.

In `Restarts/RestartTuning.cs` delete the seven properties `GoalDeadBallSeconds`, `ThrowInDeadBallSeconds`, `GoalKickDeadBallSeconds`, `CornerDeadBallSeconds`, `IndirectFreeKickDeadBallSeconds`, `DirectFreeKickDeadBallSeconds`, `PenaltyDeadBallSeconds` (with their doc comments). Change the class summary to "Restart law distances, set-piece choices and setup timing, as data …" and the `SetupTimeoutSeconds` summary to start "Once the stoppage's dead-ball time (ADR-037) is over, …".

- [ ] **Step 6: Fix the tests that relied on the fixed minimums or a draw-free stop**

Each is a test-premise fix, not a behaviour change — say so in the commit message.

- `Restarts/SetPieceRestartTests.cs`:
  - the four `Assert.True(seconds >= Tuning.XDeadBallSeconds - 1e-9)` (throw-in, goal kick, corner, offside free kick): read `var deadBall = s.State.CurrentStoppage!.DeadBallSeconds;` before `RunUntilTaken` and assert `seconds >= deadBall - 1e-9`.
  - `After_the_setup_timeout_stragglers_are_placed_and_the_restart_is_taken`: replace `Assert.Equal(Tuning.ThrowInDeadBallSeconds, seconds, 6);` with the same `deadBall` read and `Assert.InRange(seconds, deadBall - 1e-9, deadBall + s.Context.Config.TickSeconds + 1e-9);`.
  - `Nothing_random_or_eventful_happens_until_the_restart_is_taken`: stopping play now draws once; change `new ScriptedRandom()` to `new ScriptedRandom(0.5)` and the comment to `// one draw for the dead-ball time, then throws on any draw`. 40 ticks (10 s) is shorter than the corner's dead-ball time at draw 0.5 (35 s), so the phase assertion holds.
- `Restarts/FreeKickAndPenaltyTests.cs`: `Assert.True(seconds >= Tuning.PenaltyDeadBallSeconds - 1e-9);` → read the stoppage's `DeadBallSeconds` before running and assert against it.
- `Simulation/GoalAndRestartTests.cs` (~line 76): read `var deadBall = state.CurrentStoppage!.DeadBallSeconds;` next to `awarded`, and assert `Assert.InRange(kickoff.Timestamp.Seconds - stoppedAt, deadBall, deadBall + config.Restarts.SetupTimeoutSeconds + config.TickSeconds);`.
- `Simulation/MatchSimulationTests.cs` `Added_time_is_announced_once_per_half_from_its_goals_penalties_and_cards`: replace the `PlayStopped` arm with
  `MatchEventType.PlayStopped => (x.Metadata["restart"] == "Penalty" ? allowances.PerPenaltySeconds : 0) + double.Parse(x.Metadata["wasted"], System.Globalization.CultureInfo.InvariantCulture),`
  and rename the test to `..._from_its_goals_penalties_cards_and_wasted_time`.
- `Statistics/MatchStatisticsTests.cs` line ~164: the per-match floor `> 60 * 60` becomes `> 45 * 60` (the health band's floor; real matches have 55–60 min).
- `Diagnostics/BatchTests.cs` line 47: `Assert.InRange(batch.AverageBallInPlayMinutes, 60, 100);` → `45, 100`.
- Anything else the full suite reports for the same reason gets the same kind of fix; anything else failing is reported back, not patched.

- [ ] **Step 7: Run everything**

Run: format, build, `dotnet test MonsterFootball.slnx`. Expected: all pass.

- [ ] **Step 8: Measure**

```bash
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000 | tee <scratchpad>/deadball-1000.txt
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 2000 --from 1001 | tee <scratchpad>/deadball-2000.txt
dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3
```

Expected: HEALTHY both times; ball in play within 52–65 min; home possession 50 ± 1 %; safety-cap share ≤ 3 %. Report all figures. If ball in play is outside 52–65 or the batch is unhealthy, stop and report — do not tune.

- [ ] **Step 9: Commit**

```bash
git add -A src tests
git commit -m "feat(restarts): stoppages use their own dead-ball time

StopPlay draws each stoppage's dead-ball time (ADR-037) and stores it on the
stoppage; RestartSystem waits for it instead of the fixed per-type minimums,
which leave RestartTuning. A leading side's slow-down is added to the added
time in regular time. PlayStopped gains deadBall and wasted metadata.

Test premises: restart timing tests read the stoppage's own time; the quiet-
restart test scripts the one new draw; the added-time test includes wasted
time; ball-in-play floors drop from 60 to 45 min (health band floor).

Fingerprints change (ledger #14).

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(`git add -A src tests` must not pick up `logs/`; check `git status` first.)

---

### Task 3: Dead-ball times in the batch (fingerprints identical)

**Files:**
- Modify: `src/MonsterFootball.Simulation/Diagnostics/MatchSummary.cs`
- Modify: `src/MonsterFootball.Simulation/Diagnostics/BatchSummary.cs`
- Modify: `src/MonsterFootball.DebugRunner/Program.cs`
- Test: `tests/MonsterFootball.Simulation.Tests/Diagnostics/BatchTests.cs`

- [ ] **Step 1: Write the failing test** — in `BatchTests.Batch_aggregate_is_consistent` add:

```csharp
        var t = DeadBallTuning.Default;
        Assert.InRange(batch.AverageThrowInDeadBallSeconds, t.ThrowInSeconds * 0.8, t.ThrowInSeconds * 1.3);
        Assert.InRange(batch.AverageGoalKickDeadBallSeconds, t.GoalKickSeconds * 0.8, t.GoalKickSeconds * 1.3);
        Assert.InRange(batch.AverageCornerDeadBallSeconds, t.CornerSeconds * 0.8, t.CornerSeconds * 1.3);
        Assert.InRange(batch.AverageDirectFreeKickDeadBallSeconds, t.DirectFreeKickSeconds * 0.8, t.DirectFreeKickSeconds * 1.6);
        Assert.InRange(batch.AverageGoalDeadBallSeconds, t.GoalSeconds * 0.6, t.GoalSeconds * 1.4);
        Assert.True(batch.AverageWastedSeconds >= 0);
```

and in a new test:

```csharp
    [Fact]
    public void A_match_summary_lists_each_stoppage_dead_ball_time()
    {
        var result = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), 3).RunToCompletion();

        var summary = MatchSummary.From(result);

        Assert.Equal(result.Events.Count(e => e.Type == MatchEventType.PlayStopped), summary.DeadBalls.Count);
        Assert.All(summary.DeadBalls, d => Assert.True(d.Seconds > 0));
    }
```

(Add `using MonsterFootball.Simulation.Events;` and `using MonsterFootball.Simulation.Restarts;` if missing. Averages with no stoppages of a type are 0, so the per-type ranges assume six full matches contain throw-ins, goal kicks, corners, free kicks and goals — true for seeds 1–6; offside and penalty averages are not range-checked.)

- [ ] **Step 2: Run to verify it fails** — `dotnet test MonsterFootball.slnx --filter FullyQualifiedName~BatchTests` → build FAILS.

- [ ] **Step 3: `MatchSummary.DeadBalls`**

In `Diagnostics/MatchSummary.cs` add, above the record:

```csharp
/// <summary>One stoppage's dead-ball time and wasted time (from PlayStopped metadata, ADR-037).</summary>
public readonly record struct StoppageTime(RestartType Restart, double Seconds, double WastedSeconds);
```

inside the record, after `Abandoned`:

```csharp
    /// <summary>Every PlayStopped in the match, in order: restart type, dead-ball seconds, wasted seconds.</summary>
    public IReadOnlyList<StoppageTime> DeadBalls { get; init; } = [];
```

and in `From`'s initializer, after `Abandoned = result.Abandoned,`:

```csharp
            DeadBalls = result.Events.Where(e => e.Type == MatchEventType.PlayStopped)
                .Select(e => new StoppageTime(
                    Enum.Parse<RestartType>(e.Metadata["restart"]),
                    double.Parse(e.Metadata["deadBall"], CultureInfo.InvariantCulture),
                    double.Parse(e.Metadata["wasted"], CultureInfo.InvariantCulture)))
                .ToList(),
```

(`RestartType` is in `MonsterFootball.Simulation.Core`; add the using.)

- [ ] **Step 4: `BatchSummary` averages** — add init properties after `PenaltyConversion`:

```csharp
    /// <summary>Mean dead-ball seconds per stoppage of each type (ADR-037); 0 when the batch has none.</summary>
    public double AverageThrowInDeadBallSeconds { get; init; }
    public double AverageGoalKickDeadBallSeconds { get; init; }
    public double AverageCornerDeadBallSeconds { get; init; }
    public double AverageDirectFreeKickDeadBallSeconds { get; init; }
    public double AverageIndirectFreeKickDeadBallSeconds { get; init; }

    /// <summary>Mean dead-ball seconds from a goal to the kickoff.</summary>
    public double AverageGoalDeadBallSeconds { get; init; }
    public double AveragePenaltyDeadBallSeconds { get; init; }

    /// <summary>Mean seconds per match of slow-down by a leading side (added to added time only in regular time).</summary>
    public double AverageWastedSeconds { get; init; }
```

In `From`, before `return`:

```csharp
        var stoppages = ordered.SelectMany(s => s.DeadBalls).ToList();
        double DeadBall(RestartType restart) =>
            stoppages.Where(d => d.Restart == restart).Select(d => d.Seconds).DefaultIfEmpty(0).Average();
```

and in the initializer:

```csharp
            AverageThrowInDeadBallSeconds = DeadBall(RestartType.ThrowIn),
            AverageGoalKickDeadBallSeconds = DeadBall(RestartType.GoalKick),
            AverageCornerDeadBallSeconds = DeadBall(RestartType.Corner),
            AverageDirectFreeKickDeadBallSeconds = DeadBall(RestartType.DirectFreeKick),
            AverageIndirectFreeKickDeadBallSeconds = DeadBall(RestartType.IndirectFreeKick),
            AverageGoalDeadBallSeconds = DeadBall(RestartType.Kickoff), // only goals emit PlayStopped with a kickoff
            AveragePenaltyDeadBallSeconds = DeadBall(RestartType.Penalty),
            AverageWastedSeconds = ordered.Average(s => s.DeadBalls.Sum(d => d.WastedSeconds)),
```

- [ ] **Step 5: Print them** — in `src/MonsterFootball.DebugRunner/Program.cs`, after the `Added time per half (min)` line:

```csharp
    string.Create(CultureInfo.InvariantCulture, $"Dead ball (s): throw-in    {b.AverageThrowInDeadBallSeconds:0.0} · goal kick {b.AverageGoalKickDeadBallSeconds:0.0} · corner {b.AverageCornerDeadBallSeconds:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Dead ball (s): free kick   {b.AverageDirectFreeKickDeadBallSeconds:0.0} · offside {b.AverageIndirectFreeKickDeadBallSeconds:0.0} · goal {b.AverageGoalDeadBallSeconds:0.0} · penalty {b.AveragePenaltyDeadBallSeconds:0.0}"),
    string.Create(CultureInfo.InvariantCulture, $"Slow-down per match (s)    {b.AverageWastedSeconds:0.0}"),
```

- [ ] **Step 6: Run** — full suite, format, build; `batch 1000` (Release) shows the new lines; fingerprint diff against Task 2 empty. Report the per-type means: each within 10 % of its average (free kicks may sit higher from the attacking-third factor and cards — report, don't tune).

- [ ] **Step 7: Commit**

```bash
git add src/MonsterFootball.Simulation/Diagnostics src/MonsterFootball.DebugRunner/Program.cs tests/MonsterFootball.Simulation.Tests/Diagnostics/BatchTests.cs
git commit -m "feat(diagnostics): dead-ball times in the batch

Match summaries keep each stoppage's dead-ball and wasted seconds; the batch
prints mean dead-ball time per restart type and slow-down per match.
Fingerprints identical.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Docs (ADR-037)

**Files:** `docs/DECISIONS.md`, `docs/MATCH_SIMULATION.md`, `docs/LAWS_OF_THE_GAME.md`, `docs/GLOSSARY.md`, `docs/ARCHITECTURE.md`, `docs/CURRENT_STATE.md`, `docs/milestones/MILESTONE_2_PLAN.md`.

- [ ] **Step 1: ADR-037** — append to `docs/DECISIONS.md` after ADR-036, in its style (Status / Context / Decision / Consequences):
  - Title: `ADR-037 — Each Stoppage Draws Its Own Dead-Ball Time`.
  - Context: 72.9 min ball in play with fixed minimums; real 55–60; real stoppages vary with type, game state, location and cards.
  - Decision: the formula, the averages table, spread 0.65–1.35 (one draw at `StopPlay`), game state from minute 60 to 90 judged for the restarting side (×1.6 / ×0.6 at full strength, full at a two-goal margin), attacking-third factors (free kick 1.3, throw-in 1.2), cards +30 / +45, wasted = slow-down part added in regular time; opening kickoffs 0 s; supersedes the fixed `*DeadBallSeconds` minimums of `RestartTuning` (ADR-023); `PlayStopped` metadata `deadBall` / `wasted`; provisional values until the calibration pass.
  - Consequences: batch figures from Task 2/3 (ball in play, per-type means, added time, safety cap, Home/Away); fingerprints change (ledger #14); the trailing side's quick restarts deep in its own half are covered by the hurry factor, not a separate rule; time wasted during added time is not compensated.
- [ ] **Step 2: Other docs**
  - `MATCH_SIMULATION.md`: the restart/stoppage section describes per-stoppage dead-ball times and wasted time; the `PlayStopped` metadata list gains `deadBall`, `wasted`; batch output lines; update the ball-in-play figure.
  - `LAWS_OF_THE_GAME.md`: Law 7 (added time) — wasted time by a leading side is added; note the not-modelled items (substitutions, injuries, time-wasting cautions).
  - `GLOSSARY.md`: entries **Dead-ball time** and **Wasted time**.
  - `ARCHITECTURE.md`: `Restarts/` lists `DeadBallTuning` and `DeadBallDuration`; `MatchConfig.DeadBall`.
  - `CURRENT_STATE.md`: M2.7b step 4 done (pending merge), latest batch figures, next: step 5 calibration pass.
  - `MILESTONE_2_PLAN.md`: b4 row scope → "each stoppage draws its own dead-ball time (type average, spread, game state, location, cards); wasted time added. ADR-037."; ledger row `| 14 | 7b4 | per-stoppage dead-ball times |`.
  - Spec: add under the status line "Values below are provisional; final values are set in the calibration pass (ADR-037)." if any value changed during implementation, record it there.
- [ ] **Step 3: Commit**

```bash
git add docs
git commit -m "docs: dead-ball times (ADR-037)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
