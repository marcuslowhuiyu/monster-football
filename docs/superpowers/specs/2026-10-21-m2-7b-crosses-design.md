# M2.7b step 3 — Crosses (open play and corners)

Status: approved design (2026-10-21). Part of **M2.7b match realism** (plan `docs/milestones/MILESTONE_2_PLAN.md`).

Values below are the first design; the provisional tuning that superseded some of them is recorded in ADR-036.

## Context

After width (ADR-034) and clearances (ADR-035), 1,000 matches give 2.11 goals per match (real ≈ 2.7), 4.9 corners
(real ≈ 10), 27.6 throw-ins (real ≈ 40) and 77.1 min of ball in play (real 55–60). In the final third a wide player can
only cut inside, pass or shoot, so wide attacks rarely become chances; and a corner is just a pass to an attacker in
the box, who may then shoot. Real corners come largely from defenders heading crosses behind.

## Goal

Wide players in the final third can **cross**: deliver the ball to a spot in the box where attackers and defenders
contest it in the air. Corners are taken as crosses too, so one aerial model serves open play and set pieces.

Non-goals: headers other than from crosses; flick-ons; short corners; changes to corner layouts or defensive set-piece
shape; dead-ball times (step 4).

## Behaviour

### When (utility AI, ADR-018)

- `OnBallCandidateGenerator` adds a **Cross** candidate (`ActionKind.Cross`) for an outfield carrier in the final third
  (team-frame X ≥ `Zones.FinalThirdX`) and **wide**: outside the width of the penalty area (|team-frame Y − centre| >
  `PitchGeometry.PenaltyAreaWidth` / 2).
- Utility = `CrossBase` + `CrossAttackerWeight` × min(attackers in the opposition penalty area, 3) −
  `CrossDefenderWeight` × min(defenders in it, 6) + `CrossDepthWeight` × depth, where depth = (X − FinalThirdX) ÷
  (pitch length − FinalThirdX) ∈ [0, 1] (team frame). Starting values: base 30, attacker 8, defender 2, depth 10 —
  tuned with the batch only.
- **Corners**: the corner taker always crosses (`CrossKind.Corner`), replacing the corner branch of the restart's pass.

### Where (decided at the strike)

- Target: the teammate in the opposition penalty area with the most space (largest distance to the nearest opponent;
  ties → earlier player); if none, the penalty spot.
- Scatter: two draws from `IMatchRandom`, in this order — sideways ± (`BaseLateralScatter` 3 m +
  `LateralScatterPerPassing` 0.5 m × (10 − Passing)) and lengthways ± `LengthScatter` 3 m, applied across and along the
  delivery direction (no trigonometry).
- Flight: travel time = distance ÷ `BallSpeed` (20 m/s).
- At the strike: the crosser's touch is recorded (`LastTouch`); offside is judged as for a pass **except from a corner**
  (Law 11); `Cross` event (actor = crosser, team, position = start, metadata `kind`, `distance`).
- In flight, a path crossing a line goes out there by last touch (as clearances).

### The contest (decided on arrival)

Passes and shots are decided at the strike (ADR-012); a cross has no named receiver, so its contest is decided on
arrival with real positions — a deliberate exception recorded in the ADR. While it flies, the nearest outfield attacker
and nearest outfield defender to the landing point sprint to it (`TeamShapeSystem`, like the clearance chase).

On arrival at landing point L, `AerialContest` looks at active players within `ContestRadius` (3 m) of L:

1. **Keeper claim**: if L is in the defending goal area (`PitchGeometry.GoalAreaDepth` × `GoalAreaWidth`) and the
   defending keeper is within the radius, the keeper catches it: possession to the keeper (cause `Save`), `CrossClaimed`
   outcome. No draw.
2. **Attacker vs defender**: best attacker A and best defender D (highest aerial rating; ties → nearer, then earlier
   player). Aerial rating = 0.5 × Power + 0.3 × Awareness + 0.2 × Technique (attacker) or Defending (defender). P(attacker
   wins) = Sigmoid(`ContestScale` 0.4 × (A − D) + `ContestBias` −0.14) (≈ 0.43 between equals — `Curves.Sigmoid` is
   the algebraic 0.5 + 0.5x/√(1+x²), not the logistic curve); one draw.
3. **Only one side present**: that side wins, no draw.
4. **Nobody within the radius**: the ball lands loose and rolls on (35 % of the flight distance, as a misplaced pass).

Outcomes:

- **Attacker wins** → a first-time shot from the attacker's position: `ShotResolver.ResolveFirstTime` = the ordinary
  shot estimate with `FirstTimeAbilityPenalty` (1.5) subtracted from the shooter's ability, then the usual on-target,
  save, placement, hold/parry/deflection draws. `ShotResult.FromCross`; `ShotTaken`/`GoalScored` metadata `cross`.
  The ball is given to the attacker for the strike (no first-touch delay).
