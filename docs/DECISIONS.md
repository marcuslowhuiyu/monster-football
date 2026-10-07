# Decision Log

Permanent record of accepted design and architecture decisions.

When a decision changes, do not delete it: mark it `Superseded by ADR-XXX` and add a new entry.

---

## ADR-001 — Simulation Is Authoritative

Status: Accepted

Decision:
Football outcomes are determined entirely by the headless simulation.
Godot presentation code only visualises results.

Reason:
This preserves determinism, testability and separation of concerns.

Rejected alternatives:
Using Godot physics and collision outcomes to determine football actions.

---

## ADR-002 — Core Player Attributes

Status: Accepted

Decision:
Players use the following attributes:

Technique, Passing, Finishing, Defending, Pace, Power, Stamina, Awareness, Keeping

Scale: 1–10

Reason:
The game intentionally uses a compact stat model rather than
Football Manager-style attribute granularity. Concepts such as vision,
crossing, strength or composure emerge from combinations of these attributes,
context, and (later) traits or cards.

Rejected alternatives:
Separate attributes such as Vision, Crossing, Strength, Composure, Aggression,
Acceleration, Tackling.

---

## ADR-003 — Simulation Is a Plain .NET Library; Godot Is an Isolated Presentation Project

Status: Accepted

Decision:
- `src/MonsterFootball.Simulation` is a plain `net10.0` class library with no Godot reference.
- The Godot project lives in `godot/` (`project.godot` + `MonsterFootball.Godot.csproj`) and references the simulation, never the reverse.
- Debug runner and tests are ordinary .NET projects that run with `dotnet` alone; the Godot editor is not required for Milestone 1.
- An architecture test fails the build if the simulation assembly references Godot or presentation assemblies.

Reason:
Enforces ADR-001 at compile time rather than by convention, keeps tests fast,
and allows headless batch simulation. The Godot project sits in its own folder
because the Godot SDK compiles every `.cs` file beneath its project directory;
placing it at the repository root would pull simulation and test sources into
the Godot assembly.

Rejected alternatives:
- Godot project at repository root (the structure suggested in the milestone spec): source globbing conflicts described above.
- Simulation code inside the Godot project: makes Godot a dependency of every test and of batch runs.

---

## ADR-004 — Repository Is the Agent's Long-Term Memory

Status: Accepted

Decision:
Project knowledge lives in `CLAUDE.md`, `docs/` and Git history.
No external memory database (Mem0, Zep, Letta) or RAG system is used in Milestone 1.

Reason:
Authoritative retrieval beats unverified remembered information. Repository
documents are versioned, reviewable and travel with the code.

Rejected alternatives:
External agent memory or vector databases — deferred until documentation size demonstrably wastes agent context.

---

## ADR-005 — Single Deterministic Random Source

Status: Accepted (implemented in M1.3 — see ADR-009)

Decision:
All simulation randomness flows through one seeded service (`IMatchRandom`) owned by the match.
No simulation system may create its own random generator or read wall-clock time.

Reason:
Identical setup + identical seed must produce identical event histories (testing, replays, debugging).

Rejected alternatives:
`System.Random` instances per system; `Random.Shared`; Godot's random functions.

---

## ADR-006 — Toolchain

Status: Accepted

Decision:
- .NET 10 (LTS), C# latest, nullable enabled, warnings treated as errors (`Directory.Build.props`).
- xUnit for tests.
- Godot 4.7 via `Godot.NET.Sdk/4.7.2` for the presentation placeholder.
- Solution file: `MonsterFootball.slnx`.

Reason:
.NET 10 is the current LTS release and is supported by Godot 4.7's .NET SDK.
Warnings-as-errors keeps nullability honest from the start.

---

## ADR-007 — The Ball Is the Single Source of Truth for Possession

Status: Accepted

