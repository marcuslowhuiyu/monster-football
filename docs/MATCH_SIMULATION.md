# Match Simulation

Detailed documentation of the football simulation. Update whenever the simulation architecture materially changes.

**Status:** domain model (M1.2) and simulation loop, clock, RNG, phases and events (M1.3) implemented. Players do not act yet. "Design targets" below lists agreed designs from `milestones/MILESTONE_1.md` that are not yet built.

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
   - `OpenPlay` — no player behaviour yet (M1.4–M1.9).
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

## Kickoff layout (implemented early, from M1.4)

`Formation433.KickoffPosition(pitch, side, slot)` gives each slot a fixed anchor in its own half, authored for Home on a 105 × 68 pitch (left flank = +Y when facing +X), scaled to the configured pitch and rotated 180° for Away. `MatchSimulation` uses it for starting positions. Dynamic in/out-of-possession shape is still M1.4.

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

`MatchStateInvariants.FindViolations(state)` / `Verify(state)` check: 22 active players, 11 per side, unique ids, possessor is an active player, non-negative score, players on the pitch, stamina within bounds. Each message names the offending player. The clock cannot decrease by construction; the test helper `CheckedRun` additionally verifies invariants and clock advancement after every tick of a full match.

## Design targets

| Topic | Target design | Subtask |
|---|---|---|
| Action pattern | Intent → Resolver → Result → state update → event. | M1.5–M1.7 |
| Utility AI | Score each valid action; choose high-utility with controlled deterministic randomness. | M1.9 |
| Movement & shape | Pace/fatigue-based movement toward targets; attacking and defensive 4-3-3 shape; goalkeeper positioning. | M1.4 |
| Presentation data | Action results carry start/target position, travel duration, quality. | M1.5+ |

## Current limitations

- Players never move or act during open play; a full match is kickoff followed by 90 idle minutes, always 0–0.
- No statistics in `MatchResult` yet.
