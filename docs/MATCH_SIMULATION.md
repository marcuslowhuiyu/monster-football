# Match Simulation

Detailed documentation of the football simulation. Update whenever the simulation architecture materially changes.

**Status:** domain model (M1.2); loop, clock, RNG, phases, events (M1.3); movement and 4-3-3 shape (M1.4); passing (M1.5) implemented. Players hold shape, press and pass; nobody carries, tackles or shoots yet. "Design targets" below lists agreed designs from `milestones/MILESTONE_1.md` that are not yet built.

## Simulation API (implemented)

```csharp
var simulation = new MatchSimulation(MatchConfig.Default, homeTeam, awayTeam, seed: 123456);
simulation.Step();                          // advance one tick
MatchResult result = simulation.RunToCompletion();
```

- `State` (read-only `MatchState`), `Events` (ordered log), `IsFinished`, `Seed`.
- `MatchResult`: `HomeGoals`, `AwayGoals`, `Events`, `FinalState`, `Seed`. Statistics are added in M1.10.
- `Step()` after full time throws; `RunToCompletion()` is idempotent (ADR-010).
- An `internal` constructor accepts any `IMatchRandom`, so tests can script outcomes (`ScriptedRandom`).

## Fixed timestep and clock (implemented)

- `MatchConfig`: `Pitch` (default 105 × 68), `TickSeconds` (default 0.25 → 4 ticks per second), `MatchDurationSeconds` (default 5400). Both durations must be positive.
- `TotalTicks` = duration ÷ tick length, rounded **up** so a match never ends early (default 21 600).
- `MatchClock.Tick` (integer) is authoritative; `Now` (`MatchTime`, seconds) is derived as `Tick × TickSeconds`. The only mutation is `Advance()` (+1), so the clock cannot run backwards.
- `MatchTime` prints as `MM:SS` (`04:31`, `90:00`). No stoppage time.

## Tick order (implemented)

Each `Step()`:

1. If finished → throw.
2. First step only: coin toss (`Roll(0.5)`) picks `KickoffTeam`, emit `MatchStarted`.
3. Run the current phase:
   - `Kickoff` — kicking side's ST is placed on the centre spot and given the ball; emit `Kickoff`; phase → `OpenPlay`.
   - `OpenPlay` — in this order:
     1. `BallFlightSystem` — move a ball in flight; on its arrival tick, land the pass.
     2. `PassDecisionSystem` — carrier may pass (placeholder, see below).
     3. `TeamShapeSystem` — assign movement targets.
     4. `MovementSystem` — move all players.
     5. Sync a possessed ball to its carrier.
     6. `LooseBallSystem` — nearest player within 1.5 m collects a loose ball.
   - `GoalRestart` — reserved for goals (M1.7).
4. Advance the clock one tick.
5. If `Tick ≥ TotalTicks` → phase `Finished`, emit `MatchFinished` (timestamp 90:00).

Events emitted during a step carry the timestamp from **before** the clock advances, so the kickoff is at 00:00.

## Phases (implemented)

`Kickoff → OpenPlay → (GoalRestart → Kickoff → OpenPlay)* → Finished`. `GoalRestart` is defined but not yet entered.

## Deterministic RNG (implemented)

- All randomness goes through the match's single `IMatchRandom` (ADR-005). `MatchRng` is xoshiro256** seeded by SplitMix64 (ADR-009).
- Draw order is part of the outcome: adding, removing or reordering a draw changes every seeded match.
- Systems must iterate `MatchState.Players` (stable order), never hash-ordered collections, when order affects draws or outcomes.
- `DeterminismGuardTests` fails the build if the simulation references `System.Random`, `DateTime`, `DateTimeOffset`, `Guid`, `Stopwatch` or `Parallel`.
- `DeterminismTests` runs full matches twice per seed and compares a fingerprint of every event, the score, every player's final position/stamina and the ball.

## Events (implemented)

- `MatchEvent` (immutable record): `Timestamp`, `Type`, optional `ActorPlayerId`, `TargetPlayerId`, `Team`, `Position`, and `Metadata` (sorted string map, empty by default).
- `ToString()` gives a canonical one-line form used by determinism tests.
- `MatchEventLog` is append-only; `MatchSimulation.Events` exposes it read-only.
- `MatchEventType` currently: `MatchStarted`, `Kickoff`, `MatchFinished`. New types are appended as their behaviour is implemented.

## Positioning and shape (implemented)

All shape logic runs in **team frame** (`TeamFrame`): own goal at X = 0, attacking +X, left flank +Y. Away converts by a 180° rotation. A symmetry test checks Home and Away produce mirrored targets (ADR-011).