Decision:
`BallState.PossessorId` is the only stored record of possession.
`MatchState.HasBall(id)`, `MatchState.PossessionPlayerId` and `MatchState.PossessionTeam` are derived from it.
`PlayerMatchState` has no stored `HasBall` flag, and `MatchState` has no stored possession team.

Reason:
The milestone spec lists `HasBall`, `PossessionTeamId` and `PossessionPlayerId` as separate fields.
Storing the same fact three times allows them to disagree. Deriving them makes
"at most one player possesses the ball" and "possessing player belongs to possession team"
true by construction rather than by checking.

Consequences:
When the ball is loose, `PossessionTeam` is null. If later systems need "last team in possession"
(e.g. for restarts), add it as a separate, explicitly named field.

Rejected alternatives:
Separate stored flags kept in sync by convention.

---

## ADR-008 — Milestone 1 Teams Fill Each 4-3-3 Slot Exactly Once

Status: Accepted

Decision:
- `PlayingPosition` enumerates the 11 slots of the default 4-3-3: GK, LB, LCB, RCB, RB, LCM, CM, RCM, LW, ST, RW.
- `TeamDefinition` is valid only with 11 players, exactly one goalkeeper, unique ids, unique shirt numbers, and every slot filled exactly once.
- `PlayerDefinition` includes a `ShirtNumber` (1–99) for readable event output ("Blue #9").

Reason:
Custom formations are out of scope for Milestone 1. A fixed slot set lets positioning (M1.4)
map each player to one anchor without ambiguity. Shirt numbers are presentation-friendly identity, not a stat.

Consequences:
Introducing formations later will replace the "every slot exactly once" rule with formation-specific validation — supersede this ADR when that happens.

---

## ADR-009 — MatchRng Uses an In-House xoshiro256** Implementation

Status: Accepted

Decision:
- `IMatchRandom` exposes `NextDouble()`, `NextInt(min, maxExclusive)` and `Roll(probability)`.
- `MatchRng` implements it with xoshiro256**, state seeded from the `int` match seed via SplitMix64.
- `NextInt` uses rejection sampling (no modulo bias). `Roll` always consumes exactly one draw, even for 0 or 1.
- A test pins the first outputs for seed 123456; the values were cross-checked against an independent reference implementation.
- An architecture test scans the compiled simulation for references to `System.Random`, `DateTime`, `DateTimeOffset`, `Guid`, `Stopwatch` and `Parallel`, and fails if any appear.

Reason:
`System.Random`'s algorithm is an implementation detail of .NET and may change between runtime versions,
which would silently change every seeded match. Owning a small, well-known algorithm makes seeds permanent.
Constant draw consumption in `Roll` keeps the random stream aligned regardless of probability values.

Consequences:
Changing the algorithm, seeding, or the order in which systems draw numbers changes the outcome for every seed.
Treat such changes as deliberate and update pinned tests consciously.

Rejected alternatives:
`new System.Random(seed)`; `Random.Shared`; per-system generators.

---

## ADR-010 — Stepping a Finished Match Throws

Status: Accepted

Decision:
`MatchSimulation.Step()` throws `InvalidOperationException` once `Phase == Finished`.
`RunToCompletion()` is idempotent: calling it again returns the same result without new events.

Reason:
The milestone requires that the simulation "cannot accidentally continue after finish". A silent no-op would
hide caller bugs (e.g. a presentation loop that keeps stepping); an exception surfaces them immediately.

---

## ADR-011 — Team Shape Is Computed in Team Frame from Ball-Shifted Anchors

Status: Accepted

Decision:
- Shape logic is written once in a team-relative frame (own goal at X = 0, attacking +X, left flank +Y) and converted to pitch coordinates by a 180° rotation for Away (`TeamFrame`).
- Each 4-3-3 slot has an in-possession and an out-of-possession anchor (authored on 105 × 68, scaled to the pitch). Each tick the block is shifted toward the ball by a fixed fraction of the ball's offset from the centre spot.
- Out of possession the back four share one line X, kept goal-side of the ball; midfield sits ahead of the line and is compressed centrally; forwards sit ahead of midfield. The nearest outfield player sprints to the ball.
- When the ball is loose, both teams use the out-of-possession shape and each sends one presser.
- Goalkeepers stand on the goal–ball line at a depth that grows with ball distance and is scaled by Awareness; they never leave the penalty area.

