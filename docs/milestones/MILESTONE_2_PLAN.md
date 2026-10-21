# Milestone 2 — Implementation Plan

> **For agentic workers:** this is the milestone-level plan. Before each PR, write that PR's task-by-task TDD plan
> (superpowers:writing-plans) against the code as it exists at that moment, then execute it. Steps use checkbox
> syntax for tracking.

**Goal:** deliver `MILESTONE_2.md` (revision 2) — match flow and laws, tactical understanding, developer viewer —
as a sequence of small PRs, each green on CI, with every match-fingerprint change isolated and explained.

**Architecture:** the football tick gains a stoppage/restart cycle and law resolvers inside
`MonsterFootball.Simulation`; tactical analysis consumes immutable `MatchFrame`s outside the tick; a new plain-.NET
`MonsterFootball.Playback` library drives a thin Godot viewer.

**Tech stack:** .NET 10 / C# (pinned by `global.json`), xUnit, Godot 4.7.2 .NET, GitHub Actions (3 OS).

**ADR numbers** below are provisional; ADRs are numbered sequentially in `DECISIONS.md` when accepted
(possession causes became ADR-021).

**Working assumptions:** the open decisions in spec §54 (D1–D14) are taken as recommended unless changed; each PR
that implements one restates it and records the ADR.

---

## 1. Dependency direction

```text
MonsterFootball.Godot (godot/) ──┬──▶ MonsterFootball.Playback ──▶ MonsterFootball.Simulation
                                 └──────────────────────────────▶ MonsterFootball.Simulation
MonsterFootball.DebugRunner ────────────────────────────────────▶ MonsterFootball.Simulation
MonsterFootball.Simulation.Tests ──▶ Simulation, Playback
```

Inside the simulation:

```text
Laws, Restarts, Actions, AI, Positioning ──▶ Tactical.Zones, Tactical.Spatial (pure, input-agnostic)
                                         ──▶ Core, Players, Events
Observation (MatchFrame) ◀── Tactical (snapshot, play phase, observer) ◀── Tactical.Opportunities ◀── Diagnostics
```

Architecture tests (added in the step that introduces each rule):

1. football code never references `Tactical.Opportunities`, `TacticalSnapshot`, `TacticalObserver`, `Observation`;
2. `Tactical.*` (except the pure `Zones`/`Spatial` functions) never references `MatchState`, `MatchContext`,
   `IMatchRandom`, `MatchSimulation`;
3. `Playback` never references Godot; `Simulation` never references `Playback`.

Rules 1–2 concern types in the same assembly, so they are checked by reflecting over signatures and fields of
types in those namespaces (constructor/method parameters, fields, properties), plus the observation-purity test.

---

## 2. Core model changes (decided here, detailed per PR)

### 2.1 Match flow

```csharp
public enum MatchPhase { RestartSetup, OpenPlay, Stopped, Finished }      // replaces Kickoff/GoalRestart
public enum StoppageReason { MatchStart, PeriodStart, Goal, BallOutOfPlay, Foul, Offside, PeriodEnd }
public enum RestartType { Kickoff, ThrowIn, GoalKick, Corner, DirectFreeKick, IndirectFreeKick, Penalty }
public sealed record Stoppage(StoppageReason Reason, RestartType Restart, TeamSide AwardedTeam, Vec2 Location,
    long StoppedAtTick) { PlayerId? Offender; PlayerId? Fouled; DisciplinaryOutcome Discipline; }
```

- `Stopped` = dead ball for the restart's minimum dead-ball time; `RestartSetup` = players move to restart
  positions until ready; the **restart** is the action taken on the tick setup completes, which returns to `OpenPlay`.
- `MatchState.CurrentStoppage` holds the record while play is stopped.
- `RestartSystem` becomes a dispatcher over `RestartType` (one static handler per type), not an interface hierarchy.
- Events (appended): `PlayStopped` (stoppage record in metadata), `RestartTaken` (type), `PeriodEnded`,
  `AddedTimeAnnounced`, `FoulCommitted`, `Offside`, `YellowCard`, `RedCard`. `Kickoff` stays as the kickoff restart
  event so existing consumers keep working.

### 2.2 Clock and periods

- `MatchConfig`: `PeriodSeconds` (2700), `PeriodCount` (2), dead-ball minimums per restart type, added-time
  allowances (D3), safety cap (D2). `MatchDurationSeconds`/`TotalTicks` removed.
- `MatchClock`: tick stays authoritative; adds `Period`, `PeriodStartTick`; `Now` = period base + elapsed in period.
- `MatchTime` gains `Period` and displays `45+1:30` beyond regular time. Half-time takes no ticks (the referee's
  clock stops); the second half starts at 45:00 with a kickoff restart.
