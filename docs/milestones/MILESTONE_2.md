# Milestone 2 — Match Flow, Tactical Understanding & Developer Match Viewer

> Revision 2 (2026-10-05). Supersedes the first draft ("Tactical Understanding & Developer Match Viewer").
> The main change: the simulation first learns **when the ball is in play and when play is stopped** — ball out of
> play, restarts, offside, fouls, cards, reduced-player matches, periods and stoppage time — and only then builds
> tactical analysis on top of football that flows like football.

## 1. Session Bootstrap — Read This First

This milestone begins from a completed Milestone 1.

Before proposing architecture or writing code, read in order:

```text
CLAUDE.md
docs/CURRENT_STATE.md
docs/GAME_DESIGN.md
docs/ARCHITECTURE.md
docs/MATCH_SIMULATION.md
docs/DECISIONS.md
docs/GLOSSARY.md
docs/milestones/MILESTONE_1.md
docs/milestones/MILESTONE_1_ACCEPTANCE.md
docs/milestones/MILESTONE_2.md
```

Then inspect:

```text
src/MonsterFootball.Simulation/
tests/MonsterFootball.Simulation.Tests/
src/MonsterFootball.DebugRunner/
godot/
```

Do not redesign functioning Milestone 1 architecture without a documented reason.

Before coding each subtask:

1. verify the branch is based on current `main`;
2. run the test suite;
3. run a deterministic fingerprint;
4. run at least one debug match;
5. review the tick order and event architecture affected by the subtask;
6. confirm the subtask's plan and ADRs have been reviewed.

---

# 2. Existing Foundation

Milestone 1 is complete (`MILESTONE_1_ACCEPTANCE.md`). Verified baseline on 2026-10-05, `main` @ `6e4e799`:

```text
format clean · Release build 0 warnings · 269/269 tests
fingerprint 1 → 0-2 9D4F9047…   2 → 1-1 47E86E9B…   3 → 0-0 E04BB0A7…
batch 1000 → HEALTHY (13/13 bands)
goals 2.91 · shots 27.8 · passes 1 667 @ 82 % · tackles 295 · possession changes 397
striker share of goals 82 % · outfield distance 15.3 km
```

The repository contains a deterministic headless 11v11 simulation on a fixed 0.25 s tick, 4-3-3 team shape in
team frame, passing with ball flight and interceptions, carrying, two-roll tackling, shooting and goalkeeping,
goals and kickoffs, stamina and fatigue, utility AI, structured `MatchEvent`s, project-owned `MatchRng`, statistics
derived from events, batch simulation with health bands, state invariants, multi-OS CI with cross-platform
fingerprints, and a Godot .NET project that builds but has never been opened.

The approved player attributes remain (ADR-002), all 1–10:

```text
Technique  Passing  Finishing  Defending  Pace  Power  Stamina  Awareness  Keeping
```

**Do not add new core attributes in this milestone** — no Aggression, Discipline, Heading or similar. Foul,
card, offside and set-piece behaviour must come from these attributes plus match context.

---

# 3. Long-Term Game Context

The final game is a stylised fantasy football management/deckbuilding game. **The squad is the deck.**
Matches run autonomously; the manager never controls players directly. At tactically important moments,
**Opportunity Windows** will slow time and let the manager choose whether to play a card from the team's Playbook.

```text
Watch → Recognise Opportunity → Choose whether to intervene → Play card or Continue → Watch consequence
```

Milestone 2 does **not** implement cards. It builds the two foundations cards will need:

1. football that stops and restarts according to recognisable laws, so that "moments" exist;
2. tactical understanding of open play, so that the game knows which moments matter.

---

# 4. Milestone Goal

Milestone 1 answered: **Can football happen?**

Milestone 2 must answer:

1. **Does the football flow like football?** — the ball goes out of play, players are caught offside, fouls are
   given, cards are shown, restarts happen, and the match clock runs through stoppages with stoppage time at the
   end of each period.
2. **Can the game understand what is happening?** — it derives tactical information from continuous state and
   recognises a small set of football Opportunities.
3. **Can a developer see it?** — a Godot developer viewer shows the match, its stoppages and its tactical
   interpretation, so a human can verify both.

The milestone succeeds when:

> A developer watching a match sees stoppages and restarts that follow the laws, and tactical Opportunities that a
> human watching the same match generally agrees are present.

---

# 5. Core Architectural Rules

The existing rule remains absolute:

> **The simulation owns game truth. Presentation only visualises it.**

Milestone 2 adds:

> **The laws of the game are simulation.** Out-of-play, offside, fouls, cards and restarts are decided by the
> simulation through `MatchRng` and resolvers, exactly like any football action.

> **Tactical analysis observes football state. It does not alter it.**

Opportunity detection has **zero effect on match outcomes**. Given identical teams, configuration, seed and
manager decisions, the football is identical whether analysis runs or not. Opportunity detection never consumes
`MatchRng`.

> **Analysis reads immutable frames.** *(Accepted 2026-10-05.)* After each tick, observable state is copied into an
> immutable `MatchFrame`. Tactical analysis, the viewer and debug tools consume frames (plus events), never the live
> `MatchState`. This makes observation purity structural rather than a convention, and gives the viewer the
> previous/next states it needs for interpolation.