Reason:
Gives a recognisable, mirror-symmetric shape with very little code and no randomness, and
avoids "22 players chasing the ball". Writing the rules in team frame removes a whole class of
Home/Away sign bugs; a symmetry test enforces it.

Consequences:
This is positional scaffolding, not tactical intelligence. Off-ball choices such as support runs,
forward runs and recovery (M1.9 utility AI) will adjust or override these targets.

Update (M1.9, M1.11): off-ball utility AI now overrides these targets where it chooses to (ADR-018), and the shift
is computed from each team's smoothed *shape focus* rather than the raw ball (ADR-020); the team-frame model is unchanged.

---

## ADR-012 — Passes Are Resolved at the Kick and Applied on Arrival

Status: Accepted

Decision:
- `PassResolver.Resolve` decides the outcome (Completed / Intercepted / Misplaced) and the landing point when the ball is struck, using `IMatchRandom`.
- The ball then travels as a `BallFlight` for the resolved duration (ball speed 12 + 0.25 × distance m/s, capped at 25). During flight nobody possesses it; `MatchState.AttackingTeam` stays with the passing team.
- On the arrival tick the outcome is applied: receiver or interceptor gains possession, or the ball is left loose at the misplaced landing point.
- A loose ball is collected by the nearest player within 1.5 m.
- Every possession transfer goes through `MatchContext.GiveBall`, which emits `PossessionChanged` whenever control passes to the other team. `MatchState.LastPossessionTeam` records who last had control (the field anticipated by ADR-007).

Reason:
Deciding at the kick keeps resolution a single, testable function and gives the presentation layer the
authoritative result (start, target, duration, quality) before the animation starts. Holding the ball in
flight stops possession teleporting and means nobody can act on a ball that hasn't arrived.

Consequences:
Players can move during the flight, so a receiver may be a little off the landing point; the ball snaps to
the player who gains it. Misplaced passes are clamped to the pitch — there are no throw-ins or goal kicks yet.

Rejected alternatives:
Instant resolution with possession transferred immediately (simpler, but possession changes before the ball
visibly arrives); continuous ball physics with interception checks per tick (more complex, harder to test).

---

## ADR-013 — No Transcendental Maths Functions in Outcome Calculations

Status: Accepted

Decision:
Outcome-affecting code uses only +, −, ×, ÷, `Math.Sqrt`, `Math.Abs`, `Math.Min/Max/Clamp`. Probabilities come
from `Curves.Sigmoid(x) = 0.5 + 0.5·x/√(1+x²)` rather than a logistic `1/(1+e^−x)`. Random directions are
produced by rejection sampling the unit disc, not with `sin`/`cos`.

Reason:
IEEE 754 requires basic arithmetic and square root to be correctly rounded, so they give identical results on
every platform. `Math.Exp`, `Math.Sin` etc. delegate to platform maths libraries that can differ in the last bit,
which would let the same seed play a different match on another OS or CPU.

---

## ADR-014 — Carries Keep the Ball; Tackles Resolve in Two Rolls

Status: Accepted

Decision:
- A carry is a planned run of up to 10 m (`CarryResolver`, no randomness). The carrier keeps possession throughout; only an opponent's tackle (or the carrier's own pass) ends it early. Speed = top speed × (0.55 + 0.03 × Technique); Awareness steers away from the nearest opponent.
- A tackle (`TackleResolver`) rolls **contact** — `Sigmoid(0.3 × (defender rating − carrier rating) + 0.15)` — and, if made, **clean** — `0.6 + 0.03 × (Awareness − 5)`. Outcomes: no contact → `Lost`; clean → `Won`; otherwise → `LooseBall` (ball 2–5 m away in a random direction).
- The spec's "LooseBall" outcome is emitted as a new event type `TackleLooseBall`.
- Any time the ball leaves a player (`GiveBallTo` someone else, `SetBallLoose`, `LaunchBall`), `MatchState` clears that player's intent and carry. An invariant checks a carry only exists on the ball carrier.