- End rule: once regular + added time has expired, the next transition into `Stopped` ends the period, unless the
  restart is a penalty (taken first). Safety cap: 3 min past added time, ends at the next tick with no ball in flight.
- Ball-in-play time and possession time are derived from events (stopped intervals excluded).

### 2.3 Players and teams

- `PlayerMatchState.Status` (`Active`, `SentOff`) and `Discipline` (yellow count, red).
- `MatchState.ActivePlayers` / `ActiveTeamPlayers(side)` (stable order) replace `Players`/`TeamPlayers` in every
  football system; `MatchState.Goalkeeper(side)` replaces "the GK slot" lookups (keeper replacement, D11).
- `Positioning.ShapeTemplates`: reduced-formation anchors by outfield count (10 → 4-4-1, 9 → 4-3-1, 8 → 3-3-1,
  7 → 3-2-1) with deterministic best-fit assignment of remaining players to template slots (minimise anchor
  distance; ties by `Players` order).

### 2.4 Ball

- `BallFlight` carries a `PassResult` **or** a `ShotResult` (shared `Team`, `Start`, `Target`, ticks).
- `MatchState.LastTouch` (player, team).
- `LooseBallMotion` (D4): after a misplaced pass or tackle loose ball, the ball rolls on along its direction with
  linear deceleration; position per tick is a closed-form function (no integration drift).
- Crossing detection: segment–boundary intersection for flights and rolls; the crossing point decides the restart.

### 2.5 Observation

- `Observation.MatchFrame` / `PlayerFrame` / `BallFrame` (immutable, public); `MatchFrame.Capture(MatchState)`.
- Shared geometry takes a minimal `PitchPlayers` view (id, team, slot, position, status) that adapters build from
  `MatchState` (laws) or `MatchFrame` (analysis), so concepts are implemented once.

---

## 3. PR sequence

`FP` = changes match fingerprints. Every FP PR records batch figures before/after in its description and ADR.

### M2.1 — Baseline and match-flow architecture

| PR | Branch | Scope | FP |
|---|---|---|---|
| 1a | `feat/m2-1-possession-cause` | `GiveBall(id, cause)`; `cause` metadata; `PossessionsWon` = open-play causes; report wording. ADR-029 (part). | yes — metadata text only; **every other batch figure identical** (proof of no football change) |
| 1b | `feat/m2-1-shot-flight` | shot `BallFlight`, outcome applied on arrival, keeper during flight. ADR-023. | yes |
| 1c | `feat/m2-1-stoppage-model` | `MatchPhase`/`Stoppage`/`RestartType`; kickoff and goal migrated; `PlayStopped`/`RestartTaken`; dead-ball time after goals. ADR-021. | yes |
| 1d | `feat/m2-1-periods` | two periods, half-time kickoff, added time, end-at-stoppage, safety cap, ball-in-play statistics, match-length tests rewritten. ADR-022. | yes |
| 1e | `docs/m2-1-presentation-review` | Godot 4.7.2 opened and built headlessly (needs `DOTNET_ROOT`, see README); match-flow docs landed with 1c/1d; pass-arrival visual check (§6.2) moves to M2.2 | no |

Note: until M2.4 the only stoppages are goals, so most periods end via the safety cap. Expected and temporary;
telemetry shows the cap rate falling to ~0 once the ball can go out.

### M2.2 — Frames and basic viewer

| PR | Branch | Scope | FP |
|---|---|---|---|
| 2a | `feat/m2-2-match-frame` | `MatchFrame` capture; frames never alter state (test); architecture rule 2 skeleton. ADR-031. | no |
| 2b | `feat/m2-2-playback` | `MonsterFootball.Playback`: `MatchPlayer` (lazy stepping, frame history, seek), `PlaybackClock`, `FrameInterpolator`; tests; CI includes it. ADR-033. | no |
| 2c | `feat/m2-2-godot-viewer` | `godot/MatchViewer`: pitch, players, ball, score, clock/period, in-play/stopped banner with reason, play/pause, speeds, restart/new seed. | no |

### M2.3 — 20-zone model

| PR | Branch | Scope | FP |
|---|---|---|---|
| 3a+3b | `feat/m2-3-tactical-zones` | One PR, two commits (CI budget): zone model — `LongitudinalBand`, `VerticalChannel`, `TacticalZone`, `TacticalAreas` flags, `ZoneBoundaries`, `TacticalZones` (`MatchState.Zones`), zone in match report details, `docs/TACTICAL_ANALYSIS.md`; then `TackleSystem` danger uses `FinalThird` instead of a 35 m radius (§6.5). ADR-027. | yes (second commit) |