> **Shared geometry is input-agnostic.** Some tactical concepts are needed by the laws themselves (e.g. "is this
> foul denying an obvious goal-scoring opportunity?", "is this tackle in a dangerous area?"). Such calculations are
> pure functions over positions and sides, callable from the simulation (with `MatchState` data) and from analysis
> (with `MatchFrame` data). Opportunities themselves are observer-only and are never read by the simulation.

---

# 6. Milestone 1 Carry-Forward Review

These are integration points, not M1 failures.

## 6.1 Shot timing

Shots are currently resolved **and applied** in the strike tick (ADR-015): the score changes before the ball has
moved. Change shots to the passing structure (ADR-012):

```text
ShotIntent → ShotResolver → ShotResult decided at the strike → ball travels → result applied on arrival
```

The outcome may be predetermined at the strike. Presentation never decides it.

## 6.2 Pass arrival consistency

Validate visually that receivers and interceptors reach plausible locations and the ball does not snap between its
landing point and the player. Redesign passing only if the viewer demonstrates a real problem.

## 6.3 Possession semantics

`PossessionChanged` is emitted for every change of team, including the post-goal kickoff and the keeper taking the
ball after a miss; statistics count each as a possession "won". With throw-ins, goal kicks, corners and free kicks
this distinction matters much more.

Possession changes must carry their **cause** (e.g. interception, tackle, loose-ball recovery, save, or a restart
awarded by the referee), and a "possession won" statistic must count only possession won in open play.

## 6.4 Ball in flight at the end of a period

Superseded by §9: a period ends only at a stoppage, and a ball in flight is not a stoppage, so a decided shot
always arrives. Document the behaviour and test it.

## 6.5 Danger-region logic

`TackleSystem` treats "within Length/3 of own goal centre" (a 35 m radius) as the defending third. Replace this and
similar approximations with the tactical-zone model (§21–§24).

---

# PART A — MATCH FLOW AND LAWS

# 7. Ball In Play vs Play Stopped

The central new domain concept is that **the ball is either in play or play is stopped**.

```text
                  ┌──────────────────────────────────────────────┐
                  ▼                                              │
  ┌───────────┐  ball out / foul / offside / goal /    ┌──────────────┐
  │ OPEN PLAY │ ─ period end ──────────────────────────▶│   STOPPED    │
  └───────────┘                                        │ (dead ball)  │
        ▲                                              └──────┬───────┘
        │                                                     │ referee decision recorded
        │                                                     ▼
        │                                              ┌──────────────┐
        │                                              │RESTART SETUP │ players take restart positions,
        │                                              └──────┬───────┘ minimum dead-ball time elapses
        │                                                     │ ready
        │            restart taken                     ┌──────▼───────┐
        └──────────────────────────────────────────────│   RESTART    │ taker plays the ball
                                                       └──────────────┘

  Any state ──(final period over at a stoppage)──▶ FINISHED
```

Current `MatchPhase` (`Kickoff`, `OpenPlay`, `GoalRestart`, `Finished`) becomes a special case of this model:
a goal is a stoppage whose restart is a kickoff; the opening kickoff is a restart with no preceding open play.
Exact enum/class shapes are decided in implementation planning; the domain concept above is fixed.

## 7.1 Stoppage record

Every stoppage is represented by one immutable record holding the context that caused it and how play resumes:

| Field | Meaning |
|---|---|
| Reason | Goal, BallOutOfPlay, Foul, Offside, PeriodEnd, (start of match / period) |
| Restart type | Kickoff, ThrowIn, GoalKick, Corner, DirectFreeKick, IndirectFreeKick, Penalty |
| Awarded team | team that takes the restart |
| Restart location | continuous position (centre spot, touchline point, goal area, corner arc, foul spot, penalty spot) |
| Offending player | where relevant (foul, offside) |
| Fouled player | where relevant |
| Disciplinary outcome | none / yellow / second yellow / red, where relevant |
| Stopped at | tick play stopped |

Do not add fields without a current consumer.

## 7.2 Restart types

All seven are supported in M2:

```text
Kickoff · Throw-in · Goal kick · Corner · Direct free kick · Indirect free kick · Penalty
```

Set-piece execution is **deliberately simple and automated** (§11). No routines, no set-piece cards.

## 7.3 During a stoppage

- No on-ball decisions, tackles, interceptions or loose-ball collection.
- Players move to restart positions (§11.1) at a sensible intensity; movement still drains stamina normally.
- The restart is taken when the taker is in place, required distances are respected and the **minimum dead-ball
  time** for that restart type has elapsed (initial, tunable values in configuration, e.g. throw-in ~8 s,
  goal kick ~15 s, corner ~20 s, free kick ~20 s, penalty ~45 s, goal ~45 s).
- Tactical Opportunities do not exist while play is stopped (§30).

---

# 8. Match Clock, Periods, Ball-in-Play Time and Stoppage Time

## 8.1 Two clocks, one authority

The tick counter remains the single authority. Two derived measures are distinguished:

| Measure | Meaning |
|---|---|
| **Match time** | elapsed time on the referee's clock; runs through stoppages |
| **Ball-in-play time** | time during which the ball was in play; derived from stoppage events (ADR-019 style) |

Milestone 1 effectively simulated ~90 minutes of continuous ball-in-play, which is a large part of why passes,
tackles, possession changes and running distance are high. **Do not tune those volumes down before measuring how
real stoppages change them.** Real football has roughly 55–60 minutes of ball-in-play time per match.

## 8.2 Periods

The match is played in **two periods of 45 minutes** with a half-time interval. The second half starts with a
kickoff by the team that did not take the first. *(Change of ends at half-time: see Open Decision D1.)*

## 8.3 Stoppage time

- Each period has **added time**, accumulated from time lost to significant stoppages (goals, cards, penalties,
  dismissals). Allowances are configuration data. Routine restarts (throw-ins, goal kicks) do not add time.
- Added time is fixed (rounded up to whole minutes) when the regular period time expires, and emitted as an event.
- **A period ends only at a stoppage.** Once regular time plus added time has expired, the period ends at the next
  moment play stops (ball out of play, foul, offside, goal). Consequences:
  - a ball in flight is never cut off — a decided shot always arrives;
  - if the stoppage is a **penalty**, the penalty is taken before the period ends (the laws extend time for it);
  - if the stoppage is a goal, the goal stands and the period ends without the kickoff.
- A safety cap prevents an endless period if play never stops (initial: 3 minutes beyond added time, then the
  period ends at the next tick with no ball in flight). The cap should essentially never trigger once the ball can
  leave play; telemetry reports how often it does.

## 8.4 Determinism

Match length in ticks is no longer a constant. It remains fully determined by teams, configuration and seed.
Tests and tools that assume exactly 21 600 ticks or `MatchFinished` at 90:00 must be updated deliberately.

---

# 9. Ball Leaving Play

## 9.1 Laws (simplified)

The ball is out of play when it **wholly crosses** a touchline or goal line (or a goal is scored). The restart is
decided by **where it crossed** and **which team last touched it**:

| Crossed | Last touched by | Restart |
|---|---|---|
| Touchline | either team | Throw-in to the other team, at the crossing point |
| Goal line (not a goal) | attacking team | Goal kick to the defending team |
| Goal line (not a goal) | defending team | Corner to the attacking team, nearest corner |

Track the **last touch** explicitly (passer, receiver, interceptor, tackler, carrier, keeper, deflecting player).

## 9.2 How the ball can leave play

Today the ball cannot leave the pitch: misplaced passes, tackle loose balls and missed shots are all clamped to the
pitch, missed shots go straight to the keeper, and a loose ball never moves. M2 must remove these shortcuts so the
ball can genuinely go out:

- misplaced passes and long balls can land or run beyond a line;
- missed shots go wide or over → goal kick (or corner if deflected);
- tackle loose balls can be knocked out;
- goalkeeper saves can be **held** or **parried**, including behind for a corner *(Open Decision D5)*;
- loose balls may need simple deterministic rolling so a ball that lands near a line can run out
  *(Open Decision D4)*.

Do not add a ball-physics engine. Trajectories remain simple deterministic segments; crossing points come from
segment–boundary intersection.

---

# 10. Offside

Offside must exist **before** `ThroughBallLane` Opportunities are relied on: an excellent-looking run may be illegal.

## 10.1 Offside position

Judged **at the moment a teammate plays the ball**. A player is in an offside position if all are true:

- in the opponents' half (the halfway line itself is not);
- nearer the opponents' goal line than the ball;
- nearer the opponents' goal line than the **second-last opponent** (normally the last outfield defender, the
  keeper being last). Level is onside.

