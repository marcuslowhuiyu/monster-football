# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.11 — Batch simulation (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3 loop: `MatchSimulation`, fixed-timestep clock, `MatchRng`, phases, coin-toss kickoff, event log, determinism guards
- M1.4 movement and 4-3-3 shape, goalkeeper positioning
- M1.5 passing: resolver with interceptions, target selection, ball flight, loose balls, `PossessionChanged`
- M1.6 carrying and tackling, `CurrentIntent`, central intent clearing
- M1.7 shooting, goalkeeper contest, goals, restarts
- M1.8: `FatigueModel`, `StaminaSystem`, fatigue in resolvers
- M1.9: utility AI — `UtilitySelector` (bounded deterministic randomness, `IUtilityModifier` hook), on-ball candidates (hold/pass/carry/shoot), off-ball behaviours (support, forward runs, goal-side marking, recovery) with commitment and team limits, presser jockeying, judgement-based tackle commitment, shot blocking, attacking shape capped at opponents' last line, weights as data (`AiTuning`)
- M1.10: `MatchResult.Statistics` derived from events (goals, shots, on target, passes, possessions won, possession time), `Diagnostics.MatchReport`, DebugRunner `match [seed] [--all]`
- M1.11: `MatchSummary`/`BatchSummary`/`BatchHealth`, DebugRunner `batch` (parallel, exit 1 on pathology), CI batch job (1,000 matches), per-player distance tracking; tuning — arrival slowdown, per-team shape focus, AI weights, shot on-target bias. 1,000-match batch HEALTHY: 2.91 goals, 27.8 shots (32 % on target), 1,667 passes, 15.3 km
- ADR-016 (stamina model), ADR-018 (utility AI), ADR-019 (statistics from events), ADR-020 (batch health gate)
- CI/CD: GitHub Actions (format, build+test on Ubuntu/Windows/macOS, cross-platform determinism fingerprint), pinned SDK, NuGet lock files, `.editorconfig` enforced in build, Dependabot, PR template, branch/PR workflow (ADR-017)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- History before ADR-017 (28 commits) went directly to `main`
- Branch protection unavailable on the current GitHub plan for this private repo; PR workflow enforced by convention (ADR-017)
- Godot editor not installed (not needed until presentation work)
- Strikers score ~82 % of goals; ~1,670 passes and ~295 tackles per match (busy)
- Players cover 15.3 km per match (real ≈ 10–12 km), just inside the 16 km band
- Test suite with coverage takes ~4.5 min (AI cost per tick)
- Ball never goes out of play; misses go to the keeper

Next recommended work:
- M1.12 — documentation and architecture review: verify docs against code, clean CURRENT_STATE, verify ADRs and glossary, check README instructions from a clean clone, run full tests + determinism + 1,000-match batch, assess acceptance criteria

Last verified:
2026-10-05 — format clean, Release build 0 warnings, `dotnet test` 265/265 passing, `batch 1000` HEALTHY.