`Formation433` holds three anchor sets per slot, authored on 105 × 68 and scaled to the pitch:

| Layout | Use |
|---|---|
| Kickoff | Starting positions (`KickoffPosition`, public). |
| In possession | CBs split, fullbacks pushed on, CM deeper than LCM/RCM, wingers wide, ST highest. |
| Out of possession | Flat back four, compact midfield, forwards screening. |

`TeamShapeSystem.Apply(state)` sets every player's `MovementTarget` and `MovementIntensity` each tick:

- **Ball carrier** — target = current position (carrying arrives in M1.6).
- **In possession** — anchor + 0.5 × ball X offset, 0.25 × ball Y offset from centre. CBs capped 6 m behind the ball; forwards capped 6 m from the goal line.
- **Out of possession / loose ball** — anchor + 0.55 × X offset, 0.35 × Y offset.
  - Back four share one line X: follows the ball, clamped to ≥ 6 m and ≥ 4 m goal-side of the ball.
  - Midfield ≥ 8 m ahead of the line, Y compressed 20 % toward centre. Forwards ≥ 16 m ahead of the line.
  - The nearest outfield player (ties → earlier in `Players`) gets target = ball, intensity `Sprint`.
- Outfield targets are kept 3 m from goal lines and 2 m from touchlines.

`GoalkeeperPositioning.Target`:

- Out of possession — on the line from goal centre to ball, depth = clamp(0.15 × distance to ball, 1, 11) × (0.6 + 0.04 × Awareness). Low Awareness stays too deep.
- In possession — deep support: X = 4 + 0.12 × ball X, small lateral shift.
- Always clamped inside the penalty area.

## Passing (implemented)

Intent → resolver → result → state update → event (ADR-012):

```text
PassDecisionSystem ─▶ PassTargetSelector ─▶ PassIntent ─▶ PassResolver.Resolve ─▶ PassResult
                                                                     │
                     PassExecution.Launch (PassAttempted, ball in flight) ◀┘
                     … BallFlightSystem, N ticks later …
                     PassExecution.Land (PassCompleted | PassIntercepted + PossessionChanged | PassMisplaced)
```

**Estimate** (`PassResolver.Estimate`, no randomness; shared with target selection):

- `Ability = Passing + 0.3·(Technique − 5) + 0.3·(Awareness − 5)`
- `Quality = Ability − 0.1·distance − Pressure`, where `Pressure = 0.4 · max(0, 5 − distance to nearest opponent)`
- `Accuracy = Sigmoid(0.7 · (Quality + 1))` (ADR-013)
- **Interception threats** — every opponent whose closest point on the pass lane is ≥ 5 % along it and within reach:
  `reach = 1 + 0.1·Awareness + 0.5 · topSpeed · t · travelTime` (they can run toward the lane while the ball travels).
  `P = (1 − laneDistance/reach) · (0.3 + 0.06·Defending) · max(0.2, 1.1 − 0.06·Ability)`, capped at 0.95.
  Threats are ordered by how early along the lane they meet the ball. An opponent marking the receiver is a late-lane threat, which is how receiver openness enters.
- `SuccessProbability = Accuracy · Π(1 − P_threat)`

**Resolve**: one `Roll(Accuracy)`; on failure → **Misplaced**, landing 3 to 3 + 0.3·distance m from the target in a random direction (unit-disc rejection sampling), clamped to the pitch. Otherwise one `Roll(P)` per threat, earliest first; first success → **Intercepted** at that lane point. Otherwise **Completed**.

**Result presentation data**: `StartPosition`, `IntendedTarget`, `TargetPosition` (actual landing), `TravelDuration`, `Quality` (= accuracy probability).

**Target selection** (`PassTargetSelector`): for every teammate ≥ 4 m away, `score = 100·SuccessProbability + 0.8·forward progress (m, team frame) − 15 if goalkeeper`; highest wins, ties to the earlier player. No randomness yet.

**Decision placeholder** (`PassDecisionSystem`): the carrier passes once they have held the ball 2.5 s. Replaced by utility AI in M1.9.

**During flight**: the receiver (or interceptor) sprints to the landing point; the passing team keeps its attacking shape; the defending presser chases the ball.

**Events**: `PassAttempted` (actor = passer, target = intended receiver, metadata `quality`, `travel`), `PassCompleted` (actor = passer, target = receiver), `PassIntercepted` (actor = interceptor, target = intended receiver, then `PossessionChanged`), `PassMisplaced` (actor = passer, position = landing).

## Possession bookkeeping (implemented)

