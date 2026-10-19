# M2.7b step 1 — Width: wide players hold the touchline

Status: approved design (2026-10-19). Part of **M2.7b match realism**, an addition to Milestone 2 made after the
M2.7 calibration checkpoint (ADR-033).

## Context

After M2.7, matches are far from real football in how often the ball stops:

| Figure | Simulation (1,000 matches) | Real football |
|---|---|---|
| Throw-ins per match | 8.1 | ≈ 40 |
| Ball in play | 79.4 of ~95 min | 55–60 min |
| Halves ended by the safety cap | 10 % | — (milestone target ≤ 1 %) |

The user chose to aim for real football rather than only the milestone's bands. That needs four steps, each with
its own spec, plan and PR, measured with a batch after each:

1. **Width** — this spec.
2. **Clearances** (D6) — defenders under pressure in their own third kick long and wide, with no target.
3. **Crosses** — wide players in the final third deliver into the box.
4. **Dead-ball times** — stoppage durations from real averages instead of the bare minimums; the final calibration
   towards 55–60 min of ball in play.

The interim bands of ADR-033 (ball in play 45–85 min, throw-ins 5–70, safety cap ≤ 25 %) are tightened to the
milestone's targets (45–75, 15–70, ≤ 1 %) by the end of step 4.

### Why width first

A probe over 40–100 matches (seeds from 1, all-5 teams) showed:

- In open play the ball is within 5 m of a touchline only **1.9 %** of the time (8.5 % within 10 m).
- Ball-out restarts per match: goal kick after a missed shot 12.8; **throw-in after a misplaced pass 8.0**; corner
  after a missed shot 6.1; goal kick after a misplaced pass 2.5; corner after a save 0.9; **throw-in after a tackle
  0.1**.
- Wingers' in-possession anchors are 5 m from the touchline, but they average **13–16 m** from it when their team
  has the ball. Their forward runs aim at centre ± 10 m (24 m from the line), support spots are anywhere around the
  carrier, and carries are pulled towards the middle.
- Full-backs average ~9 m from the line but receive only **0.7 %** of completed passes each; the front five receive
  ~95 %. Only 5.3 % of completed passes are received within 8 m of a touchline.

Play rarely reaches the touchlines, so misplaced passes and tackle knocks rarely go out. Width changes the shape the
later steps build on, so it comes first.

## Goal

When their team has the ball, wide players (LW, RW, LB, RB) hold a **wide channel** along their touchline, so play
reaches the flanks and the ball goes out over the touchline about as often as the play near it warrants.

Non-goals: pass utility changes; out-of-possession shape; central players; crosses (step 3).

## Behaviour

All in team frame (own goal at X = 0, attacking +X, left flank +Y). A role's touchline is the left one (Y = width) for
LW and LB and the right one (Y = 0) for RW and RB.

| Where | Today | New |
|---|---|---|
| In-possession shape (`TeamShapeSystem.AttackingTarget`) | every slot shifts 0.25 × the ball's Y offset from centre, so the far-side winger drifts inside | wide roles shift 0.1 × the ball's Y offset (`WideAttackShiftY`); other slots unchanged |
| Forward runs (`OffBallCandidateGenerator.AddForwardRun`) | wingers run at centre ± 10 m | wingers run at `RunWideLaneMetres` (5 m) from their touchline; ST, LCM, RCM unchanged |
| Support (`OffBallCandidateGenerator.AddSupport`) | the best of 8 spots 12 m around the carrier, anywhere on the pitch | for wide roles each spot is clamped into the role's channel before scoring: wingers within `WingerChannelMetres` (8 m) of their touchline, full-backs within `FullBackChannelMetres` (12 m) |
| Carries (`CarryResolver.ChooseDestination`) | forward, steered away from the nearest opponent, pulled towards the middle; bends towards goal in the final third | for a wide-role carrier **outside the final third**, no pull towards the middle (the existing 3 m touchline clamp keeps the carry on the pitch); everything else unchanged, including the bend towards goal in the final third |

