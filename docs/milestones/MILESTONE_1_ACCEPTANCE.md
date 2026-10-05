# Milestone 1 — Acceptance Review

Assessment of `MILESTONE_1.md` §40 (acceptance criteria) and §41 (deliverables) at the end of M1.12.
Evidence points to tests (`tests/MonsterFootball.Simulation.Tests/…`), CI jobs (`.github/workflows/ci.yml`) and docs.

**Verdict: Milestone 1 is complete.** Every criterion is met. Caveats are calibration issues, not missing systems; they are listed at the end and carried forward in `docs/CURRENT_STATE.md`.

## Acceptance criteria (§40)

### Architecture

| Criterion | Status | Evidence |
|---|---|---|
| Simulation runs independently of presentation | Met | Plain `net10.0` library (ADR-003); `Architecture/DependencyRulesTests` fails if it references Godot or `godot/`. |
| Simulation runs headlessly | Met | Every test, the debug runner and the CI batch run without Godot. |
| No football result depends on visual physics | Met | All outcomes come from resolvers using `IMatchRandom` (ADR-001); no physics anywhere in the simulation. |
| Randomness is centralised and deterministic | Met | Single `IMatchRandom`/`MatchRng` (ADR-005, ADR-009); `DeterminismGuardTests` bans `System.Random`, `DateTime`, `Guid`, `Stopwatch`, `Parallel`; pinned RNG sequence test. |

### Football

| Criterion | Status | Evidence |
|---|---|---|
| Two 11-player teams complete a 90-minute match | Met | `MatchSimulationTests.Full_match_runs_to_ninety_minutes_and_finishes`; `TeamDefinitionTests`. |
| Players maintain basic shape | Met | `TeamShapeSystemTests` (flat back four goal-side, layers ordered, width/depth, mirror symmetry); `ShapeBehaviourTests` (no swarming: mean 1.8 players within 5 m of the ball). |
| Passing works | Met | `PassResolverTests`, `PassFlowTests` (completion, interception, misplacement, possession transfer). |
| Carrying works | Met | `CarryTests` (progression, possession kept, ends correctly). |
| Tackling works | Met | `TackleTests` (Won / Lost / LooseBall, range, cooldowns, keeper protection), `TacklingMatchTests`. |
| Shooting works | Met | `ShotTests` (distance, angle, pressure, finishing, keeper quality, outcomes). |
| Goalkeeping works | Met | `GoalkeeperPositioningTests`; keeper contest and distribution in `ShotTests`; keepers stay in their area all match. |
| Stamina works | Met | `StaminaTests`, `StaminaMatchTests` (drain, Stamina attribute effect, bounds, fatigue penalties). |
| Goals and restarts work | Met | `GoalAndRestartTests` (score once per goal, reset, conceding side kicks off). |

### Stats, events, determinism, testing, memory

| Criterion | Status | Evidence |
|---|---|---|
| Only the nine core attributes | Met | `PlayerStatsTests.Exposes_exactly_the_approved_attributes` (ADR-002). |
| Important actions emit structured events | Met | `MatchEventType` covers every required type (plus `TackleLooseBall`); event-sequence tests in `PossessionFlowMatchTests`, `TacklingMatchTests`, `GoalAndRestartTests`. |
| Complete event history available after the match | Met | `MatchResult.Events`; statistics are derived from it (ADR-019). |
| Identical setup and seed → identical outcomes and event histories | Met | `DeterminismTests` (full-match fingerprint of every event and full-precision final state); CI *Cross-platform determinism* compares Ubuntu, Windows and macOS. |
| Automated tests pass | Met | 265 tests; CI *Build & test* on three OSes. |
| Invariant validation passes | Met | `MatchStateInvariants` checked after every tick of full matches (`CheckedRun`). |
| ≥ 1 000 automated headless matches run successfully | Met | CI *Batch (1,000 matches)* on every PR, with pathology health bands (ADR-020). |
| Project memory documents exist and agree with the code | Met | `CLAUDE.md`, `GAME_DESIGN.md`, `ARCHITECTURE.md`, `MATCH_SIMULATION.md`, `DECISIONS.md` (ADR-001–020), `CURRENT_STATE.md`, `GLOSSARY.md`; audited against the code in M1.12. |

## Deliverables (§41)

| # | Deliverable | Status |
|---|---|---|
| 1 | Working Godot .NET project | Met, with a caveat: `godot/` builds in CI via `Godot.NET.Sdk/4.7.2`; it has not been opened in the Godot editor (not installed; no presentation work in M1). |
| 2 | Headless C# 11v11 simulation | Met |
| 3 | Automated tests | Met (265) |
| 4 | Deterministic seeded RNG | Met |
| 5 | MatchEvent system | Met |
| 6 | Debug match runner | Met — `DebugRunner match [seed] [--all]` |
| 7 | Basic match statistics | Met — `MatchResult.Statistics` |
| 8 | 1 000-match batch simulation | Met — `DebugRunner batch 1000`, CI job |
| 9–15 | `CLAUDE.md`, `GAME_DESIGN.md`, `ARCHITECTURE.md`, `MATCH_SIMULATION.md`, `DECISIONS.md`, `CURRENT_STATE.md`, `GLOSSARY.md` | Met |
| 16 | README: prerequisites, building, tests, one match, batch | Met — verified from a fresh clone in a clean environment (M1.12) |

## Required test areas (§33)

| Area | Tests |
|---|---|
| Team setup | `TeamDefinitionTests`, `MatchStateTests` |
| Determinism | `DeterminismTests`, `MatchRngTests`, `DeterminismGuardTests`, CI cross-platform job |
| Passing | `PassResolverTests`, `PassFlowTests`, `OnBallCandidateGeneratorTests` |
| Tackling | `TackleTests`, `TacklingMatchTests` |
| Shooting | `ShotTests`, `GoalAndRestartTests` |
| Stamina | `StaminaTests`, `StaminaMatchTests` |
| Match completion | `MatchSimulationTests` (finishes once, cannot continue) |

## Baseline figures (1 000 matches, all-5 teams, seeds 1–1000)

Goals 2.91 · shots 27.8 (32 % on target) · passes 1 667 (82 % complete) · home/draw/away 39.3 / 22.9 / 37.8 % · home possession 50.0 % · tackles 295 · distance 15.3 km per outfield player · HEALTHY on all 13 bands.

## Caveats carried forward

These are calibration, not missing functionality; none breaches a health band.

1. **Striker dominance** — strikers score ~82 % of goals.
2. **Volume** — ~1 670 passes and ~295 tackles per 90 continuous minutes (no stoppages) is busy.
3. **Distance** — 15.3 km per outfield player (real ≈ 10–12 km).
4. **Sensitivity** — shot volume is steep in `ShotBase`.
5. **Out of play** — the ball never leaves the pitch; misses go to the keeper (set pieces are out of scope).
6. **Godot editor** — the presentation project has only been built, not opened.
7. **Branch protection** — unavailable for this private repo on the current GitHub plan; the PR workflow is enforced by convention (ADR-017).