- **Defender wins** → a headed clearance (`HeaderClearance`): with `BehindChance` 0.25 the ball goes behind the
  defender's own goal line (a corner, last touch the defender); otherwise it is knocked 15–25 m (one draw) away from the
  defender's goal along the line from goal to L, landing loose and rolling, possibly out (throw-in) by last touch.
  The defender's touch is recorded.
- **Keeper claims** → as above.

The `Cross` event gains no later edit; the outcome is a separate `CrossContested` event (actor = winner or none,
metadata `outcome` = `Shot` / `Cleared` / `Claimed` / `Loose`).

## Code

- **`Actions/Crossing/`** (new): `CrossIntent(PlayerId Crosser, CrossKind Kind = OpenPlay)`; `CrossKind { OpenPlay, Corner }`;
  `CrossResult(Team, Crosser, Kind, StartPosition, IntendedTarget, TargetPosition, TravelDuration)` (public, with
  `Distance`); `CrossResolver` (`Aim`, `Resolve`); `AerialContest` (pure: contestants, keeper claim, win chance);
  `CrossExecution` (`Launch`, `LeavePlay`, `Land` → contest and outcome); `HeaderClearance`.
- **`BallFlight`**: `ForCross`; exactly one of `Pass`, `Shot`, `Clearance`, `Cross`. `BallFlightSystem`: crossing
  check also for crosses; arrival → `CrossExecution.Land`. `MatchState.LaunchBall`: touch for the crosser.
  `MatchFrame`: `BallFlightKind.Cross`.
- **`ShotResolver.ResolveFirstTime`**, `ShotResult.FromCross`, shot/goal metadata `cross`.
- **Events**: `MatchEventType.Cross`, `MatchEventType.CrossContested` (appended).
- **AI**: `ActionKind.Cross`; candidate and weights (`OnBallWeights`); `OnBallDecisionSystem` executes a `CrossIntent`.
- **Chase**: `TeamShapeSystem` — for a cross, the nearest outfield attacker and defender sprint to the landing point.
- **Corners**: `RestartSystem` takes a corner by launching a `CrossKind.Corner` cross; the corner branch of
  `ChooseReceiver` is removed.
- **Statistics / diagnostics**: per team `Crosses`, `ShotsFromCrosses`, `GoalsFromCrosses`; report line and statistics
  rows; batch averages and output; health band **crosses 10–80 per match**.

Determinism: draws only when a cross is struck (2), contested between both sides (1) and on a defender's header (1–2);
no trigonometry; iteration orders unchanged; ties broken without favouring a side's list order (nearer first; a
cross-team tie is impossible because attacker and defender are chosen per side).

## Testing (written first)

- `CrossResolver`: aims at the attacker in the box with the most space; penalty spot without one; scatter bounds with
  scripted extremes; Home and mirrored Away.
- `AerialContest`: keeper claim in the goal area (and not outside it); a lone attacker / defender always wins; a
  stronger attacker wins more often (chance rises with rating); nobody within 3 m → loose.
- `CrossExecution` (scripted random): attacker win → first-time shot with `cross` metadata; defender win → corner with
  a low draw, header away with a high draw; a path over the goal line → goal kick / corner by last touch; offside
  judged in open play, not from a corner.
- `ShotResolver.ResolveFirstTime`: lower on-target chance than the same shot taken normally.
- Candidate: offered only wide in the final third; utility rises with attackers in the box and falls with defenders.
- Corner: the taker crosses (a `Cross` event with kind `Corner`, no `PassAttempted`); corner tests updated.
- Existing full-match invariant, determinism and statistics tests stay green (seed-dependent premises fixed in tests,
  explained in commits).

## Measuring and success criteria

`batch 1000` before and after, plus a probe (crosses by outcome, shots and goals from crosses, ball-out causes), and a
second batch on seeds 1001–3000 for Home/Away balance.

- Batch HEALTHY.
- 25–45 crosses per match, corners included (real ≈ 35–45).
- Corners clearly up from 4.9 (real ≈ 10).
- Goals up from 2.11 (real ≈ 2.7).
- Home/Away: possession 50 ± 1 %, win gap within noise.

If crosses fall outside 25–45, adjust only the cross weights; anything else is reported to the user first.

## Delivery

Branch `feat/m2-7b-crosses`, one PR:

1. `feat(actions): cross resolver and aerial contest` — unused; fingerprints identical.
2. `feat(actions): cross flight, contest outcomes, first-time shots and headed clearances` — never chosen; identical.
3. `feat(diagnostics): report crosses` — identical.
4. `feat(ai): wide players cross` — candidate, chase, band; **fingerprints change** (ledger #13).
5. `feat(restarts): corners are crosses` — **fingerprints change** (ledger #13).
6. `docs: crosses (ADR-036)`.
