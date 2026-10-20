# M2.7b step 2 — Clearances (D6)

Status: approved design (2026-10-20). Part of **M2.7b match realism** (plan `docs/milestones/MILESTONE_2_PLAN.md`);
implements decision D6, triggered at the M2.7 calibration checkpoint (ADR-033).

## Context

After step 1 (width, ADR-034), 1,000 matches give 23.4 throw-ins per match (real ≈ 40), 77.9 min of ball in play
(real 55–60), 6 % of halves ended by the safety cap, and 1.95 goals (real ≈ 2.7). A defender under pressure near their
own goal can only hold, pass or carry: there is no way to simply get rid of the ball, which in real football produces
roughly 35–45 clearances per match, many of them out for throw-ins or corners.

The ball can currently travel only as a **pass** (to a teammate) or a **shot**. A misplaced pass is the only ball that
lands loose and rolls on.

## Goal

Defenders under pressure in their own third can **clear**: kick the ball to a spot — wide towards the touchline when
in danger, long upfield otherwise — with no target player. It lands loose, often goes out, and is contested.

Non-goals: blocked/charged-down clearances, headers, goalkeepers punting from their hands, crosses (step 3), changes to
passing.

## Behaviour

### When (utility AI, ADR-018)

- `OnBallCandidateGenerator` adds a **Clear** candidate (`ActionKind.Clear`) for the carrier — outfield players and
  goalkeepers — only when the carrier is in their own defensive third: team-frame X < `Zones.DefensiveThirdX` (new, `Pitch.Length / 3`
  = 35 m on the default pitch, next to the existing `FinalThirdX`).
- Utility = `ClearBase` (20) + `ClearPressureWeight` (40) × pressure + `ClearDangerWeight` (30) × danger, where
  pressure is the generator's existing value (`1 − nearest opponent / PressureRadius`, 0 beyond 3 m) and danger =
  max(0, 1 − distance to own goal centre / `ClearDangerRange` (35 m)).
- Rough scale: full pressure near goal ≈ 85 (beats a typical pass at 45–60); unpressured ≈ 20–45 (loses to a safe
  pass). Values live in `OnBallWeights` and are tuned with the batch only.

### Where (decided at the strike)

- **Wide** when pressure ≥ `ClearWidePressure` (0.5) and the carrier is within `ClearWideRange` (25 m) of their own
  goal: aim at the nearest touchline (team-frame Y = 0 or width), `ClearWideAdvance` (25 m) further upfield than the
  carrier.
- **Long** otherwise: aim upfield along the line from the carrier towards the team's striker (the active player in the
  ST role; if none, straight upfield), at the clearance distance.
- **Distance** (long; also caps a wide clearance): `BaseDistance` 35 m + `DistancePerPower` 2 m × (Power − 5).
- **Scatter** (two draws from `IMatchRandom`, in this order): lateral offset uniform in ±(`BaseLateralScatter` 4 m +
  `LateralScatterPerTechnique` 0.8 m × (10 − Technique)); length offset uniform in ±`LengthScatter` 5 m. Offsets are
  applied perpendicular to and along the aim direction, without trigonometry.
- **Flight**: travel time = distance ÷ `BallSpeed` (18 m/s).

### What happens

- The clearer's touch is recorded at the strike (`MatchState.LastTouch`), and offside is judged then, as for a pass
  (ADR-029): an attacker in an offside position who first collects the clearance commits the offence.
- **In flight**, if the path crosses a line, the ball is out of play there (`Laws.OutOfPlay`), restart by last touch:
  touchline → throw-in to the opponents; the opponents' goal line → goal kick; own goal line → corner.
- **On arrival** the ball is loose and rolls on for `PassExecution.RollOnFraction` (0.35) of the flight distance
  (`LooseBallMotion`), going out if it crosses a line; the nearest player within the control radius collects it.
- **The chase**: while a clearance is in flight, the nearest outfield player of each side to its landing point sprints
  to that point (`TeamShapeSystem`, next to the existing pass-receiver rule).

## Code