### M2.4 — Ball out of play and restarts

Restart handlers land first (unit-tested via `Scenario`, unreachable in matches), so the match changes once.

| PR | Branch | Scope | FP |
|---|---|---|---|
| 4a–4f | `feat/m2-4-out-of-play` | One PR, five commits (CI budget): last touch; throw-in, goal kick and corner handlers (unreachable, fingerprints identical); the ball goes out of play — unclamped paths, `LooseBallMotion`, `Laws.OutOfPlay`, held/parried saves, lane deflections (D4, D5); restart statistics and health bands; docs incl. `LAWS_OF_THE_GAME.md`. ADR-028. | yes (third commit) |

### M2.5 — Offside

| PR | Branch | Scope | FP |
|---|---|---|---|
| 5a+5b | `feat/m2-5-offside` | One PR (CI budget): offside rules, judgement at the pass, involvement in `GiveBall`, restart exceptions, indirect free kick — dormant behind `MatchConfig.OffsideEnabled`; then enabled with offside-aware AI (passer margin and runner eagerness by Awareness, D8). ADR-029. | yes (second commit) |

### M2.6 — Fouls, free kicks, penalties

| PR | Branch | Scope | FP |
|---|---|---|---|
| 6a+6b | `feat/m2-6-fouls` | One PR (CI budget), stacked on M2.5: direct free kicks (wall, shoot or pass) and penalties (dedicated resolution, period waits) — dormant; then fouls from tackles with severity (D9). ADR-030. | yes (second commit) |

### M2.7 — Discipline, reduced teams, calibration

| PR | Branch | Scope | FP |
|---|---|---|---|
| 7a+7b+7c | `feat/m2-7-discipline` | One PR (CI budget), stacked on M2.6: reduced teams — roles, sent-off status, active-player iteration, `Goalkeeper(side)`, shape templates, invariants (ADR-031, fingerprint-neutral); cards — DOGSO, serious foul play, promising attack, reckless, second yellow, dismissal, abandonment (ADR-032); calibration checkpoint — safety cap, match-flow bands, figures (ADR-033). | yes (cards, safety cap) |

### M2.7b — Match realism (addition after the M2.7 checkpoint, ADR-033)

The user chose to aim for real football (≈ 40 throw-ins, 55–60 min of ball in play), not only the milestone bands.
Four building steps and a calibration pass, one PR each, specs and plans in `docs/superpowers/`; the interim bands of ADR-033 tighten to the milestone
targets by the end.

| PR | Branch | Scope | FP |
|---|---|---|---|
| b1 | `feat/m2-7b-width` | wide players hold the touchline in possession (shape, runs, support, carries). ADR-034. | yes |
| b2 | `feat/m2-7b-clearances` | D6: defenders under pressure in their own third clear long and wide; fair loose-ball ties. ADR-035. | yes |
| b3 | `feat/m2-7b-crosses` | wide players in the final third cross into the box; corners are crosses; aerial contest on landing. ADR-036 (provisionally tuned). | yes |
| b4 | `feat/m2-7b-dead-ball-times` | stoppage durations from real averages | yes |
| b5 | `chore/m2-7b-calibration` | calibration pass with all systems in place (goals, crosses, corners, throw-ins, ball in play); tighten the interim bands to the milestone targets (ADR-033) | if tuned |

### M2.8 — Tactical geometry and snapshot

| PR | Branch | Scope | FP |
|---|---|---|---|
| 8a | `feat/m2-8-tactical-geometry` | `Spatial`: lines, width/depth, compactness, pressure, numerical balance, space, lanes, goal-side; dismissal-aware; symmetry tests | no |
| 8b | `feat/m2-8-tactical-snapshot` | `TacticalHistory`, `TacticalSnapshot(Factory)`, `PlayPhase` classifier, formatter; determinism/no-mutation tests | no |

### M2.9 — Opportunity framework

| PR | Branch | Scope | FP |
|---|---|---|---|
| 9a | `feat/m2-9-opportunity-framework` | `Opportunity`, ids, `OpportunityTracker` (confirm 2 ticks, grace 1, cooldown 3 s, max duration, end on stoppage), `TacticalObserver`, observation stream, test-only fixture detector, **observation-purity test** (counting `IMatchRandom` decorator). ADR-032. | no |

### M2.10 — Detectors and telemetry

