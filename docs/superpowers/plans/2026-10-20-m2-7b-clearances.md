# M2.7b Step 2 — Clearances Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Defenders under pressure in their own third can clear the ball — wide towards the touchline when in danger, long towards their striker otherwise — as a new kind of ball flight that lands loose, often goes out, and is contested.

**Architecture:** A new `Actions/Clearing` package (intent, result, resolver, execution) mirrors passing and shooting. `BallFlight` gains a third kind; `BallFlightSystem`, `MatchState.LaunchBall` and `MatchFrame` handle it. A `Clear` candidate joins the on-ball utility AI (own defensive third only); `TeamShapeSystem` sends the nearest outfield player of each side to the landing spot. Spec: `docs/superpowers/specs/2026-10-20-m2-7b-clearances-design.md`.

**Tech Stack:** .NET 10 / C#, xUnit. Run `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH` in each shell; commands from the repo root.

**Conventions:** branch `feat/m2-7b-clearances` (exists; spec committed). TDD: see each new test fail before implementing. Every commit builds and passes the full suite; `dotnet format MonsterFootball.slnx --verify-no-changes` before each commit; messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Leave the untracked `logs/` folder alone. If an existing full-match test fails because a seeded match now plays differently, do not bend production code: fix the test's premise only with the user's/controller's agreement, and explain it in the commit.

Facts used below: team frame = own goal at X = 0, attacking +X, left flank +Y; Away is a 180° rotation (`TeamFrame.Convert`). Default pitch 105 × 68. `Scenario` (tests) parks every player on its own goal line (Home x = 1, Away x = 104, y = 2, 5, 8, …) and is in open play. `PlayerStats` constructor order: technique, passing, finishing, defending, pace, power, stamina, awareness, keeping. `ScriptedRandom(params double[])` returns the given draws in order and throws when it runs out.

---

## File structure

| File | Responsibility |
|---|---|
| Create `src/MonsterFootball.Simulation/Actions/Clearing/ClearanceIntent.cs` | `ClearanceIntent`, `ClearanceKind`, `ClearanceResult` |
| Create `src/MonsterFootball.Simulation/Actions/Clearing/ClearanceResolver.cs` | Aim (kind + intended target) and resolution (scatter, travel) |
| Create `src/MonsterFootball.Simulation/Actions/Clearing/ClearanceExecution.cs` | Launch (flight, offside judgement, event), landing, leaving play |
| Modify `Actions/BallFlight.cs`, `Actions/BallFlightSystem.cs`, `Core/MatchState.cs` (`LaunchBall`), `Observation/MatchFrame.cs`, `Events/MatchEventType.cs` | The third flight kind end to end |
| Modify `AI/OnBallDecisionSystem.cs`, `MatchSimulation.cs` | Execute a `ClearanceIntent` |
| Modify `Statistics/MatchStatistics.cs`, `Diagnostics/MatchReport.cs`, `Diagnostics/BatchSummary.cs`, `DebugRunner/Program.cs` | Counting and reporting clearances |
| Modify `Tactical/TacticalZones.cs`, `AI/AiTuning.cs`, `AI/OnBallCandidateGenerator.cs`, `Positioning/TeamShapeSystem.cs`, `Diagnostics/BatchHealth.cs` | The AI candidate, the chase, the health band |
| Create tests `Actions/ClearanceResolverTests.cs`, `Actions/ClearanceFlowTests.cs`, `AI/ClearanceDecisionTests.cs` | Behaviour tests |

---

### Task 0: Baseline

