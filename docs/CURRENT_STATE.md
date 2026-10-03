# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.9 — Utility AI (complete)

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
- ADR-016 (stamina model), ADR-018 (utility AI)
- CI/CD: GitHub Actions (format, build+test on Ubuntu/Windows/macOS, cross-platform determinism fingerprint), pinned SDK, NuGet lock files, `.editorconfig` enforced in build, Dependabot, PR template, branch/PR workflow (ADR-017)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- History before ADR-017 (28 commits) went directly to `main`
- Branch protection unavailable on the current GitHub plan for this private repo; PR workflow enforced by convention (ADR-017)
- Godot editor not installed (not needed until presentation work)
- Defaults: ~2.3 goals, ~43 shots (19 % on target), ~2 000 passes, ~220 carries, ~170 tackles per match; ST scores ~80 % of goals
- Players cover 16–21 km per match (continuous jogging toward moving targets)
- Test suite with coverage takes ~4.5 min (AI cost per tick)
- Ball never goes out of play; misses go to the keeper

Next recommended work:
- M1.10 — full match: `MatchResult.Statistics` (goals, shots, on target, passes completed/attempted, possession changes, possession time), debug runner single-match readable log

Last verified:
2026-10-03 — format clean, Release build 0 warnings, `dotnet test` 244/244 passing.