| PR | Branch | Scope | FP |
|---|---|---|---|
| 10a | `feat/m2-10-first-detectors` | Transition, WideIsolation, DefensiveEmergency | no |
| 10b | `feat/m2-10-remaining-detectors` | ThroughBallLane (onside runs only), Overload, FinalThirdEntry | no |
| 10c | `feat/m2-10-opportunity-telemetry` | `DebugRunner opportunities [n] [--examples TYPE N]`, summaries, health bands, opportunity fingerprint in CI. ADR-034 (part). | no |

### M2.11 — Viewer overlays

| PR | Branch | Scope | FP |
|---|---|---|---|
| 11a | `feat/m2-11-stoppage-overlays` | stoppage/restart detail, offside line at the call, cards, dismissed players, added time | no |
| 11b | `feat/m2-11-tactical-overlays` | zones, lines, targets, anchors, pressure, lanes, numbers; Opportunity panel, highlights, jump-to | no |

### M2.12 — Validation and acceptance

| PR | Branch | Scope |
|---|---|---|
| 12a+ | `fix/m2-12-…` | tuning from human review (laws, detectors) |
| 12z | `docs/m2-12-acceptance` | full regression, `M2_VALIDATION.md`, `MILESTONE_2_ACCEPTANCE.md`, progress report, docs cleanup |

---

## 4. Fingerprint-change ledger

| # | PR | Cause |
|---|---|---|
| 1 | 1a | `PossessionChanged` metadata gains `cause` |
| 2 | 1b | shot outcomes applied on arrival |
| 3 | 1c | goals become stoppages with dead-ball time |
| 4 | 1d | periods, half-time kickoff, added time |
| 5 | 3b | tackle danger area |
| 6 | 4 | ball out of play, rolling, parries, deflections |
| 7 | 5 | offside and offside-aware AI |
| 8 | 6 | fouls |
| 9 | 7b | cards and dismissals |
| 10 | 7 | calibration: safety cap 180 s |
| 11 | 7b1 | width |
| 12 | 7b2 | clearances; fair loose-ball ties |
| 13 | 7b3 | crosses; corners as crosses |

Each is verified cross-platform by CI and recorded in its ADR. Any other PR that changes a fingerprint is a bug.

---

## 5. Testing strategy

- **Per law:** pure rule functions tested in isolation (crossing → restart, offside position, foul probability
  direction, card thresholds); handlers tested with `Scenario`; full matches under `CheckedRun` (invariants every
  tick) with seeds and with forced events (scripted `IMatchRandom`, internal dismiss hook).
- **Symmetry:** every spatial rule tested for Home and mirrored Away.
- **Statistics:** every new statistic checked against the event log in full-match tests.
- **Match length:** tests assert the new rules (ends at a stoppage, added time from allowances), not 21 600 ticks.
- **Observation purity (§46):** seeds 1–5, observed vs unobserved, equal fingerprints and equal RNG draw counts.
- **Playback:** clock/speed/pause/seek/interpolation unit tests; render never steps beyond the needed tick.
- **Regression:** existing tests keep their intent; premise changes are rewritten with the reason in the PR.
- **CI:** unchanged gates plus the Playback project, opportunity fingerprint comparison, and the extended batch.

---

## 6. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Godot needed from M2.2 | Resolved: 4.7.2 .NET found at `~/Downloads/Godot_mono.app` |
| R2 | Restart volumes unrealistic (too few throw-ins/corners) | D4/D5 in 4f; measure at the 7c checkpoint; D6 clearance as fallback |
| R3 | Offside floods matches before the AI adapts | Enable offside and the AI changes in one PR (5b); offsides band |
| R4 | Restart setup stalls (players never "ready") | Setup timeout; invariant/test that every stoppage restarts within N s |
| R5 | Reduced-team refactor silently changes normal matches | 7a must be fingerprint-neutral — the hash is the test |
| R6 | Ten fingerprint changes blur cause and effect | Ledger §4; batch before/after per FP PR |
| R7 | Calibration drift (fewer shots/goals) trips existing bands | 7c revisits bands deliberately (ADR-020 contract) |
| R8 | Stamina/distance change with stoppages | Measure at 7c; D13 revisited there |
| R9 | Detector quality subjective | Human review protocol; frequency and accuracy tracked together |
| R10 | Per-tick analysis cost in batches | O(22²) per tick; analysis opt-in for batches; measure in 9a |
| R11 | `MatchFrame` grows into a second state model | Only fields with a current reader |
| R12 | Public API break (`MatchPhase`, `MatchTime`, match length) | Accepted (D14); all consumers updated in the same PR |