The baseline is current `main` (after width, PR #32). Its batch is recorded at `$SCRATCHPAD/width-after.txt` (throw-ins 23.4, ball in play 77.9 min, goals 1.95, HEALTHY). No action unless that file is missing, in which case run `dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000 > $SCRATCHPAD/clearances-before.txt` on `main`.

---

### Task 1: Clearance types and resolver

**Files:** create `Actions/Clearing/ClearanceIntent.cs`, `Actions/Clearing/ClearanceResolver.cs`; test `tests/MonsterFootball.Simulation.Tests/Actions/ClearanceResolverTests.cs`.

- [ ] **Step 1: Write the failing tests**

```csharp
using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class ClearanceResolverTests
{
    private readonly ClearanceResolver _resolver = new();

    [Fact]
    public void Under_pressure_near_goal_the_clearance_goes_wide_towards_the_nearest_touchline()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 10, 20);
        s.Place(TeamSide.Away, PlayingPosition.ST, 11, 20); // 1 m away: pressure 2/3
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var (kind, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(ClearanceKind.Wide, kind);
        Assert.Equal(new Vec2(35, 0), target); // right touchline, 25 m upfield; within the 35 m range
    }

    [Fact]
    public void Wide_clearances_mirror_for_the_away_side()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Away, PlayingPosition.LCB, 95, 48); // team frame (10, 20)
        s.Place(TeamSide.Home, PlayingPosition.ST, 94, 48);
        s.GiveBall(TeamSide.Away, PlayingPosition.LCB);

        var (kind, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(ClearanceKind.Wide, kind);
        Assert.Equal(new Vec2(70, 68), target);
    }

    [Fact]
    public void Without_pressure_the_clearance_goes_long_towards_the_striker()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        s.Place(TeamSide.Home, PlayingPosition.ST, 60, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var (kind, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(ClearanceKind.Long, kind);
        Assert.Equal(55, target.X, precision: 9);
        Assert.Equal(34, target.Y, precision: 9);
    }

    [Fact]
    public void Far_from_goal_even_a_pressed_clearance_goes_long()
    {
        var s = new Scenario();
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 30, 20); // 33 m from goal
        s.Place(TeamSide.Away, PlayingPosition.ST, 31, 20);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        Assert.Equal(ClearanceKind.Long, _resolver.Aim(s.State, kicker).Kind);
    }

    [Fact]
    public void A_long_clearance_goes_straight_upfield_when_the_striker_is_not_ahead()
    {
        var s = new Scenario(); // the Home ST is parked on Home's own goal line, behind the kicker
        var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 30);
        s.GiveBall(TeamSide.Home, PlayingPosition.LCB);

        var (_, target) = _resolver.Aim(s.State, kicker);

        Assert.Equal(new Vec2(55, 30), target);
    }

    [Fact]
    public void Stronger_players_clear_further()
    {
        double Length(int power)
        {
            var s = new Scenario(homeStats: new PlayerStats(5, 5, 5, 5, 5, power, 5, 5, 5));
            var kicker = s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 30);
            s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
            return _resolver.Aim(s.State, kicker).Target.X - 20;
        }

        Assert.Equal(35 + 2 * 4, Length(9), precision: 9);
        Assert.Equal(35 - 2 * 4, Length(1), precision: 9);
    }

    [Fact]
    public void Scatter_moves_the_landing_point_within_its_bounds()
    {
        ClearanceResult Resolve(params double[] draws)
        {
            var s = new Scenario(random: new ScriptedRandom(draws));
            s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 30);
            var kicker = s.GiveBall(TeamSide.Home, PlayingPosition.LCB);
            return _resolver.Resolve(s.State, new ClearanceIntent(kicker.PlayerId), s.Context.Random);
        }

        // Aim (55, 30): along = +X, across = +Y. Technique 5: lateral up to 4 + 0.8 × 5 = 8 m; length up to 5 m.
        var centre = Resolve(0.5, 0.5);
        Assert.Equal(new Vec2(55, 30), centre.TargetPosition);
        Assert.Equal(new Vec2(55, 30), centre.IntendedTarget);
        Assert.Equal(35 / ClearanceResolver.BallSpeed, centre.TravelDuration, precision: 9);

        var extreme = Resolve(0.0, 0.0);
        Assert.Equal(50, extreme.TargetPosition.X, precision: 9);
        Assert.Equal(22, extreme.TargetPosition.Y, precision: 9);
    }
}
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test MonsterFootball.slnx --filter "FullyQualifiedName~ClearanceResolverTests"` → build error (types missing).

- [ ] **Step 3: Implement**

`src/MonsterFootball.Simulation/Actions/Clearing/ClearanceIntent.cs`:
```csharp
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Clearing;

/// <summary>The ball carrier's wish to clear the ball: kick it to a spot, with no target player (D6, ADR-035).</summary>
public sealed record ClearanceIntent(PlayerId Kicker) : ActionIntent(Kicker);

public enum ClearanceKind
{
    /// <summary>Towards the nearest touchline, under pressure near the clearer's own goal.</summary>
    Wide,

    /// <summary>Upfield towards the clearer's striker.</summary>
    Long,
}

/// <summary>A resolved clearance, decided when the ball is struck and applied when it lands (ADR-012).</summary>
public sealed record ClearanceResult(
    TeamSide Team,
    PlayerId Kicker,
    ClearanceKind Kind,
    Vec2 StartPosition,
    Vec2 IntendedTarget,
    Vec2 TargetPosition,
    double TravelDuration)
{
    /// <summary>Distance from the kick to where the ball lands, in metres.</summary>
    public double Distance => StartPosition.DistanceTo(TargetPosition);
}
```

`src/MonsterFootball.Simulation/Actions/Clearing/ClearanceResolver.cs`:
```csharp
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Actions.Clearing;

/// <summary>
/// Resolves a <see cref="ClearanceIntent"/> (D6, ADR-035). Wide — pressed (pressure ≥ 0.5) within 25 m of the
/// clearer's own goal — aims at the nearest touchline 25 m further upfield; Long aims towards the clearer's striker
/// (straight upfield if there is none ahead). Distance 35 m + 2 m per Power point above 5 caps both. Two draws scatter
/// the landing point sideways (± 4 m + 0.8 m per Technique point below 10) and in length (± 5 m). Reads state only.
/// </summary>
internal sealed class ClearanceResolver
{
    /// <summary>Same pressure measure as the on-ball AI: 1 − nearest opponent / radius, 0 beyond it.</summary>
    public const double PressureRadius = 3;
    public const double WidePressure = 0.5;
    public const double WideRange = 25;
    public const double WideAdvance = 25;
    public const double BaseDistance = 35;
    public const double DistancePerPower = 2;
    public const double BaseLateralScatter = 4;
    public const double LateralScatterPerTechnique = 0.8;
    public const double LengthScatter = 5;
    public const double BallSpeed = 18;

    /// <summary>How far <paramref name="kicker"/> can clear, in metres.</summary>
    public static double Distance(PlayerMatchState kicker) =>
        BaseDistance + DistancePerPower * (kicker.Definition.Stats.Power - 5);

    /// <summary>The kind of clearance and where it is aimed (pitch coordinates). Deterministic.</summary>
    public (ClearanceKind Kind, Vec2 Target) Aim(MatchState state, PlayerMatchState kicker)
    {
        var pitch = state.Pitch;
        var local = TeamFrame.Convert(pitch, kicker.Team, kicker.Position);
        var distance = Distance(kicker);
        var nearest = state.TeamPlayers(kicker.Team.Opponent()).Min(o => o.Position.DistanceTo(kicker.Position));
        var pressure = nearest < PressureRadius ? 1 - nearest / PressureRadius : 0;

        if (pressure >= WidePressure && kicker.Position.DistanceTo(pitch.OwnGoalCentre(kicker.Team)) <= WideRange)
        {
            var aim = new Vec2(local.X + WideAdvance, local.Y >= pitch.Width / 2 ? pitch.Width : 0);
            var reach = Math.Min(distance, local.DistanceTo(aim));
            return (ClearanceKind.Wide, TeamFrame.Convert(pitch, kicker.Team, local + (aim - local).Normalized() * reach));
        }

        var direction = new Vec2(1, 0);
        var striker = state.TeamPlayers(kicker.Team).FirstOrDefault(p => p.Role == PlayingPosition.ST && p != kicker);
        if (striker is not null)
        {
            var strikerLocal = TeamFrame.Convert(pitch, kicker.Team, striker.Position);
            if (strikerLocal.X > local.X)
                direction = (strikerLocal - local).Normalized();
        }

        return (ClearanceKind.Long, TeamFrame.Convert(pitch, kicker.Team, local + direction * distance));
    }

    public ClearanceResult Resolve(MatchState state, ClearanceIntent intent, IMatchRandom random)
    {
        var kicker = state.Player(intent.Kicker);
        if (!state.HasBall(intent.Kicker))
            throw new ArgumentException($"{kicker} cannot clear without the ball.", nameof(intent));

        var (kind, target) = Aim(state, kicker);
        var start = kicker.Position;
        var along = (target - start).Normalized();
        var across = new Vec2(-along.Y, along.X);
        var lateralLimit = BaseLateralScatter + LateralScatterPerTechnique * (10 - kicker.Definition.Stats.Technique);
        var lateral = (random.NextDouble() * 2 - 1) * lateralLimit;
        var length = (random.NextDouble() * 2 - 1) * LengthScatter;
        var landing = target + across * lateral + along * length;

        return new ClearanceResult(kicker.Team, kicker.PlayerId, kind, start, target, landing, start.DistanceTo(landing) / BallSpeed);
    }
}
```
(Check: wide test — local (10, 20), aim (35, 0), |aim − local| = √(625 + 400) = 32.0 < 35, so target = aim. Away: local (10, 20) → pitch (70, 68).)

- [ ] **Step 4: Run the filter (all pass), then the full suite (all pass).**

- [ ] **Step 5: Commit**
```bash
dotnet format MonsterFootball.slnx --verify-no-changes
git add src/MonsterFootball.Simulation/Actions/Clearing tests/MonsterFootball.Simulation.Tests/Actions/ClearanceResolverTests.cs
git commit -m "feat(actions): clearance resolver

ClearanceIntent, ClearanceResult and ClearanceResolver: wide towards the
nearest touchline when pressed near the clearer's own goal, otherwise
long towards the striker; distance from Power, scatter from Technique.
Unused so far; fingerprints identical.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Clearance flight end to end (still never chosen)

**Files:** create `Actions/Clearing/ClearanceExecution.cs`; modify `Actions/BallFlight.cs`, `Actions/BallFlightSystem.cs`, `Core/MatchState.cs` (`LaunchBall`), `Events/MatchEventType.cs`, `Observation/MatchFrame.cs`, `AI/OnBallDecisionSystem.cs`, `MatchSimulation.cs`; test `tests/MonsterFootball.Simulation.Tests/Actions/ClearanceFlowTests.cs`.

- [ ] **Step 1: Write the failing tests**

```csharp
using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Observation;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

/// <summary>Strike → flight → landing or leaving play, with events and state checked.</summary>
public class ClearanceFlowTests
{
    private static ClearanceResult Strike(Scenario s, PlayingPosition slot)
    {
        var kicker = s.GiveBall(TeamSide.Home, slot);
        var result = new ClearanceResolver().Resolve(s.State, new ClearanceIntent(kicker.PlayerId), s.Context.Random);
        ClearanceExecution.Launch(s.Context, result);
        return result;
    }

    private static void Fly(Scenario s)
    {
        var flight = new BallFlightSystem();
        while (s.State.Ball.IsInFlight && s.State.Phase == MatchPhase.OpenPlay)
        {
            s.State.Clock.Advance();
            flight.Apply(s.Context);
            MatchStateInvariants.Verify(s.State);
        }
    }

    [Fact]
    public void A_clearance_flies_and_lands_loose_and_rolling()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        var result = Strike(s, PlayingPosition.LCB);

        Assert.Equal(new BallTouch(result.Kicker, TeamSide.Home), s.State.LastTouch);
        var frame = MatchFrame.Capture(s.State);
        Assert.Equal(BallFlightKind.Clearance, frame.Ball.Flight!.Kind);
        Assert.Null(frame.Ball.Flight.IntendedReceiver);

        Fly(s);

        Assert.True(s.State.Ball.IsLoose);
        Assert.NotNull(s.State.Ball.Roll);
        Assert.Equal(new Vec2(55, 34), s.State.Ball.Position);
        Assert.Equal([MatchEventType.Clearance], s.EventTypes);
        var clearance = s.Log.Events.Single();
        Assert.Equal("Long", clearance.Metadata["kind"]);
        Assert.Equal(1, Statistics.MatchStatistics.FromEvents(s.Log.Events).Home.Clearances);
    }

    [Fact]
    public void A_clearance_over_the_touchline_gives_the_opponents_a_throw_in()
    {
        // Wide: aim (35, 0); a lateral draw of 0 pushes the landing 8 m to the right of the aim, over the line.
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.5));
        s.Place(TeamSide.Home, PlayingPosition.LCB, 10, 20);
        s.Place(TeamSide.Away, PlayingPosition.ST, 11, 20);
        Strike(s, PlayingPosition.LCB);

        Fly(s);

        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(RestartType.ThrowIn, stoppage.Restart);
        Assert.Equal(TeamSide.Away, stoppage.AwardedTeam);
    }

    [Fact]
    public void An_attacker_offside_when_the_clearance_is_struck_is_penalised_for_collecting_it()
    {
        var s = new Scenario(random: new ScriptedRandom(0.5, 0.5));
        foreach (var p in s.State.TeamPlayers(TeamSide.Away).Where(p => !p.Role.IsGoalkeeper()).Select((p, i) => (p, i)))
            s.Place(TeamSide.Away, p.p.Role, 45, 4 + 6 * p.i); // the Away line at x = 45
        s.Place(TeamSide.Home, PlayingPosition.LCB, 20, 34);
        var runner = s.Place(TeamSide.Home, PlayingPosition.RW, 56, 34); // beyond the line in the opponents' half
        Strike(s, PlayingPosition.LCB);

        Fly(s);
        new LooseBallSystem().Apply(s.Context);

        var stoppage = Assert.IsType<Stoppage>(s.State.CurrentStoppage);
        Assert.Equal(StoppageReason.Offside, stoppage.Reason);
        Assert.Equal(runner.PlayerId, stoppage.Offender);
    }
}
```
(If the `BallTouch` constructor, `Stoppage` type or `StoppageReason`/`RestartType` namespaces differ, adapt the test to the actual types — read `Core/BallTouch.cs` and `Core/Stoppage.cs` first.)

- [ ] **Step 2: Run to verify they fail** — `dotnet test MonsterFootball.slnx --filter "FullyQualifiedName~ClearanceFlowTests"` → build errors (`ClearanceExecution`, `BallFlightKind.Clearance`, `MatchEventType.Clearance`, `TeamStatistics.Clearances` missing). Add the statistics part in this task too (Step 3e) so the test compiles.

- [ ] **Step 3: Implement**

a) `Events/MatchEventType.cs` — append after `MatchAbandoned`:
```csharp

    // Actions (M2.7b)
    Clearance,