- `MatchContext.GiveBall` is the only way systems transfer possession; it emits `PossessionChanged` (actor = new holder, team = new team) when the team differs from `LastPossessionTeam`. The opening kickoff does not emit it.
- `PossessionStartTick` records when the current holder got the ball.
- `AttackingTeam` = possessor's team, or the passing team during a flight; null when loose.

## Movement (implemented)

`MovementSystem.Apply(state, tickSeconds)` moves each player straight toward `MovementTarget`, never overshooting, clamped to the pitch. No collisions or avoidance.

- Top speed (m/s) = (5.0 + 0.4 × Pace) × fatigue factor. Pace 1 → 5.4, Pace 5 → 7.0, Pace 10 → 9.0.
- Fatigue factor = 1 − 0.3 × (1 − stamina / 100); at zero stamina a player keeps 70 % of top speed.
- `Jog` = 60 % of top speed; `Sprint` = 100 %. Stamina is not yet consumed (M1.8), so the fatigue factor is currently always 1.

A possessed ball is moved to its carrier after movement (`SyncBallToPossessor`); an invariant checks that a possessed ball is always at its carrier's position.

## Pitch (implemented)

`PitchGeometry` — continuous coordinates in metres, default 105 × 68. Origin is a corner.

- X runs along the length. **Home defends X = 0 and attacks +X; Away defends X = 105 and attacks −X.** `PitchGeometry.AttackDirection(side)` returns ±1.
- Landmarks: `Centre`, `HalfwayX`, `OwnGoalCentre(side)`, `OpponentGoalCentre(side)`, `IsInPenaltyArea(point, defendingSide)`, `IsInOwnHalf`.
- Constants: goal width 7.32, penalty area 16.5 deep × 40.32 wide.
- `Contains` / `Clamp` for boundary checks. Zones are not modelled yet; add classification methods here when needed.

`Vec2` is a `double` value type. Only basic arithmetic and `Math.Sqrt` are used (IEEE-exact, deterministic on a given runtime). Avoid introducing trigonometric functions into outcome-affecting code without considering cross-platform determinism.

## Match state (implemented)

`MatchState.Create(home, away, pitch, startingPosition)` builds the authoritative state:

- validates that player ids are unique across both teams and that every starting position is on the pitch;
- `Players` is an ordered list of 22 `PlayerMatchState` — home then away, each in team-definition order. **Iterate this list, never a dictionary, when order can affect outcomes.**
- `Ball` starts loose at the centre spot; `Score` 0–0; `Phase` = `Kickoff`.
- `Clock` (`MatchClock`) and `KickoffTeam` (side taking the next kickoff) are also part of the state.

## Player state (implemented)

- `PlayerDefinition` — immutable: `Id`, `Name`, `ShirtNumber`, `Position` (4-3-3 slot, ADR-008), `Stats`.
- `PlayerStats` — the nine ADR-002 attributes, each validated to 1–10.
- `PlayerMatchState` — mutable: `Position`, `MovementTarget`, `CurrentStamina` (0–100, starts at 100). `CurrentIntent` will be added with the intent system (M1.5).

## Ball and possession (implemented)

`BallState` holds `Position` and `PossessorId`; `IsLoose` is derived. Possession is stored **only** on the ball (ADR-007):
`MatchState.HasBall(id)`, `PossessionPlayerId` and `PossessionTeam` are computed from it, so two players can never both have the ball.
Ball-in-flight and presentation data (start/target/duration/quality) arrive with passing (M1.5).

## Invariants (implemented)

`MatchStateInvariants.FindViolations(state)` / `Verify(state)` check: 22 active players, 11 per side, unique ids, possessor is an active player and the ball is at their position, ball on the pitch, a ball in flight has no possessor, non-negative score, players on the pitch, stamina within bounds. Each message names the offending player. The clock cannot decrease by construction; the test helper `CheckedRun` additionally verifies invariants and clock advancement after every tick of a full match.

## Design targets

| Topic | Target design | Subtask |
|---|---|---|
| Action pattern | Carry, tackle, shot follow the pass pattern. | M1.6–M1.7 |
| Utility AI | Score each valid action; choose high-utility with controlled deterministic randomness. | M1.9 |

## Current limitations

- Only on-ball action is passing, every 2.5 s; no carrying, tackling or shooting; every match is 0–0.
- ~1 500 passes per match (placeholder cadence) with ~92 % completion; interceptions are rare (< 1 %) because target selection avoids risky lanes and nothing forces risk yet. Revisit in M1.6/M1.9/M1.11.
- The ball never goes out of play; misplaced passes are clamped to the pitch.
- No off-ball decision-making (support runs, forward runs) — players only follow shape anchors.
- No statistics in `MatchResult` yet.
