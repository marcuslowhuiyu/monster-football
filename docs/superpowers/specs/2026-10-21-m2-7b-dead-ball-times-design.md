# M2.7b step 4 — Dead-ball times

Status: approved design (2026-10-21). Part of **M2.7b match realism** (plan `docs/milestones/MILESTONE_2_PLAN.md`).

## Context

After crosses (ADR-036), 1,000 matches give 72.9 min of ball in play (real 55–60). Each restart waits a fixed minimum
(`RestartTuning`: throw-in 8 s, goal kick 15 s, corner 20 s, free kick 20 s, offside free kick 12 s, goal 45 s, penalty
45 s) and is taken almost as soon as it is over, so the ball is dead for about 22 min a match. Real stoppages are
longer and vary: teams protecting a lead slow down late on, trailing teams hurry, set pieces near goal take time to
organise, and bookings stop the game.

## Goal

Each stoppage gets its own dead-ball time, drawn around a real average for its type and shaped by match context —
game state, pitch location and cards — and the referee adds time wasted by a leading side to the added time.

Non-goals: substitutions and injuries (not modelled); time-wasting cautions; extending added time for time wasted
during added time; tuning to final values (step 5, calibration pass).

## Behaviour

### When it is decided

At `MatchContext.StopPlay`, after the stoppage is recorded: the dead-ball time is computed once
(`DeadBallDuration.Compute`) and stored on the `Stoppage` (`DeadBallSeconds`). The card for a foul is already known
(`Stoppage.Discipline`). Stoppages begun directly with `BeginStoppage` (match start, period start) keep no dead-ball
time (0 s) and draw nothing.

`RestartSystem.Advance` waits `stoppage.DeadBallSeconds` instead of the per-type minimum; the restart-setup phase and
its timeout (`SetupTimeoutSeconds`, counted from the end of the dead-ball time) are unchanged.

### The formula

`seconds = average(type) × spread × gameState × location + cardSeconds`

**Average by type** (`DeadBallTuning`, real-match approximations, provisional):

| Stoppage | Average |
|---|---|
| Throw-in | 15 s |
| Goal kick | 25 s |
| Corner | 35 s |
| Direct free kick | 30 s |
| Indirect free kick (offside) | 20 s |
| Goal (to the kickoff) | 60 s |
| Penalty | 90 s |

Any other stoppage reason keeps 0 s and draws nothing.

**Spread**: one `IMatchRandom.NextDouble` draw u per stoppage; spread = `SpreadMin` + (`SpreadMax` − `SpreadMin`) × u,
with `SpreadMin` 0.65 and `SpreadMax` 1.35 (mean 1).

**Game state**, judged for the side taking the restart (`Stoppage.AwardedTeam`):

- margin m = that side's goals − the opponent's goals; lateness ℓ = clamp((r − `GameStateFromFraction` × R) ÷
  ((1 − `GameStateFromFraction`) × R), 0, 1), where r is regular time played so far in the match (completed periods
  count `RegularPeriodSeconds` each; the current period counts min(elapsed, `RegularPeriodSeconds`)) and R =
  `PeriodCount` × `RegularPeriodSeconds`. With `GameStateFromFraction` 2/3 the effect starts at minute 60 and is full
  from minute 90 (and throughout added time).
- strength s = ℓ × min(|m|, `GameStateFullMargin` 2) ÷ `GameStateFullMargin`.
- leading (m > 0): gameState = 1 + (`MaxSlowdown` 0.6) × s; trailing (m < 0): gameState = 1 − (`MaxHurry` 0.4) × s;
  level: 1.
- For a goal, the conceding side takes the kickoff, so it hurries when behind late on.

**Location** (team frame of the side taking the restart, X ≥ `Zones.FinalThirdX`):

- direct or indirect free kick in the attacking third: × `AttackingFreeKickFactor` 1.3 (wall, players go up);
- throw-in in the attacking third: × `AttackingThrowInFactor` 1.2;
- otherwise × 1. (Goal kicks, corners, penalties and kickoffs have fixed locations.)

A separate rule for quick restarts deep in a trailing side's own half is not added: the trailing hurry already covers
it.