```

b) `Actions/BallFlight.cs` — a third kind. Replace the class body's constructor/factories/properties with:
```csharp
    private BallFlight(PassResult? pass, ShotResult? shot, ClearanceResult? clearance, long launchTick, long arrivalTick)
    {
        Pass = pass;
        Shot = shot;
        Clearance = clearance;
        LaunchTick = launchTick;
        ArrivalTick = arrivalTick;
    }

    public static BallFlight ForPass(PassResult pass, long launchTick, long arrivalTick) => new(pass, null, null, launchTick, arrivalTick);

    public static BallFlight ForShot(ShotResult shot, long launchTick, long arrivalTick) => new(null, shot, null, launchTick, arrivalTick);

    public static BallFlight ForClearance(ClearanceResult clearance, long launchTick, long arrivalTick) => new(null, null, clearance, launchTick, arrivalTick);

    public PassResult? Pass { get; }
    public ShotResult? Shot { get; }
    public ClearanceResult? Clearance { get; }
    public long LaunchTick { get; }
    public long ArrivalTick { get; }

    /// <summary>The team that struck the ball.</summary>
    public TeamSide Team => Pass?.Team ?? Shot?.Team ?? Clearance!.Team;

    public Vec2 StartPosition => Pass?.StartPosition ?? Shot?.StartPosition ?? Clearance!.StartPosition;

    /// <summary>Where the ball arrives (the actual landing point, not necessarily the intended one).</summary>
    public Vec2 TargetPosition => Pass?.TargetPosition ?? Shot?.TargetPosition ?? Clearance!.TargetPosition;
