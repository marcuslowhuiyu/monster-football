# Architecture

This document records accepted technical architecture and **why** it is shaped this way.
Decisions referenced here are recorded in `DECISIONS.md`.

## 1. Simulation / presentation separation

The simulation owns game truth (ADR-001). The flow for every football action is:

1. the simulation decides the outcome;
2. the simulation updates authoritative state;
3. the simulation emits events;
4. presentation (later) visualises those results.

Presentation never feeds back into outcomes. Godot physics, collisions and animation timing are cosmetic.

Why: determinism, testability, and the ability to run thousands of matches headlessly to tune balance.

## 2. Projects and dependency direction

```text
MonsterFootball.Godot (godot/) ───┬──▶ MonsterFootball.Playback ──┐
MonsterFootball.Simulation.Tests ─┤                               ├──▶  MonsterFootball.Simulation
                                  └───────────────────────────────┤        (no outward dependencies)
MonsterFootball.DebugRunner ──────────────────────────────────────┘
```

| Project | Path | Role |
|---|---|---|
| `MonsterFootball.Simulation` | `src/MonsterFootball.Simulation` | Headless football simulation. Plain .NET, no Godot. |
| `MonsterFootball.DebugRunner` | `src/MonsterFootball.DebugRunner` | Console app: run one match / batch runs, print events. |
| `MonsterFootball.Simulation.Tests` | `tests/MonsterFootball.Simulation.Tests` | xUnit tests, including architecture rules. |
| `MonsterFootball.Playback` | `src/MonsterFootball.Playback` | Non-visual playback for presentation: frame history, playback clock, interpolation (ADR-026). Plain .NET, no Godot. |
| `MonsterFootball.Godot` | `godot/` | Developer match viewer (`MatchViewer/`): draws frames from a `PlaybackSession`. Launch with `tools/viewer.sh`. |

Dependencies point **inward** to the simulation only. The simulation knows nothing about its consumers, and
Playback knows nothing about Godot. Enforced by `Architecture/DependencyRulesTests` (ADR-003, ADR-026).

### Observation and playback (M2.2)

Consumers observe a match through `Observation.MatchFrame`, an immutable copy of the state at the end of a tick
(ADR-025), never through the live `MatchState`. `MatchPlayer` records a frame per tick and steps the simulation only
when a frame is needed; `PlaybackClock` turns real time into a position in ticks at 0.25×–4×; `FrameInterpolator`
blends the frames either side of that position; `PlaybackSession` combines them. Godot calls
`PlaybackSession.Update(delta)` every rendered frame and draws the result, so rendering never advances or resolves
football by itself (milestone §35). Godot physics is not used.

Namespaces inside the simulation are created as subtasks need them, not ahead of time:

| Namespace | Contents | Status |
|---|---|---|
| `Core` | `MatchState`, `BallState`, `Score`, `MatchPhase`, `PitchGeometry`, `Vec2`, `TeamSide`/`TeamSideExtensions`, `MatchStateInvariants`/`InvariantViolationException` | M1.2 |
| `Players` | `PlayerId`, `PlayerStats`, `PlayerDefinition`, `PlayingPosition`/`PositionGroup`/`PlayingPositionExtensions`, `TeamDefinition`/`InvalidTeamException`, `PlayerMatchState`, `MovementIntensity`; `PlayerStatus` | M1.2–M1.11, M2.7 |
| `Random` | `IMatchRandom`, `MatchRng` | M1.3 |
| `Events` | `MatchEvent`, `MatchEventType`, `MatchEventLog` | M1.3 |
| `Positioning` | `Formation433`, `TeamFrame`, `TeamShapeSystem`, `GoalkeeperPositioning`; `ShapeTemplates` (reduced-team shapes); `WideChannel` (wide roles' touchline channel) | M1.3–M1.4, M2.7, M2.7b |
| `Movement` | `MovementSystem` | M1.4 |
| root | `MatchSimulation`, `MatchResult` | M1.3 |
| `Core` additions | `MatchConfig`, `MatchClock`, `MatchTime` | M1.3 |
| `Core` additions | `Stoppage`, `StoppageReason`, `RestartType`; `MatchPhase` = `Stopped`/`RestartSetup`/`OpenPlay`/`Finished` | M2.1c |
| `Core` additions | `AddedTimeTuning`; period-aware `MatchClock` and `MatchTime` | M2.1d |
| `Periods` | `PeriodSystem` (added time, end of a period at a stoppage, half-time, full time) | M2.1d |
| `Tactical` | `LongitudinalBand`, `VerticalChannel`, `TacticalZone`, `TacticalAreas`, `ZoneBoundaries`, `TacticalZones` (`MatchState.Zones`); `Spatial` (shared pure geometry: `GoalSideCount`) | M2.3, M2.7 |
| `Observation` | `MatchFrame`, `PlayerFrame`, `BallFrame`, `BallFlightFrame` (immutable per-tick copies for observers) | M2.2 |
| `Actions` | `BallFlight` (pass or shot, M2.1b), `BallFlightSystem`, `LooseBallSystem`, `LooseBallMotion` (rolling ball, M2.4); `Actions.Passing`: `PassIntent`, `PassResolver`, `PassEstimate`, `InterceptionThreat`, `PassResult`, `PassOutcome`, `PassExecution` | M1.5 |
| `Actions` (M1.6) | `ActionIntent`; `Actions.Carrying`: `CarryIntent`, `CarryResult`, `CarryResolver`, `CarrySystem`; `Actions.Tackling`: `TackleIntent`, `TackleResult`, `TackleOutcome`, `TackleResolver`, `TackleSystem` | M1.6 |
| `Actions.Shooting` | `ShotIntent`, `ShotResult`, `ShotOutcome`, `ShotEstimate`, `ShotResolver`, `ShotExecution` | M1.7 |
| `Restarts` | `RestartSystem` (dispatch on restart type: dead ball → setup → restart), `RestartLayout` (restart positions, law distances), `RestartTakers`, `RestartTuning` | M1.7, M2.1c, M2.4 |
| `Laws` | `OutOfPlay` (segment–boundary crossing, restart from line and last touch), `BoundaryLine`; `Offside` (line, position, judgement at the pass), `OffsideJudgement`; `Discipline` (cards for fouls), `CardReason` | M2.4–M2.7 |
| `AI` | `ActionCandidate`/`ActionKind`, `UtilitySelector`, `IUtilityModifier`, `AiTuning`/`OnBallWeights`/`OffBallWeights` (weights), `OnBallCandidateGenerator` + `OnBallDecisionSystem`, `OffBallCandidateGenerator` + `OffBallDecisionSystem` + `OffBallPlan` | M1.9 |
| `Fitness` | `FatigueModel`, `StaminaSystem` | M1.8 |
| root | `MatchContext` | M1.5 |
| `Core` additions | `Curves` | M1.5 |
| `Statistics` | `MatchStatistics`, `TeamStatistics` (derived from events) | M1.10 |
| `Diagnostics` | `MatchFingerprint` (determinism), `MatchReport` (debug log + statistics table), `MatchSummary`, `BatchSummary`, `BatchHealth` (batch figures and pathology bands) | M1.9–M1.11 |

### State ownership and mutation

- Definitions (`PlayerDefinition`, `TeamDefinition`, `PlayerStats`) are immutable and validated on construction. Invalid teams cannot exist.
- Match state (`MatchState`, `PlayerMatchState`, `BallState`, `Score`) is publicly readable but **mutated only through `internal` members**. Outside code (presentation, runner) can observe the match but cannot change it. Tests reach internals via `InternalsVisibleTo`.
- Facts are stored once and derived elsewhere — e.g. possession lives only on the ball (ADR-007).

## 3. Determinism requirements

Given identical starting state, configuration and seed, the simulation must produce identical results and event histories.

- All randomness goes through a single match-owned `IMatchRandom` (ADR-005).
- No wall-clock time, `System.Random`, `Guid.NewGuid()` or unordered-collection iteration that could affect outcomes. The type-level ban is enforced by `Architecture/DeterminismGuardTests` (ADR-009).
- The random algorithm is owned by the project (xoshiro256**), not the runtime, so seeds stay valid across .NET upgrades.
- Simulation advances on a fixed timestep, independent of rendering framerate.

### Systems

Per-tick behaviour lives in small `internal sealed` system classes owned by `MatchSimulation` (e.g. `TeamShapeSystem`, `MovementSystem`, `BallFlightSystem`), each with an `Apply(…)` method. Systems that use randomness or emit events take a `MatchContext` (state + config + `IMatchRandom` + event output); purely positional systems take `MatchState`. `MatchSimulation` decides the order they run in; systems never call each other's `Apply`. They may share small static execution helpers (`PassExecution`, `ShotExecution`, `CarrySystem.Start`) and `RestartSystem` exposes `Position`/`Advance` for stopped ticks (ADR-023). This keeps each behaviour independently testable and makes the tick order explicit in one place.

## 4. Event architecture

The simulation emits immutable, structured `MatchEvent` records into an ordered log exposed by `MatchSimulation`.
Future consumers (commentary, statistics, replays, Opportunity detection) read the log; they do not alter outcomes.

Events carry the simulated timestamp, never wall-clock time. Statistics are the first external consumer: `MatchStatistics.FromEvents` derives every figure from the log (ADR-019). Metadata is a sorted map so an event's canonical string form is deterministic. Details: `MATCH_SIMULATION.md`.

## 5. Action pattern

Player AI → Intent → Resolver → Result → MatchState update → MatchEvent.
Each action type (pass, carry, tackle, shot) follows this pattern so future modifiers (roles, traits, cards) can hook in at the intent-scoring and resolution stages.

- **Resolvers are read-only**: they take state + intent (+ `IMatchRandom`) and return a result record. They never mutate state, so they can be unit-tested and called speculatively.
- **Estimates are shared**: resolvers expose RNG-free assessments — `PassResolver.Estimate`, `ShotResolver.Estimate`, `TackleResolver.ContactProbability`/`CleanProbability`, `CarryResolver.ChooseDestination` — that both resolution and the AI use, so the AI judges options with the same maths that decides outcomes.
- **Execution is separate**: a small execution step applies the result to state and emits events.
- **Determinism-safe maths** only in outcome code (ADR-013).
- **Cross-cutting effects live in one model.** Fatigue is applied through `FatigueModel` factors inside resolvers; resolvers never compute fatigue themselves (ADR-016). Future modifiers (traits, cards, species) should follow the same pattern.

### Event consumers inside the simulation

Systems may read the event log as well as write it: `StaminaSystem` charges action costs from new events since its last run. Such consumers must process each event exactly once and must not reorder or modify events.

## 6. Testing philosophy

- Football resolution systems require automated tests.
- Statistical behaviour (e.g. easy passes succeed more often) is tested with fixed seeds over many trials.
- Reusable invariant checks validate state after each tick in tests.
- Architecture rules (dependency direction) are tests, not conventions.

## 7. Delivery pipeline

Every change reaches `main` through a pull request gated by CI (ADR-017): formatting, a Release build with warnings as errors and the full test suite on Ubuntu, Release builds on Windows and macOS, a cross-platform determinism comparison of match fingerprints (`Diagnostics.MatchFingerprint`), plus a 1 000-match batch whose health bands must all pass (ADR-020). Builds are reproducible: pinned SDK (`global.json`) and locked NuGet restores.

## 8. Extension points (documented, not implemented)

- Utility-score modifiers from roles, traits, tactics, manager instructions, species and cards: implement `IUtilityModifier` and pass it to `UtilitySelector` (ADR-018). The hook exists; no modifiers do.
- AI weights per team or per match: `MatchConfig.Ai` (`AiTuning`) is data and can be varied without code changes.
- Opportunity detection as an event-log consumer.
- Tactical analysis (M2.8+) reads `MatchFrame`s, like the viewer.