Reason:
The spec says carrying should "retain possession" and "expose the player to defenders": the exposure is the
tackle, so a separate heavy-touch roll would double-count risk. Two-roll tackles give the three required
outcomes with separate, tunable knobs (who wins the duel vs. who ends up with the ball). Clearing intents
in `MatchState` rather than in each caller makes stale carries impossible (an invariant caught exactly this bug).

Consequences:
Tackle and carry *decisions* (when to tackle, when to carry) are placeholders until utility AI (M1.9).

Update (M1.9): the decisions are now made by the utility AI and judgement-based tackle commitment (ADR-018);
the resolution model above is unchanged.

---

## ADR-015 — Shots Resolve Immediately; Keeper Restarts After Saves and Misses

Status: Accepted; timing superseded by ADR-022 (shots now travel and apply on arrival)

Decision:
- A shot is resolved and applied in the same tick: `ShotTaken` then exactly one of `GoalScored`, `ShotSaved`, `ShotMissed`. The `ShotResult` still carries start/target position and travel duration for animation.
- Two rolls: **on target** `Sigmoid(0.35·(ability − 0.15·distance − pressure − angle) − 1)`, then **save** `Sigmoid(0.35·(keeper rating − shot power) + 0.74)`. Keeper rating = Keeping + 0.3·(Awareness − 5) − 0.3·(metres out of position, ≤ 10). Shot power = 0.5·Finishing + 0.5·Power + 0.2·max(0, 20 − distance).
- After a save **or a miss**, the defending goalkeeper gets the ball. A miss stands in for a goal kick until restarts exist.
- A goal moves the match to `GoalRestart`; the next tick resets every player to the kickoff layout, and the tick after that the conceding side kicks off.
- A goalkeeper holding the ball in their own penalty area cannot be tackled. Goalkeepers never shoot or carry.

Reason:
A shot lasts well under a second and Milestone 1 has nothing that could intervene (no blocks, deflections or
rebounds), so flight adds complexity without changing outcomes. Unlike passes (ADR-012), no player needs to
react while the ball travels. Giving the keeper the ball after a miss avoids inventing goal kicks, which are out of scope.

Consequences:
If blocks or rebounds are added, shots should move to the ball-flight model.

---

## ADR-016 — Stamina Drains from Distance and Action Events; One Fatigue Model

Status: Accepted

Decision:
- `FatigueModel` is the single home for stamina effects: drain multiplier from the Stamina attribute (1.3 − 0.06 × Stamina), speed factor (up to −30 % at empty), effectiveness factor (up to −15 % at empty) and tiredness (0–1).
- `StaminaSystem` runs last in each open-play tick. It charges (a) metres covered that tick × cost per metre for the movement intensity (jog 0.002, carry 0.003, sprint 0.004), and (b) a fixed cost per action, read from events emitted since its last run (pass 0.05, carry start 0.1, tackle 0.2 for the tackler / 0.1 for the carrier, shot 0.3).
- The effectiveness factor scales the ability/rating terms in the pass, tackle and shot resolvers (including interceptors and goalkeepers).
- "Demanding movement utility": presser selection weights distance by (1 + tiredness), so exhausted players are less likely to be sent to press.
- No recovery yet.

Reason:
Distance-based drain makes cost proportional to work actually done, independent of tick length. Reading
action costs from the event log keeps resolvers and executions free of stamina code and gives a single place
to add future costs (pressing intensity, species physiology). Centralising every fatigue effect makes later
recovery, substitutions or injuries a change to one class rather than to every resolver.