## 10.2 Offence

A player in an offside position is penalised only when they **become involved in play** — in M2's simplified
model, when they receive or are the first to play the ball from that pass (including a misplaced pass or a loose
ball they collect before anyone else touches it). An opponent touching the ball first resets the judgement.

The design must allow later refinement (interfering with an opponent, passive offside, deliberate play vs
deflection) without restructuring: keep "who was offside at the pass" and "who became involved" as separate steps.

## 10.3 Exceptions

No offside offence directly from a **throw-in, goal kick or corner**.

## 10.4 Restart

Indirect free kick to the defending team at the offender's position when involved.

## 10.5 AI consequence

The AI must become offside-aware, or the change will flood matches with offsides:

- forward runs currently target **1 m beyond** the opponents' last outfield line, and attacking shape caps at the
  last *outfield* line — both must use the second-last opponent and **time** runs relative to the pass;
- pass selection must account for receivers in offside positions. The referee is perfect; misjudgement, if any,
  comes from the passer's Awareness *(Open Decision D8)*.

---

# 11. Restart Execution

## 11.1 Positioning

During restart setup players take simple, law-respecting positions, derived from team shape around the restart
location:

| Restart | Taker | Constraints |
|---|---|---|
| Kickoff | a central forward/midfielder of the awarded team | everyone in own half; opponents ≥ 9.15 m from the ball |
| Throw-in | a nearby fullback/winger | thrower at the crossing point on the touchline; opponents ≥ 2 m |
| Goal kick | goalkeeper | ball in the goal area; opponents outside the penalty area until it is played |
| Corner | a designated good passer | ball in the corner arc; opponents ≥ 9.15 m; attackers and defenders in the box |
| Free kick | a good passer (or shooter, see §13) | opponents ≥ 9.15 m; optional wall for direct kicks in range |
| Penalty | best finisher | keeper on the line; everyone else outside the area and arc |

Taker choice must be deterministic and must handle missing players (§15). No player-specific set-piece roles.

## 11.2 Execution

Restarts reuse existing actions wherever possible:

- throw-in → a pass with throw-specific range and accuracy (from Passing/Power/Technique);
- goal kick → keeper distribution (pass), short or long;
- corner → a delivery into the box; a receiving attacker may attempt an immediate shot or header using the existing
  shot model;
- free kicks and penalties → §13.

Keep execution simple; detailed routines belong to later milestones.

---

# 12. Fouls

## 12.1 Scope

A **physical challenge** — in M2, a tackle — can be legal or a foul. No other foul types are required (no handball,
no off-ball fouls, no simulation/diving).

## 12.2 Resolution

Extend the tackle resolution with a foul outcome. Probability and severity use existing attributes and context, for
example:

- tackle quality and outcome (a challenge that misses the ball is more likely to be a foul);
- the tackler's Defending (reduces fouls), Power (raises severity);
- relative movement and speed (sprinting into a challenge, challenging from behind);
- tiredness (fatigued tacklers foul more);
- location and tactical danger (shared geometry, §5).

Do not build a detailed referee simulation. The referee is consistent and perfect within the simplified laws.

## 12.3 Consequences

A foul stops play and awards:

- a **direct free kick** at the foul location, or
- a **penalty** when the defending team fouls inside its own penalty area.

**Advantage** is not required in M2 *(Open Decision D7)*.

---

# 13. Free Kicks and Penalties

Enough behaviour for match continuity, no routines:

- **Direct free kick** — within shooting range and angle the taker may shoot (existing shot model with a wall
  penalty); otherwise pass.
- **Indirect free kick** (offside in M2) — must be played to a teammate; cannot score directly.
- **Penalty** — a dedicated shot resolution from the spot (Finishing/Technique/Power vs Keeping/Awareness), outcomes
  goal, saved (held/parried) or missed. No retakes, no encroachment.

---

# 14. Discipline

Disciplinary state persists for the whole match:

| Outcome | Trigger (simplified) |
|---|---|
| Yellow card | reckless foul (severity), or stopping a promising attack |
| Second yellow | a second caution → **dismissal** |
| Straight red | serious foul play (high severity), or denying an obvious goal-scoring opportunity (DOGSO) outside the area *(inside the area: penalty + yellow when the challenge is a genuine attempt to play the ball, per the modern laws — Open Decision D10)* |

DOGSO and "promising attack" use shared geometry (distance and direction to goal, defenders goal-side, control of
the ball) — the same football concepts the Opportunity detectors use, implemented once.

No Aggression or Discipline attribute.

**A red card must remove the player from active football** — not merely emit an event.

---

# 15. Reduced-Player Matches

The simulation currently assumes 22 active players and a complete 4-3-3 everywhere (invariants,
`MatchState.ActivePlayerCount`, kickoff taker = the ST, opposing keeper = the GK slot, slot-anchored shapes).

M2 must make a dismissal produce a valid **10 v 11** (or fewer) match:

- dismissed players leave the pitch and are excluded from every system (shape, pressing, marking, passing options,
  offside line, tackling, loose balls, restart takers, statistics where relevant);
- the team shape **degrades sensibly and deterministically** with a missing slot *(approach: Open Decision D11)*;
- if a **goalkeeper** is dismissed, an outfield player takes over in goal (no substitutions); their Keeping
  attribute now matters *(D11)*;
- invariants are rewritten: per side 7–11 active players, exactly one active goalkeeper per side, dismissed
  players never possess the ball and are off the pitch;
- if a team would fall below 7 players the match is **abandoned** per the laws *(Open Decision D12)*;
- Tactical Snapshots and Opportunities understand numerical imbalance caused by dismissals.

No substitutions.

---

# 16. Events, Possession and Statistics

## 16.1 Events

New football events describe stoppages, restarts, fouls, offsides, cards, dismissals, periods and added time.
Prefer **generic events with structured context** (e.g. one "play stopped" event carrying the stoppage record, one
"restart taken" event carrying the restart type) over one event type per law, unless a consumer needs a dedicated
type. New `MatchEventType` values are appended (ADR-019 consumers depend on order-stable canonical strings).

## 16.2 Possession

`PossessionChanged` carries its cause (§6.3). Possession time is credited during **ball-in-play time** only.

## 16.3 Statistics

Add, derived from events: fouls, yellow cards, red cards, offsides, corners, throw-ins, goal kicks, free kicks,
penalties (awarded/scored), ball-in-play time, added time per period.

## 16.4 Health bands

Extend `BatchHealth` with deliberately broad bands, for example (initial, to be confirmed by the first batch):

```text
ball-in-play minutes 45–75 · throw-ins 15–70 · corners 3–20 · goal kicks 8–35 · offsides 1–15
fouls 8–45 · yellow cards 0.5–8 · red cards ≤ 0.6 per match · penalties ≤ 0.8 per match
added time per period 0–10 min · safety cap triggered ≤ 1 % of periods · abandoned matches ≤ 0.1 %
```

Existing bands stay. If stoppages legitimately move an existing figure outside its band, revisit the band
deliberately with the reason recorded (ADR-020).

---

# PART B — TACTICAL UNDERSTANDING

# 17. Tactical Coordinate System

Positions stay continuous; never replace them with grid cells. Classify positions into derived tactical regions,
in the existing **team frame** (own goal at X = 0, attacking +X, left flank +Y), avoiding duplicated Home/Away logic.

# 18. Primary Tactical Grid

**4 longitudinal bands × 5 vertical channels = 20 primary tactical zones.**

Bands, from the analysing team's perspective: `Defensive`, `OwnMidfield`, `OppositionMidfield`, `Attacking`.
Channels: `LeftWide`, `LeftHalfSpace`, `Centre`, `RightHalfSpace`, `RightWide`.

```text
                      ATTACKING DIRECTION →
        LEFT WIDE   LEFT HALF   CENTRE   RIGHT HALF   RIGHT WIDE
       ┌─────────┬───────────┬────────┬────────────┬──────────┐
       │         │           │ ATTACK │            │          │
       ├─────────┼───────────┼────────┼────────────┼──────────┤
       │         │           │ OPP MID│            │          │
       ├─────────┼───────────┼────────┼────────────┼──────────┤
       │         │           │ OWN MID│            │          │
       ├─────────┼───────────┼────────┼────────────┼──────────┤
       │         │           │ DEFENCE│            │          │
       └─────────┴───────────┴────────┴────────────┴──────────┘
```

# 19. Zone Representation

Composition, not a 20-value enum: `TacticalZone = (LongitudinalBand, VerticalChannel)`. The same physical position
has different interpretations for the two teams.

# 20. Zone Boundaries

Initial bands: equal quarters of pitch length. Initial channels: five configurable bands (equal fifths acceptable).
Thresholds live in one place so visual validation can adjust them.

# 21. Overlapping Tactical Areas

Additional derived tags that may overlap each other and the grid: `OwnHalf`, `OppositionHalf`, `FinalThird`,
`PenaltyArea`, `SixYardArea` (goal area), `CentralPenaltyArea`, `WidePenaltyArea`. Do not create mutually exclusive
types for overlapping concepts.