```
Add `using MonsterFootball.Simulation.Actions.Clearing;`; summary: "A pass, a shot or a clearance … Exactly one of Pass, Shot and Clearance is set."

c) `Actions/Clearing/ClearanceExecution.cs`:
```csharp
using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Clearing;

/// <summary>Applies a resolved clearance: launches it, and later lands it loose or takes it out of play (ADR-035).</summary>
internal static class ClearanceExecution
{
    /// <summary>Launches the clearance; offside is judged now, as for a pass (ADR-029).</summary>
    public static void Launch(MatchContext context, ClearanceResult result)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        state.LaunchBall(BallFlight.ForClearance(result, tick, tick + BallFlight.TravelTicks(result.TravelDuration, context.Config.TickSeconds)));
        if (context.Config.OffsideEnabled)
            state.Offside = Laws.Offside.Judge(state, state.Player(result.Kicker));

        context.Emit(MatchEventType.Clearance,
            actor: result.Kicker,
            team: result.Team,
            position: result.StartPosition,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("distance", result.Distance.ToString("0.###", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("kind", result.Kind.ToString()),
            ]));
    }

    /// <summary>The ball lands loose and rolls on, like a misplaced pass (D4).</summary>
    public static void Land(MatchContext context, ClearanceResult result)
    {
        var state = context.State;
        var travelled = result.TargetPosition - result.StartPosition;
        var roll = LooseBallMotion.Covering(result.TargetPosition, travelled, PassExecution.RollOnFraction * travelled.Length, state.Clock.Tick);
        state.SetBallRolling(result.TargetPosition, roll);
    }

    /// <summary>A clearance whose flight crosses a line goes out there; the clearer touched it last.</summary>
    public static void LeavePlay(MatchContext context, Vec2 crossing, Laws.BoundaryLine line) =>
        Laws.OutOfPlay.Stop(context, crossing, line);
}
```

d) `Actions/BallFlightSystem.cs` — `Apply` becomes:
```csharp
        var next = state.Clock.Tick < flight.ArrivalTick ? flight.PositionAt(state.Clock.Tick) : flight.TargetPosition;
        var crossing = flight.Shot is null ? Laws.OutOfPlay.Crossing(state.Pitch, state.Ball.Position, next) : null;
        if (crossing is { } leaving)
        {
            if (flight.Pass is { } pass)
                PassExecution.LeavePlay(context, pass, leaving.Point, leaving.Line);
            else
                ClearanceExecution.LeavePlay(context, leaving.Point, leaving.Line);
        }
        else if (state.Clock.Tick < flight.ArrivalTick)
            state.Ball.MoveInFlight(flight.PositionAt(state.Clock.Tick));
        else if (flight.Pass is { } landing)
            PassExecution.Land(context, landing);
        else if (flight.Clearance is { } clearance)
            ClearanceExecution.Land(context, clearance);
        else
            ShotExecution.Land(context, flight.Shot!);