Consequences:
Stamina cost now depends on how far players run, so movement tuning (M1.9/M1.11) changes fatigue too.

---

## ADR-017 — CI Pipeline and Branch/PR Workflow

Status: Accepted; triggers and OS matrix amended 2026-10-07 (see *Amendment: Actions minutes budget* below)

Decision:
- **Workflow**: all changes go through a short-lived branch (`feat/…`, `fix/…`, `chore/…`, `docs/…`) and a pull request into `main`. `main` is never committed to directly. PRs are merged with **rebase-and-merge** to keep the small, conventional commits (§4.8 of the milestone) on a linear history. Every commit must build.
- **CI** (`.github/workflows/ci.yml`), on every PR that changes more than docs (and on demand):
  - `Format` — `dotnet format --verify-no-changes` against `.editorconfig`.
  - `Build & test (ubuntu-latest)` — locked restore, Release build of the solution (warnings are errors), full test suite with TRX results and coverage uploaded as artifacts.
  - `Build & fingerprint` on **Windows and macOS** — locked restore and Release build of the debug runner, then fingerprints.
  - `Cross-platform determinism` — each OS runs `DebugRunner fingerprint` for fixed seeds; the job fails if any OS produced a different match (`MatchFingerprint`, SHA-256 of every event and full-precision final state).
- **Reproducibility**: `global.json` pins the SDK (patch roll-forward only); `packages.lock.json` files are committed and CI restores in locked mode; `.gitattributes` normalises line endings to LF.
- **Supply chain**: third-party actions are pinned to commit SHAs (version in a comment); Dependabot proposes weekly NuGet and Actions updates.
- **Code style**: `.editorconfig` rules are enforced in the build (`EnforceCodeStyleInBuild`); XML doc generation is on so unused usings (IDE0005) and broken `cref`s fail the build; CS1591 (missing XML comment) is suppressed.

Reason:
ADR-013 promises identical results on every platform; only a multi-OS comparison can prove it. CI turns the
existing test discipline into a gate that a change cannot skip, and the PR flow gives each subtask a
reviewable unit with its CI result attached.

Enforcement:
GitHub rulesets/branch protection are unavailable for this private repository on the current plan (API returns 403:
"Upgrade to GitHub Pro or make this repository public"). Until that changes, the repository allows **rebase-merge only**
and auto-deletes merged branches, and the workflow is enforced by discipline (`CLAUDE.md`, the personal
`shipping-changes-via-pr-and-ci` skill). If the repo goes public or the plan is upgraded, add a ruleset requiring a PR and
the five CI checks (`Format`, `Build & test (ubuntu-latest|windows-latest|macos-latest)`, `Cross-platform determinism`).

Lessons:
Run CI's exact commands locally, including coverage collection: coverlet instruments the test-output copy of the
simulation assembly, which first broke the determinism guard only in CI (fixed by scanning the original build output).

Rejected alternatives:
Single-OS CI (cannot detect cross-platform divergence); squash merges (would collapse the small commits the
milestone asks for); floating action tags such as `@v7` (mutable, supply-chain risk).

Amendment: Actions minutes budget (2026-10-07):
The private repository has 2 000 Actions minutes a month; macOS minutes bill at 10× and Windows at 2×. By 7 October
CI had used ≈ 1 820 billable minutes: the macOS test job 1 350 (73 %), Windows 220, and 42 % of everything came from
re-running CI on push to `main` after each merge. Changes:
- **No CI on push to `main`.** With rebase-and-merge the merged tree is the PR's tested tree, provided the branch is
  rebased on current `main` before merging; if `main` moved since the PR's last green run, rebase and let CI run again.
- **Full tests on Ubuntu only.** Windows and macOS build the debug runner and produce fingerprints — the cross-platform
  determinism check is what they add (ADR-013). macOS stays: it runs on ARM64, the platform most likely to differ.
