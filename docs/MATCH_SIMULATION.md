# Match Simulation

Detailed documentation of the football simulation. Update whenever the simulation architecture materially changes.

**Status:** domain model (M1.2); loop, clock, RNG, phases, events (M1.3); movement and 4-3-3 shape (M1.4); passing (M1.5); carrying and tackling (M1.6); shooting, goalkeeping and goal restarts (M1.7); stamina and fatigue (M1.8); utility AI (M1.9); full-match results and statistics (M1.10); batch simulation and tuning (M1.11); match flow (M2.1), tactical zones (M2.3), ball out of play and restarts (M2.4), offside (M2.5), fouls, free kicks and penalties (M2.6), cards and reduced teams (M2.7), width (M2.7b) implemented. "Design targets" below lists agreed designs from `milestones/MILESTONE_1.md` that are not yet built.

## Simulation API (implemented)

```csharp
var simulation = new MatchSimulation(MatchConfig.Default, homeTeam, awayTeam, seed: 123456);
simulation.Step();                          // advance one tick
MatchResult result = simulation.RunToCompletion();
```

- `State` (read-only `MatchState`), `Events` (ordered log), `IsFinished`, `Seed`.
- `MatchResult`: `HomeGoals`, `AwayGoals`, `Events`, `FinalState`, `Statistics`, `Seed`, `Abandoned`.
- `Step()` after full time throws; `RunToCompletion()` is idempotent (ADR-010).
- An `internal` constructor accepts any `IMatchRandom`, so tests can script outcomes (`ScriptedRandom`).

## Fixed timestep and clock (implemented)

- `MatchConfig`: `Pitch` (default 105 × 68), `TickSeconds` (default 0.25 → 4 ticks per second), `PeriodSeconds` (default 2 700) and `Periods` (default 2), all positive; `AddedTime` (`AddedTimeTuning`).
- `PeriodTicks` = period length ÷ tick length, rounded **up** so a period never ends early (default 10 800). A match's length in ticks is not fixed: it depends on added time and on when play stops (ADR-024).
- `MatchClock.Tick` (integer) is authoritative and only ever advances by one. The clock also holds `Period`, `PeriodStartTick`, `RegularEndTick` and, once announced, `AddedTimeTicks`.
- `MatchTime`: `Seconds` = time elapsed since kickoff (never decreases; durations use it) plus `Period`, `PeriodSeconds` and `RegularPeriodSeconds` for the referee's clock. Prints `MM:SS` (`04:31`), or in added time regular end + overrun (`45+01:30`, `90+03:00`). The second half's clock starts at `45:00` however much time was added to the first.

## Periods and added time (implemented)

ADR-024, `PeriodSystem`, checked every tick after the clock advances:

- **Allowances**: in a period's regular time, each goal adds `AddedTime.PerGoalSeconds` (45 s), each penalty `PerPenaltySeconds` (45 s), each card `PerCardSeconds` (20 s) and each dismissal `PerDismissalSeconds` (30 s; a second yellow counts as a card and a dismissal).
- **Announcement**: at the end of regular time, `AddedTimeAnnounced` (metadata `minutes`, `period`) with the allowance rounded **up** to whole minutes.
- **End of a period**: once regular + added time is up, the period ends at the first tick on which play is stopped. A ball in flight is never cut off: a struck shot always lands. If play has not stopped `AddedTime.SafetyCapSeconds` after that (180 s, ADR-033; see *Current limitations*), the period ends at the next tick with no ball in flight. `PeriodEnded` (metadata `period`, `end` = `Stoppage` or `SafetyCap`).
- **Half-time** takes no ticks: the second period starts at once with a `PeriodStart` stoppage and a kickoff for the side that did not take the opening one; teams line up (placed) and kick off on the next tick. No stamina recovery.
- **Full time**: the last period's `PeriodEnded` is followed by `MatchFinished`.

## Tick order (implemented)

Each `Step()`:

1. If finished → throw.
2. First step only: coin toss (`Roll(0.5)`) picks the side taking the opening kickoff (recorded on the `MatchStart` stoppage), emit `MatchStarted`.
3. Run the current phase:
   - `Stopped` / `RestartSetup` — see *Stoppages*: restart targets → advance the stoppage (the tick ends if the restart is taken) → movement → stamina.
   - `OpenPlay` — in this order:
     1. `BallFlightSystem` — move a pass or shot in flight; on its arrival tick, apply its outcome. A misplaced pass whose path leaves the pitch goes out of play where it crosses. If play stopped (goal, ball out), the rest of the tick is skipped.
     2. `CarrySystem` — end carries whose time is up, whose target is reached, or whose carrier lost the ball.
     3. `OnBallDecisionSystem` — utility AI: carrier holds, passes, carries or shoots (a shot is struck and launched; it lands on a later tick).
     4. `TackleSystem` — the presser (or another opponent in range) may challenge. A ball knocked out of play skips the rest of the tick.
     5. `TeamShapeSystem` — default targets: team shape, the presser, a pass receiver; the carrier follows their carry.
     6. `OffBallDecisionSystem` — utility AI for everyone else: support, forward runs, marking, recovery.
     7. `MovementSystem` — move all players.
     8. Sync a possessed ball to its carrier.
     9. `LooseBallSystem` — a rolling ball moves on (and may go out of play); then the nearest player within 1.5 m collects a loose ball.
     10. `StaminaSystem` — drain stamina for metres covered and for this tick's action events.
4. Advance the clock one tick.
5. `PeriodSystem`: announce added time at the end of regular time; end the period at a stoppage once time is up (or at the safety cap); start the second half or finish the match.

Events emitted during a step carry the timestamp from **before** the clock advances, so the kickoff is at 00:00.

## Phases (implemented)

ADR-023. The ball is in play (`OpenPlay`) or play is stopped:

```text
Stopped (dead ball) → RestartSetup (waiting for positions) → restart taken → OpenPlay → … → Finished
```

A new match is `Stopped` with a `MatchStart` stoppage; a goal stops play with a `Goal` stoppage and the ball crossing a line with a `BallOutOfPlay` stoppage. The clock keeps running through stoppages. An abandoned match goes straight to `Finished` (ADR-032).

## Stoppages (implemented)

- `MatchState.CurrentStoppage` (`Stoppage`): `Reason` (`MatchStart`, `Goal`, `PeriodStart`, `BallOutOfPlay`, `Offside`, `Foul`), `Restart` (`Kickoff`, `ThrowIn`, `GoalKick`, `Corner`, `IndirectFreeKick`, `DirectFreeKick`, `Penalty`), `AwardedTeam`, `Location`, `StoppedAtTick`, `Offender` (offside, foul), `Fouled`, `Discipline` (the card for a foul: `None`, `Yellow`, `SecondYellow`, `Red`). Present exactly while play is stopped.
- `MatchContext.StopPlay` records it, makes the ball dead (nobody holds it), clears every player's intent, carry, off-ball plan and tackle cooldown, and emits `PlayStopped` (team = awarded team, position = restart location, metadata `reason`, `restart`).
- The taker is chosen once, when play stops (`RestartTakers`; ties → earlier player): kickoff — the ST; throw-in — the nearest full-back or winger; goal kick — the goalkeeper; corner — the highest Passing (then Technique) outfielder other than the centre-backs and striker; indirect free kick — the nearest defender; direct free kick — the best finisher within 32 m of goal, otherwise the nearest defender or midfielder; penalty — the best finisher (then Technique, Power). Each falls back to any outfield player.
- Each stopped tick, `RestartSystem.Position` gives every player a restart target at `Jog` (`RestartLayout`):
  - kickoff — the 4-3-3 kickoff layout (everyone in their own half, opponents outside the centre circle), the taker on the ball;
  - throw-in, goal kick and free kicks — both shapes built around the restart spot (awarded side attacking, the other defending, nobody pressing);
  - corner — the same, then set slots go into the box (attack: ST, both CBs, a winger at the far post, CM on the edge; defence: CBs, a full-back on each post, three midfielders, wingers, ST up at 25 m);
  - direct free kick within 32 m — a wall of four (LCM, CM, RCM, then wingers/ST) 0.7 m apart across the line to goal, just beyond 9.15 m; the shot model counts them as lane blockers;
  - penalty — the defending keeper on the goal line; everyone else outside the penalty area and the arc (9.15 m from the spot);
  - then the taker's target is the ball, and opponents' targets are moved 0.5 m beyond the law's limit: 2 m from a throw-in, 9.15 m from a corner or free kick, outside the penalty area for a goal kick.