The laws use these too: penalty area (penalties, goal kicks, keeper handling), goal area (goal kicks), halves
(offside, kickoff), final third (tackle danger, §6.5).

# 22. Tactical Geometry

Reusable, deterministic, read-only, input-agnostic (§5):

```text
zone classification · distance to goal · distance to touchline · nearest opponent · nearest teammate
pressure on ball · local numerical balance · team width · team depth · defensive / midfield / forward lines
second-last opponent (offside line) · compactness · space ahead · passing-lane clearance · goal-side relationships
```

All must handle **reduced teams**. Geometry never decides actions by itself; where the laws or AI use it, the
decision stays in the resolver or system that calls it.

# 23. MatchFrame

An immutable copy of observable state at the end of a tick: tick, match time, period, ball-in-play flag and current
stoppage record, score, ball (position, possessor, flight), and per player: identity, team, slot, position,
movement target, intensity, stamina, off-ball plan kind/target, disciplinary state, active/dismissed.

Rules:

- contains only what analysis or the viewer actually reads;
- analysis must not read predetermined outcomes of balls in flight (intended receiver and lane only);
- the viewer may read the flight's landing point to animate it.

# 24. Tactical Snapshot

Immutable derived interpretation of one frame:

```text
Tick · Time · BallInPlay · AttackingTeam · PossessionPlayer · BallPosition · BallZone (per team)
HomeTeamAnalysis · AwayTeamAnalysis (lines, width, depth, compactness, active players)
RecentPossessionChangeAge · PressureOnBall · LocalNumbers · PassingLanes · PlayPhase
```

`MatchState` = authoritative truth. `MatchFrame` = its immutable copy. `TacticalSnapshot` = interpretation.
Exact schema may change after implementation review.

# 25. Snapshot Determinism

The same frame always produces an identical snapshot: no random numbers, no wall-clock, no rendering dependency,
stable iteration order. Add tests.

# 26. Play Phase

A derived football-state classifier, separate from the technical match flow (§7), for open play only:

```text
EstablishedPossession · Progression · AttackingTransition · DefensiveTransition · FinalThirdAttack · DangerousAttack
```