- **Docs-only PRs skip CI** (`docs/**`, `**/*.md`); in-progress runs on a branch are cancelled by newer pushes.
- Estimated cost per merged PR: ≈ 100 → ≈ 45 billable minutes. If still too high, run macOS on a weekly schedule.
- Making the repository public would remove the limit.

---

## ADR-018 — Utility AI: Scored Candidates, Bounded Randomness, Weights as Data

Status: Accepted

Decision:
- Every decision is a list of `ActionCandidate`s with utilities, chosen by one `UtilitySelector`:
  candidates within `Tolerance` (8) of the best are plausible; one plausible option is taken without a random draw,
  several are chosen with probability proportional to how far each clears the cut-off (one `IMatchRandom` draw).
  The best option is always most likely; anything more than 8 below it is never picked.
- **On the ball** (`OnBallCandidateGenerator`): Hold, Pass (one per teammate ≥ 4 m away), Carry, Shoot (offered from xG 0.03).
  Utilities reuse resolver estimates (pass success, shot xG), so the AI judges options with the outcome maths.
- **Off the ball** (`OffBallCandidateGenerator` + `OffBallDecisionSystem`): MoveIntoPosition (team-shape target),
  SupportBallCarrier, MakeForwardRun, DefendGoalSide, RecoverPosition. Players commit to a choice (`OffBallPlan`)
  for 1–3 s; team limits: 2 supporters, 2 runners, each opponent marked once.
  CloseDownOpponent stays a team-level assignment in `TeamShapeSystem` (one fatigue-weighted presser, now jockeying
  1.5 m goal-side of a carrier rather than standing on the ball); its tackle commitment rate scales with contact
  chance and danger (own third).
- **Weights are data**: `AiTuning` (`OnBallWeights`, `OffBallWeights`) on `MatchConfig.Ai`. Defaults came from a
  random search scored against plausibility bands; batch tuning (M1.11) can override them without code changes.
- **Extension point**: `IUtilityModifier` adjusts candidate utilities before selection — the hook for roles, traits,
  tactics, manager instructions, species and cards. No modifiers exist in Milestone 1.
- Attackers' shape targets stop at the opponents' last outfield line (positional discipline, not an offside rule).
- Shots are penalised 0.5 per outfield defender within 1.5 m of the shooting lane (blocking pressure).

Reason:
A single selector gives one place for determinism, randomness and future modifiers. Continuous utilities remove
the threshold cliffs of the placeholder rules (e.g. shooting jumping from 6 to 34 shots per match). Commitment
stops off-ball targets jittering every tick. Weights as data make tuning measurable and repeatable.

Consequences:
Behaviour emerges from interacting weights; changing one (e.g. pressure) shifts several match statistics at once.
Tune with batch runs, not single matches.

Rejected alternatives:
Argmax only (identical choices in identical situations; predictable); softmax with `Math.Exp` (ADR-013);
per-system ad-hoc thresholds (the M1.5–M1.8 placeholders).

---

## ADR-019 — Statistics Are Derived from the Event Log

Status: Accepted

Decision:
- `MatchStatistics.FromEvents(events)` computes all statistics; the simulation keeps no separate counters. `MatchResult.Statistics` is filled from it.
- Per team: goals, shots, shots on target (goals + saves), passes attempted/completed, possessions won, possession seconds. Match: possession changes.
- Possession time is credited to the team that last had the ball under control — including while its pass is in flight or the ball is loose — from the opening kickoff, switching at each `PossessionChanged`, until `MatchFinished`. Later kickoffs do not reset tracking (a post-goal kickoff already emits `PossessionChanged`).
- `Diagnostics.MatchReport` renders a terse text log and statistics table for the debug runner; it is not commentary.

Reason:
The milestone lists statistics as an event-log consumer. Deriving them keeps one source of truth (events), makes
statistics automatically deterministic and replayable, and lets them be recomputed for any recorded match.