- `RestartSystem.Advance`: once the minimum dead-ball time has passed (`MatchConfig.Restarts`: goal 45 s, throw-in 8 s, goal kick 15 s, corner 20 s, offside free kick 12 s, direct free kick 20 s, penalty 45 s; 0 at the start of a period) the phase becomes `RestartSetup`. A kickoff is taken when everyone is within 1 m of their target; any other restart when the taker is within 1 m of the ball and every opponent respects the law. After `SetupTimeoutSeconds` (30 s) the taker and any opponent breaking the law (everyone, for a kickoff) are placed directly and the restart is taken. A kickoff that starts a period is taken at once with the teams placed (they line up before the whistle).
- Kickoff: the taker is placed on the spot, shape focus resets to it, play resumes, `Kickoff` is emitted and the taker gets the ball (`PossessionChanged`, cause `Kickoff`, after a goal).
- A penalty is struck at once (`ShotResolver.ResolvePenalty`). A direct free kick within 32 m is shot if its xG (wall included) is ≥ 0.04, otherwise passed.
- Throw-in, goal kick, corner, free kick, penalty: the same, emitting `RestartTaken` (actor = taker, team, position, metadata `restart`) and giving the taker the ball (cause `ThrowIn` / `GoalKick` / `Corner` / `FreeKick` / `Penalty`, not open play). The taker then passes at once (`PassResolver`, as any pass) to the teammate with the best on-ball pass utility — no randomness in the choice: within 25 m for a throw-in, among attackers in the opposition penalty area for a corner, judged onside (as in open play) for a free kick. A free kick is judged for offside like any pass; throw-ins, goal kicks and corners are not. With nobody eligible the taker keeps the ball and open play takes over.
- No decisions, tackles or random draws happen while play is stopped.

## Offside (implemented)

ADR-029, `Laws.Offside`, on unless `MatchConfig.OffsideEnabled` is false.

- **Line**: in the attacking team's frame, the furthest of halfway, the ball and the second-last opponent (the keeper counts). A player beyond it (strictly; level is onside) is in an offside position.
- **Judged at the pass**: `PassExecution.Launch` records an `OffsideJudgement` (`MatchState.Offside`): the passer's teammates in an offside position as the ball is played. Not for throw-ins, goal kicks or corners.
- **Involvement**: if one of them is the next player to get the ball — receiving the pass or collecting it loose — `MatchContext.GiveBall` stops play instead: `Offside` stoppage, indirect free kick to the defenders where the offender stands, `PlayStopped` with actor = offender. Any other touch (an opponent, an onside teammate, a parry, a deflection) clears the judgement.
- Being offside and becoming involved are separate steps, so interfering with an opponent or passive offside can be added later without restructuring.

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
- `MatchEventType` (25 values; `PossessionChanged` carries a `cause`): match lifecycle (`MatchStarted`, `Kickoff`, `MatchFinished`, `MatchAbandoned`), match flow (`PlayStopped`, `RestartTaken`, `AddedTimeAnnounced`, `PeriodEnded`), laws (`FoulCommitted`, `YellowCard`, `RedCard`), passing (`PassAttempted`, `PassCompleted`, `PassIntercepted`, `PassMisplaced`), `PossessionChanged`, `CarryStarted`, tackling (`TackleAttempted`, `TackleWon`, `TackleLost`, `TackleLooseBall`), shooting (`ShotTaken`, `ShotSaved`, `ShotMissed`, `GoalScored`). New types are appended.

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
- **In possession** (intensity `Easy`) — anchor + 0.5 × ball X offset, 0.25 × ball Y offset from centre (0.1 for wingers and full-backs, who keep their wide channel — ADR-034). CBs capped 6 m behind the ball; forwards capped 6 m from the goal line; no attacker pushes beyond max(offside line — the second-last opponent —, ball X).
- **Out of possession / loose ball** — anchor + 0.55 × X offset, 0.35 × Y offset.
  - Back four share one line X: follows the ball, clamped to ≥ 6 m and ≥ 4 m goal-side of the ball.
  - Midfield ≥ 8 m ahead of the line, Y compressed 20 % toward centre. Forwards ≥ 16 m ahead of the line.
  - The presser — the outfield player with the lowest distance × (1 + tiredness), ties → earlier in `Players` — sprints to a jockeying spot 1.5 m goal-side of a carrier, or to the ball itself when it is loose or in flight (see *CloseDownOpponent*).
