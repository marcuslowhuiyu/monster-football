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
MonsterFootball.Godot (godot/)  ──┐
MonsterFootball.DebugRunner     ──┼──▶  MonsterFootball.Simulation
MonsterFootball.Simulation.Tests ─┘         (no outward dependencies)
```

| Project | Path | Role |
|---|---|---|
| `MonsterFootball.Simulation` | `src/MonsterFootball.Simulation` | Headless football simulation. Plain .NET, no Godot. |
| `MonsterFootball.DebugRunner` | `src/MonsterFootball.DebugRunner` | Console app: run one match / batch runs, print events. |
| `MonsterFootball.Simulation.Tests` | `tests/MonsterFootball.Simulation.Tests` | xUnit tests, including architecture rules. |
| `MonsterFootball.Godot` | `godot/` | Presentation placeholder. Empty in Milestone 1. |

Dependencies point **inward** to the simulation only. The simulation knows nothing about its consumers.
This rule is enforced by `Architecture/DependencyRulesTests` (ADR-003).

Namespaces inside the simulation are created as subtasks need them, not ahead of time:

| Namespace | Contents | Status |
|---|---|---|
| `Core` | `MatchState`, `BallState`, `Score`, `MatchPhase`, `PitchGeometry`, `Vec2`, `TeamSide`, `MatchStateInvariants` | M1.2 |
| `Players` | `PlayerId`, `PlayerStats`, `PlayerDefinition`, `PlayingPosition`, `TeamDefinition`, `PlayerMatchState` | M1.2 |
| `Random` | `IMatchRandom`, `MatchRng` | M1.3 |
| `Events` | `MatchEvent`, `MatchEventType`, `MatchEventLog` | M1.3 |
| `Positioning` | `Formation433`, `TeamFrame`, `TeamShapeSystem`, `GoalkeeperPositioning` | M1.3–M1.4 |
| `Movement` | `MovementSystem` | M1.4 |
| root | `MatchSimulation`, `MatchResult` | M1.3 |
| `Core` additions | `MatchConfig`, `MatchClock`, `MatchTime` | M1.3 |
| `Actions` | `BallFlightSystem`, `LooseBallSystem`; `Actions.Passing`: `PassIntent`, `PassResolver`, `PassResult`, `BallFlight`, `PassTargetSelector`, `PassExecution` | M1.5 |
| `Actions` (M1.6) | `ActionIntent`; `Actions.Carrying`: `CarryIntent`, `CarryResult`, `CarryResolver`, `CarrySystem`; `Actions.Tackling`: `TackleIntent`, `TackleResult`, `TackleResolver`, `TackleSystem` | M1.6 |
| `AI` | `OnBallDecisionSystem` (placeholder pass/carry choice) | M1.5–M1.6 |
| root | `MatchContext` | M1.5 |
| `Core` additions | `Curves` | M1.5 |
| `Statistics` | — | planned |

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

Per-tick behaviour lives in small `internal sealed` system classes owned by `MatchSimulation` (e.g. `TeamShapeSystem`, `MovementSystem`, `BallFlightSystem`), each with an `Apply(…)` method. Systems that use randomness or emit events take a `MatchContext` (state + config + `IMatchRandom` + event output); purely positional systems take `MatchState`. `MatchSimulation` decides the order they run in; systems never call each other. This keeps each behaviour independently testable and makes the tick order explicit in one place.

## 4. Event architecture

The simulation emits immutable, structured `MatchEvent` records into an ordered log exposed by `MatchSimulation`.
Future consumers (commentary, statistics, replays, Opportunity detection) read the log; they do not alter outcomes.

Events carry the simulated timestamp, never wall-clock time. Metadata is a sorted map so an event's canonical string form is deterministic. Details: `MATCH_SIMULATION.md`.

## 5. Action pattern

Player AI → Intent → Resolver → Result → MatchState update → MatchEvent.
Each action type (pass, carry, tackle, shot) follows this pattern so future modifiers (roles, traits, cards) can hook in at the intent-scoring and resolution stages.

- **Resolvers are read-only**: they take state + intent (+ `IMatchRandom`) and return a result record. They never mutate state, so they can be unit-tested and called speculatively.
- **Estimates are shared**: a resolver exposes an RNG-free `Estimate` that both resolution and AI target selection use, so the AI judges options with the same maths that decides outcomes.
- **Execution is separate**: a small execution step applies the result to state and emits events.
- **Determinism-safe maths** only in outcome code (ADR-013).

## 6. Testing philosophy

- Football resolution systems require automated tests.
- Statistical behaviour (e.g. easy passes succeed more often) is tested with fixed seeds over many trials.
- Reusable invariant checks validate state after each tick in tests.
- Architecture rules (dependency direction) are tests, not conventions.

## 7. Extension points (documented, not implemented)

- Utility-score modifiers from roles, traits, tactics, manager instructions, species and cards.
- Opportunity detection as an event-log consumer.
- Presentation playback driven by `Step()` and event presentation data (start/target position, duration, quality).