Consequences:
Any statistic needs the events that carry it; a new statistic may require a new event type or metadata.

---

## ADR-020 — Batch Simulation with Health Bands Gates Every Change

Status: Accepted

Decision:
- `DebugRunner batch [count] [--from SEED]` plays matches in parallel (parallelism stays in the runner, ADR-005), reduces each to a `MatchSummary`, and aggregates a `BatchSummary` in seed order (identical however matches were scheduled).
- `BatchHealth` checks the aggregate against wide **pathology bands** (not realism targets): goals 0.5–6, shots 6–60, on-target share 0.2–0.6, pass completion 0.6–0.97, home possession 0.45–0.55 and |home win − away win| ≤ 0.1 between equal teams, draws 0.05–0.6, team-matches without a shot ≤ 10 %, ≤ 14 goals in any match, striker share of goals ≤ 85 %, tackles 10–600, outfield distance 5–16 km, outfield stamina at full time 10–95. The command exits non-zero if any band fails.
- CI runs `batch 1000` on every code PR (≈ 2.5 min) and uploads the report.
- Tuning in M1.11 used batches, not single matches: arrival slowdown for jogging (`Easy` for the team in possession, a small radius for `Jog`), per-team **shape focus** (the shape follows a moving average of the ball — 3 s for the attacking team, 0.75 s for the defending team), AI weights (`HoldBase` 88, `CarryBase` 42, `ShotBase` 22) and the shot model's on-target bias (−0.7).

Reason:
The milestone's acceptance criteria require ≥ 1 000 automated headless matches and the identification of pathological
behaviour. Making the batch a CI gate means a change that breaks the football (e.g. teams stop shooting, home bias,
20 km per player) fails review like a failing test.

Consequences:
Bands can be tightened as the simulation matures. A legitimate behaviour change that moves a figure outside a band
needs the band revisited deliberately, with the reason recorded here.

---

## ADR-021 — Possession Changes Carry a Cause; Only Open-Play Changes Count as Won

Status: Accepted (M2.1a)

Decision:
- `MatchContext.GiveBall(player, cause)` requires a `PossessionCause`; `PossessionChanged` carries it as `cause` metadata.
- Causes: `Reception`, `Interception`, `Tackle`, `LooseBall`, `Save` (open play) and `Kickoff`, `KeeperRestart` (restarts awarded by the laws, not won). `PossessionCauseExtensions.IsOpenPlay` is the single definition.
- `MatchStatistics.PossessionsWon` counts only open-play causes. `PossessionChanges` still counts every change, and possession time still switches at every change.
- No new event types: the cause is metadata, so consumers that only need "who has the ball now" are unaffected. Restarts added in M2 (throw-ins, goal kicks, corners, free kicks, penalties) add causes to the same enum.

Reason:
A kickoff after conceding, or the keeper picking the ball up after a miss, changes possession without anyone winning it.
Counting those as "possessions won" mixed defensive success with the laws' bookkeeping (≈ 214 per side per match).
With M2's restarts this distinction becomes much larger, and tactical analysis needs it (a Transition follows a ball
won in open play, not a throw-in).

Consequences:
Every match fingerprint changes (the `PossessionChanged` canonical text gains `cause`), but no football does: the
1 000-match batch figures are identical to the M1 baseline. `PossessionsWon` falls; `PossessionChanges` is unchanged.

---

## ADR-022 — Shots Are Resolved at the Strike and Applied on Arrival

Status: Accepted (M2.1b). Supersedes the timing in ADR-015; its probabilities and keeper restarts are unchanged.

Decision:
- `ShotExecution.Strike` emits `ShotTaken` and launches a `BallFlight` holding the decided `ShotResult`. `BallFlightSystem`
  moves it each tick and calls `ShotExecution.Land` on the arrival tick, which applies the outcome exactly as before
  (goal → score and `GoalRestart`; save or miss → the keeper gets the ball).
