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
| `Actions`, `AI`, `Events`, `Random`, `Statistics` | — | planned |

### State ownership and mutation

- Definitions (`PlayerDefinition`, `TeamDefinition`, `PlayerStats`) are immutable and validated on construction. Invalid teams cannot exist.
- Match state (`MatchState`, `PlayerMatchState`, `BallState`, `Score`) is publicly readable but **mutated only through `internal` members**. Outside code (presentation, runner) can observe the match but cannot change it. Tests reach internals via `InternalsVisibleTo`.
- Facts are stored once and derived elsewhere — e.g. possession lives only on the ball (ADR-007).

## 3. Determinism requirements

Given identical starting state, configuration and seed, the simulation must produce identical results and event histories.

- All randomness goes through a single match-owned `IMatchRandom` (ADR-005).
- No wall-clock time, `System.Random`, `Guid.NewGuid()` or unordered-collection iteration that could affect outcomes.
- Simulation advances on a fixed timestep, independent of rendering framerate.

## 4. Event architecture

The simulation emits immutable, structured `MatchEvent` records into an ordered log exposed by `MatchSimulation`.
Future consumers (commentary, statistics, replays, Opportunity detection) read the log; they do not alter outcomes.

*Implementation: M1.3 onward. Details will be documented in `MATCH_SIMULATION.md`.*

## 5. Action pattern

Player AI → Intent → Resolver → Result → MatchState update → MatchEvent.
Each action type (pass, carry, tackle, shot) follows this pattern so future modifiers (roles, traits, cards) can hook in at the intent-scoring and resolution stages.

## 6. Testing philosophy

- Football resolution systems require automated tests.
- Statistical behaviour (e.g. easy passes succeed more often) is tested with fixed seeds over many trials.
- Reusable invariant checks validate state after each tick in tests.
- Architecture rules (dependency direction) are tests, not conventions.

## 7. Extension points (documented, not implemented)

- Utility-score modifiers from roles, traits, tactics, manager instructions, species and cards.
- Opportunity detection as an event-log consumer.
- Presentation playback driven by `Step()` and event presentation data (start/target position, duration, quality).