- Outfield targets are kept 3 m from goal lines and 2 m from touchlines.

**Reduced teams** (ADR-031, D11): every shape rule reads a player's `Role`, not their listed position. After a dismissal `ShapeTemplates.Reform` fills the template for the remaining outfield count — 10 → all ten 4-3-3 roles, 9 → 4-4-1 (no CM; the wingers' defending anchors sit on the midfield line), 8 → 4-3-1 (no wingers), 7 → 4-2-1, 6 → 4-1-1 — using the 4-3-3 anchors of the roles kept. Players whose role survives keep it; each vacant role (in template order) goes to the displaced player whose kickoff anchor is nearest (ties → earlier player). With no keeper left, the outfielder with the best Keeping takes the GK role first.

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

**Resolve**: one `Roll(Accuracy)`; on failure → **Misplaced**, landing 3 to 3 + 0.3·distance m from the target in a random direction (unit-disc rejection sampling) — possibly beyond a line, in which case the flight goes out of play where it crosses (ADR-028). A misplaced pass that lands rolls on along its direction for 0.35 × the distance it flew (`LooseBallMotion`, 3 m/s² deceleration) and can run out. Otherwise one `Roll(P)` per threat, earliest first; first success → **Intercepted** at that lane point. Otherwise **Completed**.

**Result presentation data**: `StartPosition`, `IntendedTarget`, `TargetPosition` (actual landing), `TravelDuration`, `Quality` (= accuracy probability).

**Target selection**: every teammate ≥ 4 m away is a Pass candidate in the utility AI (see *Utility AI*), scored from this estimate's `SuccessProbability`, forward progress and the receiver's goal proximity.


**During flight**: the receiver (or interceptor) sprints to the landing point; the passing team keeps its attacking shape; the defending presser chases the ball.

**Events**: `PassAttempted` (actor = passer, target = intended receiver, metadata `quality` (accuracy), `success`, `distance`, `threats`, `stamina`, `travel`), `PassCompleted` (actor = passer, target = receiver), `PassIntercepted` (actor = interceptor, target = intended receiver, metadata `chance`, then `PossessionChanged`), `PassMisplaced` (actor = passer, target = intended receiver, position = landing).

## Carrying (implemented)

`CarryIntent(Carrier, Destination)` → `CarryResolver.Resolve` → `CarryResult(Carrier, StartPosition, TargetPosition, Speed, Duration, StartTick, EndTick, Quality)` (ADR-014). No randomness; the carrier keeps the ball.

- `ChooseDestination`: up to 10 m forward in team frame (bending toward goal in the final third), steered sideways away from the nearest opponent within 12 m (steer strength ∝ Awareness and closeness), with a small drift toward the centre — except a winger or full-back outside the final third, who carries straight down the line (ADR-034); kept 3 m inside touchlines and goal lines.
- Speed = top speed × (0.55 + 0.03 × Technique) — `MovementIntensity.Carry`.
- `CarrySystem.Start` sets `CurrentIntent` / `ActiveCarry` and emits `CarryStarted` (metadata `distance`, `speed`, `stamina`). The carry ends at `EndTick`, on reaching the target, or when the ball leaves the carrier.

## Tackling (implemented)

`TackleIntent(Tackler, Carrier)` → `TackleResolver.Resolve` → `TackleResult` (ADR-014):