- `BallFlight` moved from `Actions.Passing` to `Actions` and carries a `PassResult` **or** a `ShotResult` (`Team`,
  `StartPosition`, `TargetPosition` shared). Travel time in ticks uses one rule for both: ⌈travel ÷ tick⌉, at least one.
- A goal ends the tick at the ball-flight step (the check moved from after the on-ball decision).
- Interim: a shot still in the air at 90:00 is abandoned. M2.1d replaces this: a period ends only at a stoppage, so a
  struck shot always arrives.

Reason:
Milestone 2 §6.1: the viewer must show the ball reach the net before the score changes, and later laws (parries,
deflections, corners) need the ball to exist in flight. The outcome stays predetermined at the strike, as for passes
(ADR-012), so presentation never decides it.

Consequences:
Every match fingerprint changes (players keep moving for the shot's flight time, and outcomes land later). 1 000-match
batch (seeds 1–1000), before → after: goals 2.91 → 2.87, shots 27.8 → 27.4, passes 1 667 → 1 660, tackles 295 → 293,
possession changes 397 → 395, striker share 82 % → 83 %, home−away win gap 0.015 → 0.051 — the last is seed noise
(seeds 1001–2000: 0.017 after vs 0.025 before, favouring away). All bands pass.

---

## ADR-023 — The Ball Is In Play or Play Is Stopped; Stoppages Are Records

Status: Accepted (M2.1c). Replaces the `Kickoff`/`GoalRestart` phases.

Decision:
- `MatchPhase` is `Stopped` (dead ball), `RestartSetup` (waiting for positions), `OpenPlay` or `Finished`. Taking the
  restart returns to `OpenPlay`; there is no separate "restart" phase because the restart is an action on one tick.
- `MatchState.CurrentStoppage` holds a `Stoppage(Reason, Restart, AwardedTeam, Location, StoppedAtTick)` exactly while
  play is stopped (invariant). Nobody holds a dead ball (invariant). Reasons and restart types are appended as laws
  arrive; fields (offender, fouled player, discipline) are added only when a law needs them.
- `MatchContext.StopPlay` is the single way to stop play: it records the stoppage, clears intents, plans and tackle
  cooldowns, and emits `PlayStopped` (metadata `reason`, `restart`). A kickoff keeps its existing `Kickoff` event;
  later restart types will emit a generic restart event.
- While play is stopped, `RestartSystem` gives every player a restart target and they **walk** there under the normal
  movement and stamina rules; no decisions, tackles or random draws happen. The restart is taken once the minimum
  dead-ball time has passed and everyone is in position, or after a setup timeout with stragglers placed. Timing and
  distances live in `MatchConfig.Restarts` (`RestartTuning`).
- After a goal: 45 s dead ball, then the kickoff once everyone is back in the kickoff layout. Kickoffs that start a
  period are taken immediately with the teams placed (they line up before the whistle).
- The kickoff layout is legal: everyone in their own half and the strikers' anchors 10.5 m from the centre spot
  (was 3.5 m, inside the circle); the kicking side's ST walks to the ball.
- `MatchState.KickoffTeam` is removed: the awarded team lives on the stoppage.

Reason:
Milestone 2 needs throw-ins, goal kicks, corners, free kicks and penalties. One stoppage model, with kickoffs and goals
as its first users, avoids a special case per restart and gives the viewer, statistics (ball-in-play time) and
tactical analysis (no open-play Opportunities while play is stopped) one thing to read.

Consequences:
Every fingerprint changes; `MatchPhase` values are a public API break (D14), updated in all consumers in the same PR.
1 000-match batch, before → after: goals 2.87 → 2.86, shots 27.4 → 27.2, passes 1 660 → 1 621, tackles 293 → 286,
possession changes 395 → 385, outfield distance 15.3 → 15.1 km, stamina at full time 48.6 → 49.6. All bands pass.
Each goal now removes ~50 s of open play; M2.1d adds the matching added time.