```
(update the class summary: "A misplaced pass or a clearance whose path leaves the pitch goes out of play where it crosses the line"; add the `Actions.Clearing` using).

e) `Statistics/MatchStatistics.cs` — `TeamStatistics`: add `public int Clearances { get; init; }` (after `RedCards`); `Tally`: `public int Clearances;` and `Clearances = Clearances,` in `ToStatistics`; in the event switch:
```csharp
                case MatchEventType.Clearance:
                    Of(e.Team!.Value).Clearances++;
                    break;
```

f) `Core/MatchState.cs` `LaunchBall`: `RecordTouch(flight.Pass?.Passer ?? flight.Shot?.Shooter ?? flight.Clearance!.Kicker);`

g) `Observation/MatchFrame.cs`: add `Clearance,` to `BallFlightKind`, and in `Capture`:
```csharp
            ? new BallFlightFrame(
                f.Pass is not null ? BallFlightKind.Pass : f.Shot is not null ? BallFlightKind.Shot : BallFlightKind.Clearance,
                f.Team, f.StartPosition, f.TargetPosition, f.LaunchTick, f.ArrivalTick, f.Pass?.IntendedReceiver)
```

h) `AI/OnBallDecisionSystem.cs`: add a `ClearanceResolver clearanceResolver` constructor parameter (last) and the case
```csharp
            case ClearanceIntent clearance:
                ClearanceExecution.Launch(context, clearanceResolver.Resolve(state, clearance, context.Random));
                break;