The out-of-possession shape, pressing, marking and the pass maths are unchanged.

Roles, not listed positions, decide (ADR-031): after a dismissal, whoever fills a wide role behaves as above; a
reduced team without wingers (4-3-1, 4-2-1, 4-1-1) still has wide full-backs.

## Code

- **New `Positioning/WideChannel.cs`** — a small pure static helper, team frame:
  - `IsWide(PlayingPosition role)` — LW, RW, LB, RB.
  - `TouchlineY(PitchGeometry pitch, PlayingPosition role)` — the Y of the role's touchline in team frame.
  - `Clamp(PitchGeometry pitch, PlayingPosition role, Vec2 local, double channelMetres)` — moves `local` in Y so it
    is at most `channelMetres` from the role's touchline; non-wide roles are returned unchanged.
- **`TeamShapeSystem`** — `WideAttackShiftY` = 0.1 next to the existing `AttackShiftY`.
- **`OffBallWeights`** — `RunWideLaneMetres` 5, `WingerChannelMetres` 8, `FullBackChannelMetres` 12, beside the
  existing run and support weights.
- **`OffBallCandidateGenerator`** — winger run lane from `RunWideLaneMetres`; support spots of wide roles through
  `WideChannel.Clamp` (a clamped spot that coincides with another direction's is simply scored again).
- **`CarryResolver.ChooseDestination`** — skip the centre bias for a wide-role carrier whose team-frame X is below
  `MatchState.Zones.FinalThirdX`.

Determinism: no new random draws; only arithmetic in team frame (no trigonometry); iteration orders unchanged.

## Testing (written first)

- `WideChannel`: `IsWide` for every role; `TouchlineY` and `Clamp` for each wide role; non-wide roles unchanged;
  Home and mirrored Away give mirrored results.
- Shape: with the ball on the left, the RW's in-possession target stays within 8 m of the right touchline (Home and
  Away), while a central midfielder still shifts as before.
- Forward run: a winger's run target is 5 m from their touchline.
- Support: a full-back's chosen support spot is within 12 m of their touchline; a central midfielder's is not
  constrained.
- Carry: in their own half, a winger's carry next to the touchline goes straight up the line (no inward Y component
  beyond opponent avoidance) while a central midfielder's keeps the centre bias; in the final third the winger's
  carry still bends towards goal.
- Existing full-match invariant, determinism and statistics tests stay green.

## Measuring

- `batch 1000` before and after (Release): all health bands pass; goals, shots and possession within their bands.
- The probe, re-run after: share of open play within 5 m of a touchline, wingers' mean distance from the line in
  possession, receptions by role and near the touchline, ball-out restarts by cause.

### Success criteria

- Batch HEALTHY.
- Wingers average **under 8 m** from their touchline when their team has the ball (today 13–16 m).
- Throw-ins from misplaced passes and tackles clearly up — roughly doubled is the expectation; this is a step towards
  ≈ 40, not the whole way.
- Full-backs' share of receptions is reported. If it is still ≈ 1 % each, a small pass-utility follow-up (favouring
  open wide receivers) is proposed, not included here.

## Delivery

Branch `feat/m2-7b-width`, one PR:

1. `feat(positioning): wide channel helper` — `WideChannel` and its tests; unused, fingerprints identical.
2. `feat(ai): wide players hold the touchline in possession` — the four behaviour changes and their tests;
   **fingerprints change** (ledger #11).
3. `docs: width (ADR-034)` — ADR-034, `MATCH_SIMULATION` (shape, off-ball, carrying, batch figures, limitations),
   `GLOSSARY` (wide channel), `CURRENT_STATE`, and a new **M2.7b match realism** section in `MILESTONE_2_PLAN.md`
   listing the four steps and ledger entry #11.

Batch figures before → after go in ADR-034 and the PR description.
