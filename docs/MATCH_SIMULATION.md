# Match Simulation

Detailed documentation of the football simulation. Update whenever the simulation architecture materially changes.

**Status:** domain model (M1.2); loop, clock, RNG, phases, events (M1.3); movement and 4-3-3 shape (M1.4); passing (M1.5); carrying and tackling (M1.6); shooting, goalkeeping and goal restarts (M1.7); stamina and fatigue (M1.8); utility AI (M1.9); full-match results and statistics (M1.10); batch simulation and tuning (M1.11) implemented. "Design targets" below lists agreed designs from `milestones/MILESTONE_1.md` that are not yet built.

## Simulation API (implemented)

```csharp
var simulation = new MatchSimulation(MatchConfig.Default, homeTeam, awayTeam, seed: 123456);
simulation.Step();                          // advance one tick
MatchResult result = simulation.RunToCompletion();
```

- `State` (read-only `MatchState`), `Events` (ordered log), `IsFinished`, `Seed`.
- `MatchResult`: `HomeGoals`, `AwayGoals`, `Events`, `FinalState`, `Statistics`, `Seed`.
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
   - `Kickoff` — `RestartSystem.TakeKickoff`: the kicking side's ST is placed on the centre spot; emit `Kickoff`; give them the ball (after a goal this also emits `PossessionChanged`); phase → `OpenPlay`.
   - `OpenPlay` — in this order:
     1. `BallFlightSystem` — move a pass or shot in flight; on its arrival tick, apply its outcome. If a shot went in, the rest of the tick is skipped.
     2. `CarrySystem` — end carries whose time is up, whose target is reached, or whose carrier lost the ball.
     3. `OnBallDecisionSystem` — utility AI: carrier holds, passes, carries or shoots (a shot is struck and launched; it lands on a later tick).
     4. `TackleSystem` — the presser (or another opponent in range) may challenge.
     5. `TeamShapeSystem` — default targets: team shape, the presser, a pass receiver; the carrier follows their carry.
     6. `OffBallDecisionSystem` — utility AI for everyone else: support, forward runs, marking, recovery.
     7. `MovementSystem` — move all players.
     8. Sync a possessed ball to its carrier.
     9. `LooseBallSystem` — nearest player within 1.5 m collects a loose ball.
     10. `StaminaSystem` — drain stamina for metres covered and for this tick's action events.
   - `GoalRestart` — `RestartSystem.ResetForKickoff`: all players back to the kickoff layout, intents and cooldowns cleared, ball loose on the centre spot; phase → `Kickoff`.
4. Advance the clock one tick.
5. If `Tick ≥ TotalTicks` → phase `Finished`, emit `MatchFinished` (timestamp 90:00).

Events emitted during a step carry the timestamp from **before** the clock advances, so the kickoff is at 00:00.

## Phases (implemented)

`Kickoff → OpenPlay → (GoalRestart → Kickoff → OpenPlay)* → Finished`. A goal sets `GoalRestart` and `KickoffTeam` = the conceding side. The clock keeps running through restarts (one tick each).

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
- `MatchEventType` (17 values; `PossessionChanged` carries a `cause`): match lifecycle (`MatchStarted`, `Kickoff`, `MatchFinished`), passing (`PassAttempted`, `PassCompleted`, `PassIntercepted`, `PassMisplaced`), `PossessionChanged`, `CarryStarted`, tackling (`TackleAttempted`, `TackleWon`, `TackleLost`, `TackleLooseBall`), shooting (`ShotTaken`, `ShotSaved`, `ShotMissed`, `GoalScored`). New types are appended.

## Positioning and shape (implemented)

All shape logic runs in **team frame** (`TeamFrame`): own goal at X = 0, attacking +X, left flank +Y. Away converts by a 180° rotation. A symmetry test checks Home and Away produce mirrored targets (ADR-011).

`Formation433` holds three anchor sets per slot, authored on 105 × 68 and scaled to the pitch:

| Layout | Use |
|---|---|
| Kickoff | Starting positions (`KickoffPosition`, public). |
| In possession | CBs split, fullbacks pushed on, CM deeper than LCM/RCM, wingers wide, ST highest. |
| Out of possession | Flat back four, compact midfield, forwards screening. |