**Cards**: + `BookingSeconds` 30 for a yellow; + `DismissalSeconds` 45 for a red or second yellow (instead of the
booking time).

### The referee adds wasted time

wasted = average × spread × location × (gameState − 1) when gameState > 1, else 0 — the part of the stoppage caused by
the leading side's slow-down. In regular time (`Clock.Tick < RegularEndTick`, as for goals and cards) it is added to
`MatchState.AddedTimeAllowanceSeconds`. The existing per-goal, per-penalty and per-card allowances are unchanged.

## Code

- **`Restarts/DeadBallTuning.cs`** (new, public record with `Default`, exposed as `MatchConfig.DeadBall` like
  `RestartTuning`/`AddedTimeTuning`): averages, spread, game-state, location and card values above.
- **`Restarts/DeadBallDuration.cs`** (new, internal, pure apart from the one draw): `Compute(MatchState, Stoppage,
  DeadBallTuning, IMatchRandom)` → `DeadBallTime(double Seconds, double WastedSeconds)`; helpers for lateness and
  game-state factor, testable on their own.
- **`Stoppage.DeadBallSeconds`** (init property, default 0).
- **`MatchContext.StopPlay`**: compute, store on the stoppage, add wasted time in regular time; `PlayStopped` metadata
  gains `deadBall` and `wasted` (seconds, invariant culture, round-trip format so diagnostics and the added-time test
  can sum them exactly).
- **`RestartSystem`**: waits `stoppage.DeadBallSeconds`; the per-type `*DeadBallSeconds` fields leave `RestartTuning`
  (replaced by `DeadBallTuning`).
- **Diagnostics**: `TeamStatistics`/`MatchReport` unchanged except the batch: `BatchSummary` gains mean dead-ball
  seconds per restart type and wasted seconds per match; `Program.cs` prints them. The ball-in-play health band stays
  45–85 until step 5.

Determinism: one draw per stoppage stopped through `StopPlay` with a non-zero average; no trigonometry; iteration
orders unchanged. **Fingerprints change** (ledger #14).

## Testing (written first)

- `DeadBallDuration`: average × spread at the extremes (draws 0 and 0.9999) for each type; match start / period start
  → 0 with no draw; lateness 0 before minute 60, 0.5 at minute 75, 1 at 90 and in added time and in the second half's
  timing (period 2 counts period 1 in full); leading by 1 at minute 90 → × 1.3, by 2 or more → × 1.6; trailing by 2 at
  minute 90 → × 0.6; level → × 1; game state judged for the awarded side (Home leading, Away restart → hurry); free kick
  and throw-in in the attacking third use the factors, in the middle third do not, Home and mirrored Away; booking
  +30, red +45, second yellow +45; wasted = slow-down part only, 0 when hurrying or level.
- `MatchContext.StopPlay`: stores `DeadBallSeconds`, emits `deadBall` metadata, adds wasted time to the allowance in
  regular time only.
- `RestartSystem`: the restart is not taken before the stoppage's dead-ball time; setup timeout counted from its end.
- Existing restart tests that relied on the fixed minimums are updated to read the stoppage's time (premise fixes
  explained in commits); full-match invariant, determinism and statistics tests stay green.

## Measuring and success criteria

`batch 1000` before and after, and seeds 1001–3000 for Home/Away balance.

- Batch HEALTHY.
- Ball in play 52–65 min (real 55–60; final tuning in step 5).
- Mean dead-ball seconds per type within 10 % of the averages above (spread mean 1; late game-state effects nudge them).
- Added time per half up modestly (wasted time in close late games); safety-cap share not worse than now (2 %).
- Home/Away: possession 50 ± 1 %, win gap within noise.

## Delivery

Branch `feat/m2-7b-dead-ball-times`, one PR:

1. `feat(restarts): dead-ball duration model` — `DeadBallTuning`, `DeadBallDuration`, tests; unused; fingerprints
   identical.
2. `feat(restarts): stoppages use their own dead-ball time` — `Stoppage.DeadBallSeconds`, `StopPlay`, `RestartSystem`,
   wasted time to added time, metadata; **fingerprints change** (ledger #14).
3. `feat(diagnostics): dead-ball times in the batch` — fingerprints identical.
4. `docs: dead-ball times (ADR-037)`.