- Defender rating = Defending + 0.3·(Power − 5) + 0.3·(Awareness − 5). Carrier rating = Technique + 0.3·(Power − 5) + 0.3·(Awareness − 5).
- Roll contact `Sigmoid(0.3·(def − car) + 0.15)` (≈ 0.57 between equals).
- Roll **foul** (ADR-030, D9): p = 0.03 + 0.06 if contact failed − 0.006·(Defending − 5) + 0.06 × tiredness + 0.06 from behind (tackler > 0.5 m behind the carrier in the carrier's attacking direction) + 0.02 if sprinting; × 0.15 in the tackler's own penalty area; clamped to [0.005, 0.6] → **Foul**. Severity (for discipline) = clamp(0.2 + 0.06·(Power − 5) + 0.25 from behind + 0.15 sprinting + 0.15 missed ball, 0, 1).
- Without contact → **Lost**.
- Roll clean `clamp(0.6 + 0.03·(tackler Awareness − 5), 0.1, 0.9)` → **Won**, otherwise **LooseBall** (ball 2–5 m away, random direction; if that path crosses a line the ball is out of play off the tackler).
- Pace matters indirectly: faster defenders reach challenge range sooner.

`TackleSystem`: the nearest outfield opponent within 1.8 m of a carrier who has held the ball ≥ 0.75 s commits with probability rate × tick length, rate = `TackleBaseRatePerSecond` (0.17) × (contact chance ÷ an even duel's) × (1 + `TackleDangerBonus`, default 1) when the carrier is in its `FinalThird` (the defender's own third, a 35 m strip; ADR-027). Cooldowns: 1.5 s after any attempt, 3 s after being beaten (Lost), 2 s for a dispossessed carrier. Goalkeepers do not tackle.

Events: `TackleAttempted` (actor = tackler, target = carrier; metadata `contact`, `clean`, `stamina`, `carrierStamina`) then one of `TackleWon` (+ `PossessionChanged`), `TackleLost`, `TackleLooseBall` (position = where the ball went), `FoulCommitted` (metadata `chance`, `severity`) + `PlayStopped` — a direct free kick at the carrier's position, or a penalty if that is inside the tackler's own penalty area. A booked tackler's foul chance is × 0.6.

**Cards** (`Laws.Discipline`, ADR-032), decided for each foul in order, with "goal-side" = outfield defenders other than the tackler nearer their goal than the carrier (`Spatial.GoalSideCount`):

1. DOGSO — no one goal-side and the carrier within 25 m of goal: red outside the tackler's penalty area; inside it red from behind, otherwise yellow (D10).
2. Serious foul play — roll clamp((severity − 0.7) × 1.2, 0, 0.5) → red.
3. Promising attack — at most one goal-side and the carrier in the final third → yellow.
4. Reckless — roll clamp(0.07 + (severity − 0.3) × 0.55, 0, 0.6), × 0.5 if already booked → yellow.

A yellow for a booked player becomes `SecondYellow`. `MatchContext.Caution` emits `YellowCard` and/or `RedCard` (actor = offender, metadata `reason`: `Dogso`, `SeriousFoulPlay`, `PromisingAttack`, `Reckless`, `SecondYellow`) after `PlayStopped`, adds added time and, for a dismissal, calls `MatchState.SendOff`. If the side then has fewer than seven active players the match is abandoned: `MatchAbandoned` (team = that side, metadata `reason` = `TooFewPlayers`), then `MatchFinished`.

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

**Resolve**: `Roll(onTarget)` → fail = **Missed**: `Roll(0.5)` picks a side, target 0.5–4 m wide of a post on the goal line; if an outfield defender is within 1.5 m of the shooting lane, `Roll(0.35)` → deflected behind by the one nearest the shooter (`DeflectedBy`). Otherwise the target is a random point inside the goal mouth, then `Roll(save)` → **Goal**, or **Saved**: `Roll(clamp(0.6 + 0.05·(Keeping − 5), 0.2, 0.9))` → **Held**; else `Roll(0.4)` → **ParriedBehind**; else **Parried** back out 4–10 m from the goal line, forward and to a random side (`ReboundPosition`). Everything is decided at the strike (ADR-022). Travel duration = distance ÷ (18 + 1.2·Power) m/s.

**Strike**: emit `ShotTaken` (metadata `xg`, `onTarget`, `save`, `distance`, `stamina`, `keeperStamina`, `travel`) and launch a `BallFlight` carrying the decided `ShotResult`; it arrives after ⌈travel ÷ tick⌉ ticks (at least one). While it travels nobody has the ball and `AttackingTeam` is the shooting side.

**Land** (arrival tick):

- **Goal** — ball loose at the target, score +1, emit `GoalScored` (metadata `score`), then stop play: `Goal` stoppage, kickoff to the conceding side.
- **Saved** — emit `ShotSaved` (actor = keeper, target = shooter, metadata `save` = `Held` / `Parried` / `ParriedBehind`). Held: the keeper gets the ball (`PossessionChanged`, cause `Save`). Parried: the keeper touches it and the ball is knocked loose to the rebound spot. Parried behind: out of play off the keeper, 1 m outside the post — a corner.
- **Missed** — emit `ShotMissed` (target and metadata `deflected` = `true` when deflected); the ball is out over the goal line: a goal kick, or a corner when a defender deflected it.

**Penalty** (`ResolvePenalty`): on target `Sigmoid(0.5·(ability − 5) + 1.55)` (≈ 0.92), save `Sigmoid(0.35·(keeper rating − shot power) − 0.75)` (≈ 0.20), with ability, keeper rating and shot power as above without distance, angle or pressure; then the same placement, hold and parry draws. `ShotTaken` and `GoalScored` carry metadata `penalty`. A period waits for an awarded penalty to be taken; each penalty in regular time adds 45 s.

**Goalkeepers in possession**: cannot be tackled inside their own penalty area; never shoot or carry; distribute by passing.

## Utility AI (implemented)

ADR-018. Every decision: generate `ActionCandidate`s → `UtilitySelector.Choose` → execute.

**Selector**: apply `IUtilityModifier`s (none in M1) → plausible = utility > best − 8 → one plausible: take it (no random draw); several: pick with probability ∝ (utility − (best − 8)) using one `IMatchRandom` draw.

**Weights** live in `MatchConfig.Ai` (`AiTuning`: `OnBallWeights`, `OffBallWeights`). Defaults came from a random search over 84 configurations (M1.9) refined with 1 000-match batches (M1.11).

### On the ball (`OnBallCandidateGenerator`, every tick after the 0.5 s first touch)

| Candidate | Utility |
|---|---|
| Hold | 88 − 12 × seconds held − 30 × pressure. While carrying: 62 − 10 × seconds carrying − 30 × pressure. Pressure = 1 − (nearest opponent ÷ 3 m), 0 beyond 3 m. |
| Pass (each teammate ≥ 4 m whom the carrier judges onside: not beyond the offside line by more than 1.5 × (10 − Awareness) ÷ 9 m; D8) | 50 × success probability + 0.75 × forward progress (m) + 18 × goal proximity of receiver (1 on the goal line, 0 at ≥ 35 m) − 15 if goalkeeper |
| Carry (outfield, not mid-carry, > 4 m from the goal line) | 42 + 0.8 × forward gain + 2 × space ahead (≤ 12 m) − 30 × pressure × (1 − 0.05 × (Technique − 5)) |
| Shoot (outfield, ≤ 35 m, xG ≥ 0.03) | 22 + 318 × xG |

Goalkeepers only hold or pass.

### Off the ball (`OffBallDecisionSystem`, after `TeamShapeSystem`)

Skipped: goalkeepers, the carrier, the presser and a pass receiver (they keep the shape system's assignment). Everyone else follows their `OffBallPlan` until it expires or the attacking team changes, then re-decides:

| Candidate | When | Target / intensity | Utility |
|---|---|---|---|
| MoveIntoPosition | always | team-shape target and intensity (`Easy` in possession, `Jog` defending) | 40 |
| RecoverPosition | defending, > 10 m from shape target | shape target, sprint | 40 + 1.5 × (distance − 10) (+10 if ahead of the ball) |
| SupportBallCarrier | attacking, not a centre-back, ≤ 30 m from carrier | best of 8 spots 12 m around the carrier, jog; a winger's spot is kept within 8 m of their touchline, a full-back's within 12 m (`WideChannel`, ADR-034) | 28 + 2.5 × openness (≤ 10) + 2 × lane clearance (≤ 5) − 0.4 × travel (+4 if forward) |
| MakeForwardRun | attacking, ST/LW/RW/LCM/RCM, ball beyond 35 m | 1 m beyond the offside line (second-last opponent) but never closer than 16 m to the goal line, in the player's lane (wingers 5 m from their touchline, LCM/RCM centre ± 6 m, ST centre), sprint; until a teammate's pass is in the air the runner waits at the current offside line + 1.5 × (10 − Awareness) ÷ 9 − 0.5 m (onside when aware, up to 1 m beyond when not) | 31 + 0.8 × Pace + min(20, 0.6 × space behind the line) + role bonus (ST 8, wingers 5) |
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
| Possessions won | `PossessionChanged` whose `cause` is open play (not restarts) |
| Throw-ins / goal kicks / corners / free kicks | `RestartTaken` by type, for the team taking it |
| Offsides | `PlayStopped` with reason `Offside`, for the offender's team |
| Fouls | `FoulCommitted`, for the offender's team |
| Yellow / red cards | `YellowCard` / `RedCard`, for the offender's team (a second yellow counts in both) |
| Penalties / scored | `RestartTaken` `Penalty`; `GoalScored` with `penalty` |
| Ball-in-play seconds | each `Kickoff` or `RestartTaken` → next `PlayStopped`, `PeriodEnded` or `MatchFinished` |
| Possession seconds | ball-in-play time only, switching at each restart and `PossessionChanged`; loose and in-flight time counts for the last team in control |

Full-match tests check the statistics against the score, the event log and a 5 400 s possession total.

## Batch simulation (implemented)

ADR-020. `DebugRunner batch [count] [--from SEED]` plays `count` matches (default 1 000, seeds from 1) in parallel, aggregates them in seed order and runs `BatchHealth` pathology bands; it exits 1 if any band fails. CI runs it on every code PR.

Current defaults, 1 000 matches between all-5 teams (after M2.7b width):

| Figure | Value |
|---|---|
| Goals per match | 1.95 (max 7) |
| Shots / on target per match | 19.1 / 5.9 (31 %) |
| Passes per match, completion | 1 369, 82 % |
| Home win / draw / away win | 33.8 % / 33.5 % / 32.7 % |
| Home possession | 50.0 % |
| Tackles / carries / possession changes | 267 / 222 / 312 |
| Striker share of goals | 76 % |
| Outfield distance, stamina at full time | 14.0 km, 54.0 |
| Throw-ins / goal kicks / corners / offsides per match | 23.4 / 11.0 / 4.7 / 3.1 |
| Fouls; penalties (scored) per match | 22.8; 0.14 (68 %) |
| Yellow / red cards per match; abandoned | 3.42 / 0.16; 0 % |
| Ball in play; added time per half; halves ended by the safety cap | 77.9 min; 1.8 min; 6 % |

Health bands also cover, per match, goal kicks (8–35), corners (3–20), offsides (1–15), fouls (8–45), penalties
(≤ 0.8), yellow cards (0.5–8), red cards (≤ 0.6), added minutes per period (0–10) and abandoned matches (≤ 0.1 %).
Width (ADR-034) brought throw-ins from 8.1 to 23.4 (real ≈ 40), but the ball is still in play ~78 of ~94 minutes
(real ≈ 55–60) and goals fell to 1.95 (real ≈ 2.7) as more attacks go wide without crosses. The match-realism steps
(clearances, crosses, dead-ball times; plan M2.7b) address these; until then three bands are interim (ADR-033): ball in
play 45–85 min, throw-ins 5–70, halves ended by the safety cap ≤ 25 %.

## Observation (implemented)

ADR-025. `MatchFrame.Capture(state)` copies the end-of-tick state: tick, `MatchTime`, phase, stoppage, score, attacking
team, ball (position, holder, flight: kind, team, start, landing point, launch/arrival tick, intended receiver) and every
player (id, team, slot, shirt number, position, movement target and intensity, stamina, off-ball plan kind, carrying).
Frames never change once captured; capturing one every tick leaves the match identical (fingerprint test). The
landing point is for animation; tactical analysis must not read predetermined outcomes.

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
- `PossessionCause` (ADR-021): `Reception` (completed pass), `Interception`, `Tackle`, `LooseBall`, `Save` — won in open play — and `Kickoff`, `ThrowIn`, `GoalKick`, `Corner` (and the retired `KeeperRestart`) — restarts, not won. `MatchEvent.GetPossessionCause()` reads it back.
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
- Constants: goal width 7.32, penalty area 16.5 deep × 40.32 wide, goal area 5.5 deep × 18.32 wide.
- `Contains` / `Clamp` for boundary checks. Tactical zones and areas: `MatchState.Zones`, see `TACTICAL_ANALYSIS.md`.

`Vec2` is a `double` value type. Only basic arithmetic and `Math.Sqrt` are used (IEEE-exact, deterministic on a given runtime). Avoid introducing trigonometric functions into outcome-affecting code without considering cross-platform determinism.

## Match state (implemented)

`MatchState.Create(MatchConfig config, TeamDefinition home, TeamDefinition away, Func<TeamSide, PlayerDefinition, Vec2> startingPosition)` builds the authoritative state:

- validates that player ids are unique across both teams and that every starting position is on the pitch;
- `Players` is an ordered list of 22 `PlayerMatchState` — home then away, each in team-definition order — including sent-off players. Football systems iterate `ActivePlayers` or `TeamPlayers(side)` (active only, same order). **Iterate these lists, never a dictionary, when order can affect outcomes.**
- `Goalkeeper(side)` is the side's active player in the GK role. `SendOff(id)` removes a player from play (ADR-031); `Abandoned` is set when a side falls below `MinimumPlayers` (7).
- `Ball` starts loose at the centre spot; `Score` 0–0; `Phase` = `Kickoff`.
- `Clock` (`MatchClock`) and `CurrentStoppage` (while play is stopped) are also part of the state. A new match starts `Stopped`, awaiting the opening kickoff.

## Player state (implemented)

- `PlayerDefinition` — immutable: `Id`, `Name`, `ShirtNumber`, `Position` (4-3-3 slot, ADR-008), `Stats`.
- `PlayerStats` — the nine ADR-002 attributes, each validated to 1–10.
- `PlayerMatchState` — mutable: `Role` (slot played now; starts as the listed position), `Status` (`Active` / `SentOff`; `IsActive`), `YellowCards`, `Position`, `MovementTarget`, `MovementIntensity`, `CurrentStamina` (0–100, starts at 100), `LastTickDistance`, `DistanceCovered`, `CurrentIntent`, `ActiveCarry`, `OffBallPlan`, `TackleCooldownUntilTick`.

## Ball and possession (implemented)

`BallState` holds `Position` and `PossessorId`; `IsLoose` is derived. Possession is stored **only** on the ball (ADR-007):
`MatchState.HasBall(id)`, `PossessionPlayerId` and `PossessionTeam` are computed from it, so two players can never both have the ball.
A pass or shot in the air is `BallState.Flight` (`BallFlight`: the resolved `PassResult` **or** `ShotResult` plus launch/arrival ticks); `IsInFlight` / `IsLoose` are derived. A loose ball may be rolling (`BallState.Roll`, a `LooseBallMotion`). `MatchState.LastTouch` (`BallTouch`: player, team) is set by every possession, pass or shot strike, a tackle that knocks the ball loose, a parry and a deflection; it decides the restart when the ball goes out. A shot still in the air when the match reaches 90:00 is abandoned (interim until M2.1d, where a period ends only at a stoppage).

## Invariants (implemented)

`MatchStateInvariants.FindViolations(state)` / `Verify(state)` check: an active carry exists only on the ball carrier; 22 players, 7–11 active per side with exactly one goalkeeper and unique roles, sent-off players off the pitch and never on the ball, unique ids, possessor is an active player and the ball is at their position, ball on the pitch, a ball in flight has no possessor, non-negative score, players on the pitch, stamina within bounds. Each message names the offending player. The clock cannot decrease by construction; the test helper `CheckedRun` additionally verifies invariants and clock advancement after every tick of a full match.

## Current limitations

- **Striker dominance**: strikers score ~76 % of goals; wingers and midfielders rarely finish moves.
- **Volume**: ~1 370 passes, ~265 tackles and ~310 possession changes per match (~78 minutes of ball in play) — busy compared with real football.
- **Distance**: 14.0 km per outfield player (real ≈ 10–12 km); inside the 16 km band.
- **Sensitivity**: shot volume is steep in `ShotBase` (22 → ~28 shots; 16 → ~14). Tune with batches only.
- Few stoppages: ~40 ball-out restarts (23.4 throw-ins), ~23 fouls and ~3 offsides per match, so ~6 % of halves still end at the 180 s safety cap; dead-ball times are the bare minimums. Clearances, crosses and dead-ball times follow (M2.7b).
- **Goals** fell to ~1.95 per match with width (ADR-034): wide attacks have no crosses yet. Full-backs receive under 1 % of passes each.
- No advantage (D7): every foul stops play. No substitutions, so a dismissal is permanent and nobody is replaced.
- Set pieces are simple: a throw-in is an ordinary pass limited to 25 m; corners are a pass into the box; no headers, routines or short corners.
- No blocks as events or posts; deflections only send misses behind.
- Presentation snaps (measured over 20 matches, M2.2): passes land exactly on the receiver or interceptor (max 0.4 m),
  but on a held save the ball jumps from the goal mouth to the keeper (median 2.5 m, max 4.3 m, M2.2 figure). Misses
  now stay on the goal line until the goal kick.