Answering: settled or transition? recently changed possession? opponent still recovering? in the dangerous area?
Do not over-model. Names may change if implementation reveals better terminology (e.g. deriving
DefensiveTransition as the opponent's view of AttackingTransition). While play is stopped the classifier reports
no open-play phase.

# 27. Opportunity Architecture

```text
MatchFrame (+ recent MatchEvents + small previous Opportunity state)
    → TacticalSnapshotFactory → TacticalSnapshot → OpportunityDetectors → OpportunityTracker → OpportunityObservation
```

No dependency on Godot. Driven from outside `MatchSimulation`.

# 28. Opportunity Data

Enough for debugging, presentation, future card validation, future AI-manager evaluation and telemetry:

```text
Id · Type · Team · StartTick · LastValidTick · PrimaryPlayerId · SecondaryPlayerIds · OpponentPlayerIds
Position · Zone · Confidence · Danger · State (Active / Ended + reason)
```

Do not add fields merely because they might be useful later.

# 29. Opportunity Lifecycle

An Opportunity is an **episode**, not a per-tick event: stable IDs, creation, continuation, update, expiration,
cooldown/debouncing. The same tactical condition persisting must not produce duplicates.

# 30. Opportunities and Stoppages

- Open-play Opportunities exist only while the ball is in play.
- When play stops, every active Opportunity ends (reason: stoppage).
- No open-play Opportunity is created during a stoppage or restart setup.
- Restart contexts may later create **set-piece Opportunities**; that is an extension point, not M2 scope.

# 31. Opportunity Observation Stream

Kept separate from the authoritative `MatchEvent` stream. MatchEvents describe football; Opportunities describe
interpretation. Presentation may show both together.

# 32. The Six Opportunity Types

Exactly these six unless a design change is accepted:

```text
Transition · WideIsolation · ThroughBallLane · Overload · FinalThirdEntry · DefensiveEmergency
```

**Transition** — shortly after an open-play possession change (not a restart), the new attacking team can exploit
an unsettled opponent: space ahead, players ahead of the ball, few defenders goal-side. Not merely because
possession changed. Ends when the opponent recovers, the attack slows or is recycled, possession changes, play stops,
or a maximum duration passes.

**WideIsolation** — a wide attacker who has, or is about to receive, the ball, one-on-one with a defender, the
nearest supporting defender meaningfully farther away, usable space ahead or inside. *"The winger has the fullback
one-on-one."* Dribbling not required.

**ThroughBallLane** — the carrier has an advanced teammate threatening space behind or between defenders, a
plausible forward corridor, and **the run is onside** (§10). Means an opportunity to attempt one, not a guarantee.
Reuse existing pass-lane geometry.

**Overload** — meaningful local numerical superiority (2v1, 3v2) in a tactically relevant area near the ball,
counting only connected attackers; not arbitrary players in a huge radius. Includes superiority created by a
dismissal.

**FinalThirdEntry** — controlled progression into the attacking area with support and forward options; not merely
crossing an X coordinate. *"The attack has established a dangerous platform."*

**DefensiveEmergency** — the defending team is acutely exposed: a run behind the line, central penetration, local
defensive disadvantage, a dangerous carrier approaching goal with insufficient cover. Rare and clearly dangerous;
will later support defensive Reaction cards.

# 33. Confidence and Danger

Normalised descriptive outputs, not attributes: **Confidence** — how strongly the state matches the definition;
**Danger** — how threatening the situation is to the defending team. Do not overfit; they exist for debugging,
sorting, future UI and future AI-manager decisions.

# 34. Opportunity Telemetry

Batch diagnostics report: opportunities per match, by type, average and median duration by type, by start zone, by
team; and where practical, Opportunity → shot / goal / possession loss within N seconds (correlation, not
causation).

# 35. Opportunity Health and Frequency

Detect pathologies with broad bands: zero opportunities in most matches; hundreds per match; the same opportunity
recreated every tick; one type dominating; opportunities lasting minutes; DefensiveEmergency active for large parts
of a match; any opportunity active during a stoppage.

The eventual game expects roughly **20–30 manager intervention moments per match**. M2 raw counts need not match
that (several detections may compete for one window), but typical equal-team matches should produce **tens, not
hundreds**. Tune with telemetry *and* human review.

---

# PART C — DEVELOPER MATCH VIEWER

# 36. Viewer

Turn the Godot project into a developer visualiser — an engineering tool, not final presentation. Simple shapes.

Required: pitch and markings, all players (active and dismissed distinguished), ball, score, match clock with
period and added time, team distinction.

The viewer consumes frames from the simulation. **Godot physics never affects football.**

# 37. Playback Controls

Play, pause, speeds 0.25× / 0.5× / 1× / 2× / 4× (approximately), restart same seed, run another seed, and jump to a
moment (e.g. a listed stoppage or Opportunity). No replay editing, no cinematic cameras.

# 38. Stoppages and Discipline in the Viewer

At minimum the developer can see:

- whether the ball is in play;
- why play stopped (and the offending/fouled players where relevant);
- the restart type, awarded team and restart location;
- the offside line at the moment of an offside call;
- yellow and red cards, and dismissed players;
- added time and the end of each period.

# 39. Tactical Overlays

Toggleable, not all on by default: 20 zones, movement targets, team shape anchors, defensive / midfield / forward
lines, offside line, pressure around the ball, passing lanes, local numerical balance, active Opportunities.

# 40. Opportunity Visualisation

For an active Opportunity show type, teams, primary player, relevant teammates and opponents, zone, confidence,
danger and duration, and highlight the players and space on the pitch:

```text
OPPORTUNITY  Wide Isolation
Blue RW #11  vs  Red LB #3
Zone: Attacking / RightWide
Confidence 0.86 · Danger 0.63 · Active 2.25 s
```

# 41. Presentation Follows Simulation Time

The viewer interpolates between authoritative frames. **Rendering time never advances or resolves football.**
Non-visual playback logic (clock, speed, frame history, interpolation) lives in plain, tested .NET code; Godot
only draws.

# 42. No Final Art

No monster models, stadium, final UI, broadcast cameras, detailed animation, crowds, particles or polished effects.

---

# PART D — VALIDATION AND DELIVERY

# 43. Human Validation Protocol

Automated tests cannot prove that football looks right or that an Opportunity is meaningful.

**Laws:** for each restart type, offside and fouls/cards, review examples across several seeds in the viewer and
confirm the decision and restart are correct per the simplified laws. Record errors and fix them.

**Opportunities:** for each type —

1. collect examples across several deterministic seeds;
2. watch them in the viewer;
3. inspect what caused the detector to fire;
4. classify: clearly correct / arguable / clearly incorrect;
5. record recurring false positives;
6. adjust rules; repeat.

Inspect at least **20 examples per type** where practical. Target: **≥ ~80 %** clearly defensible, without making
a detector so restrictive it almost never fires. Every reviewed example is recorded with seed and tick so it is
reproducible.

# 44. Regression Requirements

All Milestone 1 guarantees remain: determinism by seed, simulation/presentation separation, centralised
`MatchRng`, headless execution, cross-platform fingerprints, invariants, automated tests, batch simulation, format
and architecture checks.

**Part A deliberately changes every seeded match.** Each fingerprint change must:

1. come from one identified PR with one explained cause;
2. be verified as intended (tests, batch before/after recorded);
3. be re-verified cross-platform;
4. be recorded in the relevant ADR.

Never regenerate baselines casually to make tests pass. Do not weaken M1 tests merely to accommodate new behaviour;
tests whose premise changes (e.g. "a match is exactly 21 600 ticks") are rewritten to the new rule, with the reason
in the PR.

# 45. Tests

**Match flow**: state transitions; stoppage records complete; the clock runs through stoppages; ball-in-play time
excludes them; added time computed from allowances; a period ends only at a stoppage; a penalty awarded after time
is taken; a shot in flight at the end of time arrives; safety cap.

**Out of play**: crossing detection on each line; last-touch attribution; throw-in vs goal kick vs corner decided
correctly from crossing and last touch; restart location correct.

**Offside**: offside position per §10.1 (own half, level, ahead of ball, second-last opponent incl. keeper);
involvement required; reset by opponent touch; no offside from throw-in/goal kick/corner; indirect free kick at the
offender's position; Home/Away symmetry.

**Fouls/free kicks/penalties**: foul probability responds to the specified inputs in the right direction (fixed
seeds, many trials); foul in own area → penalty, elsewhere → direct free kick; distances respected; indirect free
kick cannot score directly; penalty outcomes.

**Discipline/reduced teams**: yellow, second yellow → red, straight red; dismissed player removed from every system;
valid 10v11 shapes; keeper dismissal → outfield keeper; invariants on reduced teams; abandonment; full matches
with forced dismissals run cleanly under `CheckedRun`.

**Tactical**: all 20 zones reachable; Home/Away mirror; deterministic boundaries; no invalid zones; lines and
symmetry; pressure (near increases, distant does not, multiple increase); numerical balance 1v1, 2v1, 3v2, 2v3 and
with a dismissed player; snapshot determinism and no mutation.

**Each Opportunity detector**: clear positive, clear negative, boundary, expiry, no duplicate per tick, team-frame
symmetry, **ends on stoppage**.

# 46. Observation Must Not Affect Football

Critical acceptance test:

```text
Match A: run normally.
Match B: run identically while capturing frames and generating snapshots and Opportunities every tick.
Assert: identical football events, score, final state and RNG draw sequence.
```

# 47. Known M1 Calibration Issues

```text
Strikers score ~82 % of goals.
~1 670 passes and ~295 tackles per match.
~15.3 km per outfield player.
Shot volume is sensitive to ShotBase.
Ball does not leave play.          ← addressed by this milestone
```

Stoppages will change most of these. **Measure first, tune second.** A calibration checkpoint follows the laws work
(§51): re-run the batch, record the new figures, set the new health bands, and tune only where the cause is clear.
No blind tuning sweeps.

# 48. Explicit Non-Goals

```text
Playbook · cards · decks · hands · draw/discard · Command resource · Prepared Plays
manager intervention UI · Opportunity slowdown · opponent manager cards · manager AI
sophisticated set-piece routines · set-piece cards · set-piece Opportunities (extension point only)
referee personalities · referee errors · VAR · advantage (unless D7 accepted)
handball · off-ball fouls · simulation/diving · injuries · substitutions
extra time (30-minute knockout periods) · penalty shoot-outs
fantasy species mechanics · flight · anatomy · species traits
player personality · traits · development · new core attributes
transfers · contracts · campaign · narrative · league simulation
final models · animation · stadium · crowds · commentary · audio · multiplayer
```

Do not implement M3 systems early because an extension point becomes obvious. Document it.

# 49. Suggested Repository Additions

Exact structure follows architecture review.

```text
src/MonsterFootball.Simulation/
    Core/            (match flow state, periods, stoppage record)
    Laws/            OutOfPlay, Offside, Fouls, Discipline
    Restarts/        restart positioning, takers, execution per type
    Observation/     MatchFrame, PlayerFrame, BallFrame
    Tactical/        zones, areas, boundaries, shared geometry, snapshot, play phase, observer
        Opportunities/   types, opportunity, tracker, observation, detectors
    Diagnostics/     opportunity and match-flow summaries, health bands

src/MonsterFootball.Playback/   (plain .NET: playback clock, frame history, interpolation; no Godot)

godot/
    MatchViewer/  DebugOverlays/  OpportunityOverlay/
```

Do not create abstractions before implementation requires them.

# 50. Documentation

Update `ARCHITECTURE.md`, `MATCH_SIMULATION.md`, `GAME_DESIGN.md`, `GLOSSARY.md`, `CURRENT_STATE.md`,
`DECISIONS.md`, `README.md`.

Add:

- `docs/LAWS_OF_THE_GAME.md` — the simplified laws as implemented: match flow, periods, stoppage time, out of play,
  restarts, offside, fouls, free kicks, penalties, discipline, reduced teams, known simplifications;
- `docs/TACTICAL_ANALYSIS.md` — team-frame coordinates, 20 zones, overlapping areas, frames and snapshots, pressure,
  lines, numerical balance, play phase, Opportunity definitions, lifecycle, detector limitations, telemetry.

Documentation never drifts from implementation.

# 51. Implementation Sequence

Small, reviewable PRs. Each step may be several PRs; fingerprint-changing work is isolated per PR.

| Step | Scope |
|---|---|
| **M2.1 — Baseline and match-flow architecture** | Baseline review; stoppage/restart model with kickoff and goal as its first users; periods, half-time, ball-in-play time, added time, end-at-stoppage; possession-change causes; shot flight (§6.1); Godot 4.7.2 opened; ADRs. |
| **M2.2 — Frames and developer viewer (basic)** | `MatchFrame`; `Playback` library; Godot viewer with pitch, players, ball, score, clock/period, in-play/stopped banner, playback controls, seed selection, interpolation. |
| **M2.3 — 20-zone tactical model** | Bands, channels, zones, overlapping areas, boundaries, shared geometry basics; replace the tackle danger radius (§6.5). |
| **M2.4 — Ball out of play and restarts** | Last touch; crossing detection; removal of pitch clamps; loose-ball rolling and keeper parries per D4/D5; throw-ins, goal kicks, corners. |
| **M2.5 — Offside** | Offside position at the pass, involvement, exceptions, indirect free kick; offside-aware runs, shape and passing. |
| **M2.6 — Fouls, free kicks, penalties** | Foul outcome in tackles; direct/indirect free kicks; penalties. |
| **M2.7 — Discipline, reduced teams, calibration checkpoint** | Cards, dismissals, reduced-player shapes, keeper replacement, abandonment, invariants; then the football calibration checkpoint and new health bands (§47). |
| **M2.8 — Tactical geometry and Tactical Snapshot** | Full shared geometry (dismissal-aware), snapshot, play phase, determinism and no-mutation tests. |
| **M2.9 — Opportunity framework** | Data model, ids, tracker, lifecycle, stoppage handling, observation stream, telemetry hooks, **observation-purity test**. |
| **M2.10 — Six detectors and telemetry** | Transition, WideIsolation, DefensiveEmergency, then ThroughBallLane, Overload, FinalThirdEntry; `DebugRunner` Opportunity batch, health bands, opportunity fingerprint in CI. |
| **M2.11 — Viewer: stoppages and tactical overlays** | Stoppage/restart/card detail, offside line, all tactical overlays, Opportunity panel, highlights, jump-to. |
| **M2.12 — Human validation, regression, acceptance** | Laws and Opportunity review, tuning, full regression, docs, ADRs, `MILESTONE_2_ACCEPTANCE.md`, progress report. |

**Why this differs from a strict laws-first order:**

- **Viewer at M2.2, not at the end** — out-of-play crossing points, restart positions, offside lines and foul
  locations are spatial and hard to verify from text logs. Laws and detectors are built *with* the viewer instead of
  being tuned twice. M2.11 adds the detailed overlays.
- **Zones at M2.3, before the laws** — zone classification is pure geometry with no dependencies, and the laws need
  it: penalty and goal areas, halves for offside, final third for tackle danger, danger context for fouls and DOGSO.
- **Ball out of play and its three restarts in one step** — the ball cannot leave play without a restart to resume
  it; separating them would require a throwaway interim restart. They remain separate PRs inside M2.4.
- **Calibration checkpoint at the end of M2.7** — tactical thresholds should be tuned on football that already has
  stoppages, not on M1's continuous play.

# 52. Acceptance Criteria

## Match flow
- Ball-in-play and play-stopped states exist with complete stoppage records.
- Two periods with half-time; added time per period; periods end only at a stoppage; penalty exception; safety cap.
- Ball-in-play time is measured and reported.

## Laws
- The ball leaves play; throw-ins, goal kicks and corners are awarded correctly from crossing point and last touch.
- Offside is judged at the pass, penalised on involvement, with the three restart exceptions.
- Tackles can be fouls; free kicks (direct/indirect) and penalties are awarded and taken.
- Yellow, second-yellow and straight-red cards work; dismissed players leave active football.
- Reduced-player matches are valid, deterministic and pass invariants; keeper dismissal and abandonment handled.

## Tactical zones
- 20 primary zones from 4 bands × 5 channels, in team frame, with Home/Away symmetry tested; continuous coordinates
  remain authoritative; overlapping areas exist.

## Tactical interpretation
- Zone, pressure, local numerical balance, lines, width/depth, compactness and passing-lane information, all
  dismissal-aware.

## Tactical Snapshot
- Immutable, deterministic, derived from frames, never mutates the match.

## Opportunities
- All six exist; each creates, persists, expires, does not duplicate per tick, ends on stoppage, exposes useful
  context and has automated tests.

## Observation purity
- Analysis on vs off produces identical score, football events, RNG behaviour and final state.

## Telemetry and health
- Match-flow statistics and Opportunity telemetry are reported; all health bands pass on a 1 000-match batch.

## Viewer
- A developer can watch a full match with basic playback control and see stoppages, restarts, cards, dismissals,
  offside lines and toggleable tactical overlays including Opportunities.

## Human validation
- Laws and every Opportunity type reviewed on reproducible samples; most detections defensible; recurring false
  positives fixed or documented.

## Regression
- All M1 architectural guarantees intact; every fingerprint change explained and verified cross-platform.

# 53. Definition of Success

M2 does not succeed merely because the ball can go out or coordinates have tactical names.

It succeeds when a developer can watch a match and think:

> *"That was offside — and the flag was right."*
> *"Yes, that tackle from behind deserved the yellow."*
> *"They're down to ten, and you can see the overload on the left."*
> *"Transition detected — yes, they just won it and the opponent is exposed."*
> *"Through Ball Lane — yes, there's an onside run and a channel to exploit."*

That flowing, law-respecting, tactically understood football is the foundation for Milestone 3.

# 54. Open Design Decisions

To be resolved before or during the relevant step's planning. Recommendations are proposals until approved.

| # | Decision | Recommendation |
|---|---|---|
| D1 | Change of ends at half-time | Two periods yes; **defer change of ends** — no gameplay effect in the simulation, and ~8 files assume a side's attack direction is fixed. Revisit with final presentation. |
| D2 | Safety cap length when play never stops | 3 minutes beyond added time; report how often it triggers. |
| D3 | Added-time allowances | Goal 45 s, card 20 s, penalty 45 s, dismissal 30 s; rounded up to whole minutes. |
| D4 | Loose-ball rolling | Simple deterministic roll-on after misplaced passes and tackle loose balls (direction, decaying speed), so balls near a line can go out. Needed for plausible throw-in counts. |
| D5 | Source of corners | Keeper saves split into held / parried (loose rebound or behind for a corner); defenders in the shooting lane can deflect misses behind. Without this, corners would be near zero. |
| D6 | Clearance action for defenders under pressure | Defer; add only if the calibration checkpoint shows restart volumes far below real football. |
| D7 | Advantage | Not in M2; every foul stops play. |
| D8 | Offside judgement by the AI | Perfect referee; passers may misjudge marginal (≈ 1 m) offside positions with probability falling with Awareness. |
| D9 | Foul sources | Tackles only. |
| D10 | DOGSO inside the area | Penalty + yellow when the challenge is an attempt to play the ball (modern law); red otherwise. |
| D11 | Reduced-player shapes | Reduced-formation templates by outfield count (e.g. 10 → 4-4-1), with deterministic best-fit reassignment of remaining players; keeper dismissed → outfielder with highest Keeping goes in goal. |
| D12 | Fewer than 7 players | Match abandoned (terminal state with a reason; result recorded as abandoned). |
| D13 | Stamina recovery at half-time / during stoppages | None in M2; drain simply falls because players move less. Revisit at the calibration checkpoint. |
| D14 | Breaking public API (`MatchPhase` values, match length) | Accepted as part of M2.1; consumers (tests, DebugRunner, Godot) updated in the same PR. |

# 55. Expected Next Milestone

Milestone 3 is expected to be **Playbook & Manager Intervention**: player-contributed cards, match Playbook,
draw/hand/discard, context-valid cards, Command, Opportunity slowdown, Play/Continue decision, card effects on
intents and utilities, manager AI. Set pieces and stoppages from M2 are natural candidates for intervention moments.

Do not implement these during M2.

# 56. First Instruction to the AI Agent

> Read all project-memory documents in §1, inspect the simulation and tests, and verify the M1 baseline. Then produce
> a concrete implementation plan for M2.1–M2.12: proposed classes, dependency direction, new ADRs, testing strategy,
> fingerprint-change sequence and risks. Resolve or escalate the open decisions in §54. Do not write implementation
> code until the plan is reviewed. Preserve deterministic simulation/presentation separation and do not introduce
> card systems.