- **`Actions/Clearing/`** (new):
  - `ClearanceIntent(PlayerId Kicker)` — an `ActionIntent`.
  - `ClearanceKind { Wide, Long }`.
  - `ClearanceResult(Team, Kicker, Kind, StartPosition, IntendedTarget, TargetPosition, TravelDuration)` — public,
    like `PassResult`.
  - `ClearanceResolver` — `Aim(state, kicker)` (deterministic: kind and intended target) and `Resolve(state, intent,
    random)` (aim + scatter + travel). Constants as named above.
  - `ClearanceExecution` — `Launch(context, result)` (launch the flight, judge offside, emit `Clearance`) and
    `Land(context, result)` (loose and rolling); `LeavePlay` when the flight crosses a line.
- **`BallFlight`** — `ForClearance(result, launch, arrival)`; exactly one of `Pass`, `Shot`, `Clearance`; `Team`,
  `StartPosition`, `TargetPosition` cover all three.
- **`BallFlightSystem`** — the crossing check applies to clearances as well as passes; arrival dispatches to
  `ClearanceExecution.Land`.
- **`MatchState.LaunchBall`** — records the touch for a clearance's kicker.
- **AI** — `ActionKind.Clear`; the candidate in `OnBallCandidateGenerator`; weights in `OnBallWeights`;
  `OnBallDecisionSystem` executes a `ClearanceIntent`.
- **`TeamShapeSystem`** — the chase rule.
- **Events** — `MatchEventType.Clearance` (appended; actor = clearer, team, position = start, metadata `kind`,
  `distance`).
- **Statistics / diagnostics** — `TeamStatistics.Clearances` (from `Clearance` events); `MatchReport` line;
  `BatchSummary.AverageClearances`; batch output line; health band **clearances per match 10–80**.
- **`TacticalZones`** — `DefensiveThirdX` (`Pitch.Length / 3`), beside `FinalThirdX`.
- **Observation** — `BallFlightKind.Clearance` in `MatchFrame`; `IntendedReceiver` null. The Godot viewer draws only
  the ball position and needs no change.

Determinism: two new draws per clearance, only when one is chosen; no trigonometry; iteration orders unchanged.

## Testing (written first)

- `ClearanceResolver`: wide vs long by pressure and distance to own goal; wide aims at the nearest touchline and
  upfield; long aims towards the striker (straight upfield without one); distance grows with Power; with scripted
  draws at the extremes the landing point stays within the scatter bounds; Home and mirrored Away.
- Candidate: offered only in the defensive third; utility rises with pressure and with danger; with no pressure and a
  safe pass available a pass scores higher.
- Execution (`Scenario`, scripted random): a clearance lands loose and rolling; a path over the touchline stops play
  for a throw-in to the opponents; an attacker offside at the strike who collects it is penalised; `Clearance` event,
  `LastTouch` and statistics recorded.
- Chase: while a clearance flies, the nearest outfield player of each side targets the landing point.
- Frames: a clearance in flight appears as `BallFlightKind.Clearance` with no intended receiver.
- Existing full-match invariant, determinism and statistics tests stay green (seed-dependent premises are fixed in the
  test, with the reason in the commit).

## Measuring and success criteria

`batch 1000` before and after, plus the probe (ball-out restarts by cause, including after a clearance).

- Batch HEALTHY.
- 20–50 clearances per match (real ≈ 35–45).
- Throw-ins clearly up from 23.4, towards ≈ 40.
- Ball in play down a little from 77.9 min.
- Goals not falling below ≈ 1.9.

If a criterion is missed, report the figures before tuning anything beyond the clearance weights.

## Delivery

Branch `feat/m2-7b-clearances`, one PR:

1. `feat(actions): clearance flight, resolver and execution` — everything except the AI candidate and the chase; the
   AI never clears, so fingerprints are identical.
2. `feat(ai): defenders clear under pressure` — the candidate and the chase; **fingerprints change** (ledger #12).
3. `docs: clearances (ADR-035)` — ADR-035; `MATCH_SIMULATION` (on-ball AI, clearances, flight, statistics, batch
   figures), `LAWS_OF_THE_GAME` (clearances going out), `GLOSSARY` (clearance), `ARCHITECTURE` (`Actions.Clearing`),
   `CURRENT_STATE`, plan row b2 and ledger #12.