```
`MatchSimulation.cs`: pass `new ClearanceResolver()` as the new last argument.

- [ ] **Step 4: Run the filter (all pass), then the full suite (all pass).** Nothing chooses a clearance yet, so full matches are unchanged.

- [ ] **Step 5: Confirm fingerprints are identical to `main`, then commit**
```bash
git worktree add "$TMPDIR/clear-base" main
(cd "$TMPDIR/clear-base" && dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3) > "$TMPDIR/fp-main.txt"
dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3 > "$TMPDIR/fp-branch.txt"
diff "$TMPDIR/fp-main.txt" "$TMPDIR/fp-branch.txt" && echo IDENTICAL
git worktree remove --force "$TMPDIR/clear-base"
dotnet format MonsterFootball.slnx --verify-no-changes
git add -A src tests
git commit -m "feat(actions): clearance flight and execution

BallFlight gains a third kind. A clearance records the clearer's touch,
is judged for offside when struck, emits Clearance (kind, distance), goes
out where its path crosses a line and otherwise lands loose and rolling.
Frames show it as BallFlightKind.Clearance; clearances are counted per
team. The AI never clears yet; fingerprints identical.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Reporting

**Files:** `Diagnostics/MatchReport.cs`, `Diagnostics/BatchSummary.cs`, `src/MonsterFootball.DebugRunner/Program.cs`; test: extend the existing `tests/.../Diagnostics/MatchReportTests.cs` only if it already checks per-type lines (read it first; otherwise no new test — these are formatting lines).