`TeamShapeSystem.Apply(state, attackingSmoothing, defendingSmoothing)` sets every player's `MovementTarget` and `MovementIntensity` each tick. Each team's shape is built around its **shape focus** (`MatchState.ShapeFocus(side)`), a moving average of the ball: time constant 3 s for the attacking team, 0.75 s for the defending team. Attackers drift with the flow of play instead of shifting for every square pass; defenders track the ball closely. Pressers, markers and pass receivers use the real ball. Tests call `Apply(state)`, which snaps the focus to the ball.

- **Ball carrier** — follows their active carry (intensity `Carry`); otherwise target = current position.
- **In possession** (intensity `Easy`) — anchor + 0.5 × ball X offset, 0.25 × ball Y offset from centre. CBs capped 6 m behind the ball; forwards capped 6 m from the goal line; no attacker pushes beyond max(opponents' last outfield line, ball X).
- **Out of possession / loose ball** — anchor + 0.55 × X offset, 0.35 × Y offset.
  - Back four share one line X: follows the ball, clamped to ≥ 6 m and ≥ 4 m goal-side of the ball.
  - Midfield ≥ 8 m ahead of the line, Y compressed 20 % toward centre. Forwards ≥ 16 m ahead of the line.
  - The presser — the outfield player with the lowest distance × (1 + tiredness), ties → earlier in `Players` — sprints to a jockeying spot 1.5 m goal-side of a carrier, or to the ball itself when it is loose or in flight (see *CloseDownOpponent*).
- Outfield targets are kept 3 m from goal lines and 2 m from touchlines.

`GoalkeeperPositioning.Target`:

- Out of possession — on the line from goal centre to ball, depth = clamp(0.15 × distance to ball, 1, 11) × (0.6 + 0.04 × Awareness). Low Awareness stays too deep.
- In possession — deep support: X = 4 + 0.12 × ball X, small lateral shift.
- Always clamped inside the penalty area.

## Passing (implemented)

Intent → resolver → result → state update → event (ADR-012):

```text
OnBallDecisionSystem ─▶ (utility AI picks a Pass candidate) ─▶ PassIntent ─▶ PassResolver.Resolve ─▶ PassResult
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

**Target selection**: every teammate ≥ 4 m away is a Pass candidate in the utility AI (see *Utility AI*), scored from this estimate's `SuccessProbability`, forward progress and the receiver's goal proximity.


**During flight**: the receiver (or interceptor) sprints to the landing point; the passing team keeps its attacking shape; the defending presser chases the ball.

**Events**: `PassAttempted` (actor = passer, target = intended receiver, metadata `quality` (accuracy), `success`, `distance`, `threats`, `stamina`, `travel`), `PassCompleted` (actor = passer, target = receiver), `PassIntercepted` (actor = interceptor, target = intended receiver, metadata `chance`, then `PossessionChanged`), `PassMisplaced` (actor = passer, target = intended receiver, position = landing).

## Carrying (implemented)

`CarryIntent(Carrier, Destination)` → `CarryResolver.Resolve` → `CarryResult(Carrier, StartPosition, TargetPosition, Speed, Duration, StartTick, EndTick, Quality)` (ADR-014). No randomness; the carrier keeps the ball.

- `ChooseDestination`: up to 10 m forward in team frame (bending toward goal in the final third), steered sideways away from the nearest opponent within 12 m (steer strength ∝ Awareness and closeness), with a small drift toward the centre; kept 3 m inside touchlines and goal lines.
- Speed = top speed × (0.55 + 0.03 × Technique) — `MovementIntensity.Carry`.
- `CarrySystem.Start` sets `CurrentIntent` / `ActiveCarry` and emits `CarryStarted` (metadata `distance`, `speed`, `stamina`). The carry ends at `EndTick`, on reaching the target, or when the ball leaves the carrier.

## Tackling (implemented)

`TackleIntent(Tackler, Carrier)` → `TackleResolver.Resolve` → `TackleResult` (ADR-014):

- Defender rating = Defending + 0.3·(Power − 5) + 0.3·(Awareness − 5). Carrier rating = Technique + 0.3·(Power − 5) + 0.3·(Awareness − 5).
- Roll contact `Sigmoid(0.3·(def − car) + 0.15)` (≈ 0.57 between equals) → fail = **Lost**.
- Roll clean `clamp(0.6 + 0.03·(tackler Awareness − 5), 0.1, 0.9)` → **Won**, otherwise **LooseBall** (ball 2–5 m away, random direction).
- Pace matters indirectly: faster defenders reach challenge range sooner.

`TackleSystem`: the nearest outfield opponent within 1.8 m of a carrier who has held the ball ≥ 0.75 s commits with probability rate × tick length, rate = `TackleBaseRatePerSecond` (0.17) × (contact chance ÷ an even duel's) × (1 + `TackleDangerBonus`, default 1) when the carrier is within Length/3 of the defender's goal centre. Cooldowns: 1.5 s after any attempt, 3 s after being beaten (Lost), 2 s for a dispossessed carrier. Goalkeepers do not tackle.

Events: `TackleAttempted` (actor = tackler, target = carrier; metadata `contact`, `clean`, `stamina`, `carrierStamina`) then one of `TackleWon` (+ `PossessionChanged`), `TackleLost`, `TackleLooseBall` (position = where the ball went).

## Shooting and goalkeeping (implemented)

`ShotIntent(Shooter)` → `ShotResolver` → `ShotResult` → `ShotExecution.Strike` → ball in flight → `ShotExecution.Land` (ADR-015, ADR-022).

**Estimate** (no randomness; shared with the shooting decision):

- Ability = Finishing + 0.3·(Technique − 5) + 0.2·(Power − 5).
- Distance to goal centre; angle penalty = min(4, 1.5 × lateral offset ÷ distance to goal line); pressure = 0.5 × max(0, 3 − nearest outfield opponent distance) + 0.5 per outfield defender within 1.5 m of the shooting lane (blocking).
- On target = `Sigmoid(0.35·(Ability − 0.15·distance − pressure − angle) − 0.7)`.
- Keeper rating = Keeping + 0.3·(Awareness − 5) − 0.3·min(10, distance from the keeper's ideal spot). The ideal spot is `GoalkeeperPositioning`, so Awareness counts twice: positioning and rating.
- Shot power = 0.5·Finishing + 0.5·Power + 0.2·max(0, 20 − distance) (close shots are harder to save).
- Save = `Sigmoid(0.35·(keeper rating − shot power) + 0.74)`.
- `GoalProbability` (xG) = on target × (1 − save). All-5s, central, unpressed, keeper on their spot: ≈ 0.49 at 6 m, 0.20 at 15 m, 0.10 at 20 m, 0.08 at 25 m.

**Resolve**: `Roll(onTarget)` → fail = **Missed**: `Roll(0.5)` picks a side, target 0.5–4 m wide of a post on the goal line. Otherwise the target is a random point inside the goal mouth, then `Roll(save)` → **Saved** or **Goal**. Travel duration = distance ÷ (18 + 1.2·Power) m/s.

**Strike**: emit `ShotTaken` (metadata `xg`, `onTarget`, `save`, `distance`, `stamina`, `keeperStamina`, `travel`) and launch a `BallFlight` carrying the decided `ShotResult`; it arrives after ⌈travel ÷ tick⌉ ticks (at least one). While it travels nobody has the ball and `AttackingTeam` is the shooting side.

**Land** (arrival tick):

- **Goal** — ball loose at the target, score +1, `KickoffTeam` = conceding side, phase `GoalRestart`, emit `GoalScored` (metadata `score`).
- **Saved** — emit `ShotSaved` (actor = keeper, target = shooter), keeper gets the ball (`PossessionChanged`).
- **Missed** — emit `ShotMissed`, defending keeper gets the ball (stand-in for a goal kick).

**Goalkeepers in possession**: cannot be tackled inside their own penalty area; never shoot or carry; distribute by passing.

## Utility AI (implemented)

ADR-018. Every decision: generate `ActionCandidate`s → `UtilitySelector.Choose` → execute.

**Selector**: apply `IUtilityModifier`s (none in M1) → plausible = utility > best − 8 → one plausible: take it (no random draw); several: pick with probability ∝ (utility − (best − 8)) using one `IMatchRandom` draw.

**Weights** live in `MatchConfig.Ai` (`AiTuning`: `OnBallWeights`, `OffBallWeights`). Defaults came from a random search over 84 configurations (M1.9) refined with 1 000-match batches (M1.11).

### On the ball (`OnBallCandidateGenerator`, every tick after the 0.5 s first touch)

| Candidate | Utility |
|---|---|
| Hold | 88 − 12 × seconds held − 30 × pressure. While carrying: 62 − 10 × seconds carrying − 30 × pressure. Pressure = 1 − (nearest opponent ÷ 3 m), 0 beyond 3 m. |
| Pass (each teammate ≥ 4 m) | 50 × success probability + 0.75 × forward progress (m) + 18 × goal proximity of receiver (1 on the goal line, 0 at ≥ 35 m) − 15 if goalkeeper |
| Carry (outfield, not mid-carry, > 4 m from the goal line) | 42 + 0.8 × forward gain + 2 × space ahead (≤ 12 m) − 30 × pressure × (1 − 0.05 × (Technique − 5)) |
| Shoot (outfield, ≤ 35 m, xG ≥ 0.03) | 22 + 318 × xG |

Goalkeepers only hold or pass.

### Off the ball (`OffBallDecisionSystem`, after `TeamShapeSystem`)

Skipped: goalkeepers, the carrier, the presser and a pass receiver (they keep the shape system's assignment). Everyone else follows their `OffBallPlan` until it expires or the attacking team changes, then re-decides:

| Candidate | When | Target / intensity | Utility |
|---|---|---|---|
| MoveIntoPosition | always | team-shape target and intensity (`Easy` in possession, `Jog` defending) | 40 |
| RecoverPosition | defending, > 10 m from shape target | shape target, sprint | 40 + 1.5 × (distance − 10) (+10 if ahead of the ball) |
| SupportBallCarrier | attacking, not a defender, ≤ 30 m from carrier | best of 8 spots 12 m around the carrier, jog | 28 + 2.5 × openness (≤ 10) + 2 × lane clearance (≤ 5) − 0.4 × travel (+4 if forward) |
| MakeForwardRun | attacking, ST/LW/RW/LCM/RCM, ball beyond 35 m | 1 m beyond the opponents' last outfield line but never closer than 16 m to the goal line, in the player's lane, sprint | 31 + 0.8 × Pace + min(20, 0.6 × space behind the line) + role bonus (ST 8, wingers 5) |
| DefendGoalSide | defending, defender/midfielder | 2 m goal-side of the nearest unmarked attacker (not the carrier, within 15 m of the shape target, X < ball X + 15 m in team frame); sprint if > 8 m away | 36 + 18 × threat − 0.5 × travel; threat = max(0, 1 − attacker's distance to own goal ÷ 50) |

Commitment: forward runs 3 s, support and marking 1.5 s, others 1 s. A plan is dropped early if the attacking team changes or the marked opponent gains the ball. Team limits: 2 supporters, 2 runners, one marker per opponent.

**CloseDownOpponent** is the presser chosen by `TeamShapeSystem` (fatigue-weighted nearest outfield player). Against a carrier the presser jockeys 1.5 m goal-side (inside the 1.8 m challenge range); a loose or flying ball is chased directly.

## Statistics (implemented)

ADR-019. `MatchStatistics.FromEvents` derives everything from the event log:

| Statistic | Source |
|---|---|
| Goals | `GoalScored` (team = scorer) |
| Shots | `ShotTaken` |
| Shots on target | `GoalScored` + `ShotSaved` (event team is the saving side; credited to the shooting side) |
| Passes attempted / completed | `PassAttempted` / `PassCompleted` |
| Possession changes | every `PossessionChanged` |
| Possessions won | `PossessionChanged` whose `cause` is open play (not kickoffs or keeper restarts) |
| Possession seconds | opening `Kickoff` → each `PossessionChanged` → `MatchFinished`; loose and in-flight time counts for the last team in control |

Full-match tests check the statistics against the score, the event log and a 5 400 s possession total.

## Batch simulation (implemented)

ADR-020. `DebugRunner batch [count] [--from SEED]` plays `count` matches (default 1 000, seeds from 1) in parallel, aggregates them in seed order and runs `BatchHealth` pathology bands; it exits 1 if any band fails. CI runs it on every PR.

Current defaults, 1 000 matches between all-5 teams (after M2.1b shot flight):

| Figure | Value |
|---|---|
| Goals per match | 2.87 (max 11) |
| Shots / on target per match | 27.4 / 8.8 (32 %) |
| Passes per match, completion | 1 660, 82 % |
| Home win / draw / away win | 41.0 % / 23.1 % / 35.9 % |
| Home possession | 50.0 % |
| Tackles / carries / possession changes | 293 / 209 / 395 |
| Striker share of goals | 83 % |
| Outfield distance, stamina at full time | 15.3 km, 48.6 |

Matches are 90 continuous minutes (no stoppages); real football has ~55–60 minutes of ball-in-play, so per-match volumes (passes, tackles) read high against real totals.

## Debug runner (implemented)

```sh
dotnet run --project src/MonsterFootball.DebugRunner -- match 7          # highlights + statistics
dotnet run --project src/MonsterFootball.DebugRunner -- match 7 --all    # every event
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000
dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3   # SHA-256 per seed (CI compares OSes)
```

`Diagnostics.MatchReport` prints one line per event (`04:31 Blue #6 passes to Blue #9`, `09:14 Saved by Red GK`, `27:51 GOAL — Blue #10 (1–0)`), `FULL TIME`, the score and a statistics table. Highlights mode keeps kickoffs, shots, saves, misses and goals.

Each action is followed by an indented **odds line** (omit with `--brief`) showing the ratings used, the player's stamina at that moment and the probabilities rolled against, read from the event's metadata:

```text
00:18 Blue #6 passes to Blue #10
      Passing 5 · Technique 5 · Awareness 5 · stamina 100 · 32 m · accuracy 85% · success 34% · 2 in lane
00:20 Blue #10 shoots
      Finishing 5 · Technique 5 · Power 5 · stamina 100 · 17 m · on target 39% · save 68% · xG 0.12
```

## Stamina and fatigue (implemented)

ADR-016. `CurrentStamina` runs 0–100 and starts at 100.

**Drain** (`StaminaSystem`, last step of each open-play tick), always × `DrainMultiplier = 1.3 − 0.06 × Stamina attribute` (1.24 at 1, 1.0 at 5, 0.7 at 10):

| Source | Cost (stamina points) |
|---|---|
| Jogging | 0.002 per metre |
| Carrying | 0.003 per metre |
| Sprinting | 0.004 per metre |
| `PassAttempted` (passer) | 0.05 |
| `CarryStarted` | 0.1 |
| `TackleAttempted` | 0.2 tackler, 0.1 carrier |
| `ShotTaken` | 0.3 |

Action costs are read from events emitted since the system last ran, so each event is charged exactly once. Standing still costs nothing. No recovery.

**Penalties** (`FatigueModel`, linear in tiredness = 1 − stamina/100):

- Speed × (1 − 0.3 × tiredness).
- Action effectiveness × (1 − 0.15 × tiredness), applied to: pass ability and interceptor chance; tackle defender and carrier ratings; shot ability; goalkeeper rating.
- Pressing: presser chosen by distance × (1 + tiredness).

Typical full-time stamina (all-5 teams): GK ≈ 86, defenders ≈ 63–67, midfielders ≈ 36–54, wingers ≈ 27, ST ≈ 8. A Stamina-9 side finishes ~8–17 points fresher per position.

## Possession bookkeeping (implemented)

- `MatchContext.GiveBall(player, cause)` is the only way systems transfer possession; it emits `PossessionChanged` (actor = new holder, team = new team, metadata `cause`) when the team differs from `LastPossessionTeam`. The opening kickoff does not emit it.
- `PossessionCause` (ADR-021): `Reception` (completed pass), `Interception`, `Tackle`, `LooseBall`, `Save` — won in open play — and `Kickoff`, `KeeperRestart` — restarts, not won. `MatchEvent.GetPossessionCause()` reads it back.
- `PossessionStartTick` records when the current holder got the ball.
- When the ball leaves a player by any route, `MatchState` clears that player's `CurrentIntent` and `ActiveCarry`.
- `AttackingTeam` = possessor's team, or the passing team during a flight; null when loose.

## Movement (implemented)

`MovementSystem.Apply(state, tickSeconds)` moves each player straight toward `MovementTarget`, never overshooting, clamped to the pitch. No collisions or avoidance.

- Top speed (m/s) = (5.0 + 0.4 × Pace) × `FatigueModel.SpeedFactor`. Pace 1 → 5.4, Pace 5 → 7.0, Pace 10 → 9.0 when fresh.
- `Jog` = 60 % of top speed; `Sprint` = 100 %; `Carry` = 0.55 + 0.03 × Technique.
- **Arrival**: jogging slows in proportion to the remaining distance, down to 25 % of jog speed — within 12 m of the target for `Easy` (team in possession holding position) and within 4 m for `Jog`. Sprints and carries never slow.
- Each player's `LastTickDistance` is recorded for stamina drain.

A possessed ball is moved to its carrier after movement (`SyncBallToPossessor`); an invariant checks that a possessed ball is always at its carrier's position.

## Pitch (implemented)

`PitchGeometry` — continuous coordinates in metres, default 105 × 68. Origin is a corner.

- X runs along the length. **Home defends X = 0 and attacks +X; Away defends X = 105 and attacks −X.** `PitchGeometry.AttackDirection(side)` returns ±1.
- Landmarks: `Centre`, `HalfwayX`, `OwnGoalCentre(side)`, `OpponentGoalCentre(side)`, `IsInPenaltyArea(point, defendingSide)`, `IsInOwnHalf`.
- Constants: goal width 7.32, penalty area 16.5 deep × 40.32 wide.
- `Contains` / `Clamp` for boundary checks. Zones are not modelled yet; add classification methods here when needed.

`Vec2` is a `double` value type. Only basic arithmetic and `Math.Sqrt` are used (IEEE-exact, deterministic on a given runtime). Avoid introducing trigonometric functions into outcome-affecting code without considering cross-platform determinism.

## Match state (implemented)

`MatchState.Create(MatchConfig config, TeamDefinition home, TeamDefinition away, Func<TeamSide, PlayerDefinition, Vec2> startingPosition)` builds the authoritative state:

- validates that player ids are unique across both teams and that every starting position is on the pitch;
- `Players` is an ordered list of 22 `PlayerMatchState` — home then away, each in team-definition order. **Iterate this list, never a dictionary, when order can affect outcomes.**
- `Ball` starts loose at the centre spot; `Score` 0–0; `Phase` = `Kickoff`.
- `Clock` (`MatchClock`) and `KickoffTeam` (side taking the next kickoff) are also part of the state.

## Player state (implemented)

- `PlayerDefinition` — immutable: `Id`, `Name`, `ShirtNumber`, `Position` (4-3-3 slot, ADR-008), `Stats`.
- `PlayerStats` — the nine ADR-002 attributes, each validated to 1–10.
- `PlayerMatchState` — mutable: `Position`, `MovementTarget`, `MovementIntensity`, `CurrentStamina` (0–100, starts at 100), `LastTickDistance`, `DistanceCovered`, `CurrentIntent`, `ActiveCarry`, `OffBallPlan`, `TackleCooldownUntilTick`.

## Ball and possession (implemented)

`BallState` holds `Position` and `PossessorId`; `IsLoose` is derived. Possession is stored **only** on the ball (ADR-007):
`MatchState.HasBall(id)`, `PossessionPlayerId` and `PossessionTeam` are computed from it, so two players can never both have the ball.
A pass or shot in the air is `BallState.Flight` (`BallFlight`: the resolved `PassResult` **or** `ShotResult` plus launch/arrival ticks); `IsInFlight` / `IsLoose` are derived. A shot still in the air when the match reaches 90:00 is abandoned (interim until M2.1d, where a period ends only at a stoppage).

## Invariants (implemented)

`MatchStateInvariants.FindViolations(state)` / `Verify(state)` check: an active carry exists only on the ball carrier; 22 active players, 11 per side, unique ids, possessor is an active player and the ball is at their position, ball on the pitch, a ball in flight has no possessor, non-negative score, players on the pitch, stamina within bounds. Each message names the offending player. The clock cannot decrease by construction; the test helper `CheckedRun` additionally verifies invariants and clock advancement after every tick of a full match.

## Current limitations

- **Striker dominance**: strikers score ~82 % of goals; wingers and midfielders rarely finish moves.
- **Volume**: ~1 670 passes, ~295 tackles and ~400 possession changes per match (90 continuous minutes) — busy compared with real football.
- **Distance**: 15.3 km per outfield player (real ≈ 10–12 km); just inside the 16 km band.
- **Sensitivity**: shot volume is steep in `ShotBase` (22 → ~28 shots; 16 → ~14). Tune with batches only.
- The ball never goes out of play (no throw-ins, corners or goal kicks); misses go to the keeper.
- No blocks as events, rebounds or posts.