- [ ] **Step 1: Implement**
  - `MatchReport` event description switch: `MatchEventType.Clearance => $"Clearance ({e.Metadata.GetValueOrDefault("kind", "?").ToLowerInvariant()}) by {P(e.ActorPlayerId)}",` (not in `Highlights`).
  - `MatchReport.StatisticsTable`: after the `("Fouls", …)` row add `("Clearances", N(stats.Home.Clearances), N(stats.Away.Clearances)),`.
  - `BatchSummary`: `public double AverageClearances { get; init; }` and `AverageClearances = ordered.Average(s => s.Statistics.Home.Clearances + s.Statistics.Away.Clearances),` next to the fouls lines.
  - `Program.cs` `FormatBatch`: after the fouls line add `string.Create(CultureInfo.InvariantCulture, $"Clearances per match       {b.AverageClearances:0.0}"),`.

- [ ] **Step 2: Full suite passes; `dotnet run --project src/MonsterFootball.DebugRunner -- match 7` prints a `Clearances 0 0` row.**

- [ ] **Step 3: Commit**
```bash
dotnet format MonsterFootball.slnx --verify-no-changes
git add -u
git commit -m "feat(diagnostics): report clearances

Report line and statistics row; batch average. Fingerprints identical.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Defenders clear under pressure

**Files:** `Tactical/TacticalZones.cs`, `AI/ActionCandidate.cs` (`ActionKind`), `AI/AiTuning.cs` (`OnBallWeights`), `AI/OnBallCandidateGenerator.cs`, `Positioning/TeamShapeSystem.cs`, `Diagnostics/BatchHealth.cs`; test `tests/MonsterFootball.Simulation.Tests/AI/ClearanceDecisionTests.cs`.

- [ ] **Step 1: Write the failing tests**

```csharp
using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Movement;
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
```
(The clearance aims towards the Home ST, landing at ≈ (54.3, 40.9): the ST at (50, 40) is the nearest Home outfielder, ≈ 4.4 m; the Away LCB at (58, 30) the nearest Away one, ≈ 11.5 m — everyone else is parked on the goal lines. Check `Generate`'s exact signature and parameter name for held seconds in `OnBallCandidateGenerator` and adapt.)

- [ ] **Step 2: Run to verify they fail** — `dotnet test MonsterFootball.slnx --filter "FullyQualifiedName~ClearanceDecisionTests"` → build errors (`DefensiveThirdX`, `ActionKind.Clear` missing).

- [ ] **Step 3: Implement**
  - `TacticalZones`: next to `FinalThirdX` add `public double DefensiveThirdX => Pitch.Length / 3;` with a summary like its neighbour's ("team frame: the defending team's own third ends here").
  - `ActionKind`: add `Clear,` after `Shoot` in the on-the-ball group.
  - `OnBallWeights` (after the carry block):
```csharp

    // Clear (own defensive third only; D6, ADR-035).
    public double ClearBase { get; init; } = 20;
    public double ClearPressureWeight { get; init; } = 40;
    public double ClearDangerWeight { get; init; } = 30;

    /// <summary>Danger is 1 on the own goal line and 0 this far from the own goal centre.</summary>
    public double ClearDangerRange { get; init; } = 35;
```
  - `OnBallCandidateGenerator.Generate`, before `return candidates;`:
```csharp
        if (local.X < state.Zones.DefensiveThirdX)
        {
            var danger = Math.Max(0, 1 - carrier.Position.DistanceTo(pitch.OwnGoalCentre(carrier.Team)) / W.ClearDangerRange);
            var utility = W.ClearBase + W.ClearPressureWeight * pressure + W.ClearDangerWeight * danger;
            candidates.Add(new ActionCandidate(ActionKind.Clear, utility, new ClearanceIntent(carrier.PlayerId)));
        }
```
    and add "clear (own third)" to the class summary's list of options with its rough scale.
  - `TeamShapeSystem.Apply`, after the pass-receiver block:
```csharp
        // A clearance in the air: the nearest outfield player of each side races to where it will land (ADR-035).
        if (state.Ball.Flight is { Clearance: { } clearance })
        {
            var landing = state.Pitch.Clamp(clearance.TargetPosition);
            foreach (var side in Enum.GetValues<TeamSide>())
            {
                var chaser = state.TeamPlayers(side).Where(p => !p.Role.IsGoalkeeper()).MinBy(p => p.Position.DistanceTo(landing));
                if (chaser is null)
                    continue;
                chaser.MovementTarget = landing;
                chaser.MovementIntensity = MovementIntensity.Sprint;
            }
        }
```
    (A sprinting player is left alone by the off-ball AI, so the chase holds.)
  - `BatchHealth`: after the fouls band add `new("Clearances per match", s.AverageClearances, 10, 80),`.

- [ ] **Step 4: Run the filter (all pass), then the full suite.** If an existing full-match test fails because a seeded match now plays out differently, stop and report it (test, assertion, diagnosis) — do not change seeds or production code to satisfy it.

- [ ] **Step 5: Commit**
```bash
dotnet format MonsterFootball.slnx --verify-no-changes
git add -A src tests
git commit -m "feat(ai): defenders clear under pressure

In their own third the carrier may clear (utility 20 + 40 x pressure +
30 x danger). While a clearance flies, the nearest outfield player of
each side sprints to where it will land. New health band: clearances
10-80 per match (D6, ADR-035). Fingerprints change.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Measure (controller)

- [ ] `dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000 > $SCRATCHPAD/clearances-after.txt`; must be HEALTHY.
- [ ] Probe (scratchpad): ball-out restarts by preceding event, now including `Clearance`; touchline share.
- [ ] Success criteria: 20–50 clearances per match; throw-ins up from 23.4; ball in play down a little; goals ≥ ≈ 1.9. If clearances fall outside 20–50, adjust only `ClearBase` / `ClearPressureWeight` / `ClearDangerWeight` with a batch per change and record each try; anything else → report to the user first.

---

### Task 6: Docs (ADR-035)

- [ ] `DECISIONS.md` — ADR-035 "Defenders Clear Under Pressure" (status M2.7b step 2, implements D6): decision (candidate and utility; wide/long aim, distance, scatter, flight speed; offside at the strike; loose landing; out by last touch; the chase), reason (probe: throw-ins only from misplaced passes after width; real ≈ 35–45 clearances), consequences (ledger #12; batch before → after; any tuning tries).
- [ ] `MATCH_SIMULATION.md` — status line; on-ball AI candidate table/list (Clear); a "Clearances (implemented)" section (resolver, execution, chase); ball-flight paragraph (three kinds); events list (26 values, `Clearance`); statistics table (Clearances); batch figures and health bands (clearances 10–80); limitations (no blocked clearances, headers or punts).
- [ ] `LAWS_OF_THE_GAME.md` — in *Ball out of play*, add clearances to "Ways the ball goes out".
- [ ] `GLOSSARY.md` — "**Clearance** — Kicking the ball away from danger to a spot rather than a teammate: wide towards the touchline under pressure near goal, otherwise long upfield (ADR-035)."
- [ ] `ARCHITECTURE.md` — `Actions.Clearing` row (`ClearanceIntent`, `ClearanceResult`, `ClearanceKind`, `ClearanceResolver`, `ClearanceExecution`), M2.7b; `Tactical` row mentions `DefensiveThirdX`.
- [ ] `MILESTONE_2_PLAN.md` — ledger row `| 12 | 7b2 | clearances |`.
- [ ] `CURRENT_STATE.md` — current subtask (step 2 clearances, ADR-035), what exists, tests count, ADR range, known issues with new figures, next work (step 3 crosses).
- [ ] Commit `docs: clearances (ADR-035)`.

---

### Task 7: Verify, review, PR (controller)

- [ ] Format, Release build (0 warnings), full suite.
- [ ] Final code review of `main...feat/m2-7b-clearances`; apply follow-ups.
- [ ] Push; open PR "M2.7b step 2: defenders clear under pressure" (what/why, commits, before → after batch, probe, tests, trailer line); wait for CI; ask the user to approve the merge.
