# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.8 — Stamina (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3 loop: `MatchSimulation`, fixed-timestep clock, `MatchRng`, phases, coin-toss kickoff, event log, determinism guards
- M1.4 movement and 4-3-3 shape, goalkeeper positioning
- M1.5 passing: resolver with interceptions, target selection, ball flight, loose balls, `PossessionChanged`
- M1.6 carrying and tackling, `CurrentIntent`, central intent clearing
- M1.7 shooting, goalkeeper contest, goals, restarts
- M1.8: `FatigueModel` (drain multiplier, speed and effectiveness penalties), `StaminaSystem` (distance × intensity + action events), fatigue applied in pass/tackle/shot resolvers, tired players press less
- ADR-016 (stamina model)
- CI/CD: GitHub Actions (format, build+test on Ubuntu/Windows/macOS, cross-platform determinism fingerprint), pinned SDK, NuGet lock files, `.editorconfig` enforced in build, Dependabot, PR template, branch/PR workflow (ADR-017)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- History before ADR-017 (28 commits) went directly to `main`
- Branch protection unavailable on the current GitHub plan for this private repo; PR workflow enforced by convention (ADR-017)
- Godot editor not installed (not needed until presentation work)
- Placeholder decisions: ~34 shots, ~2 goals, ~2 000 passes, ~450 tackles per match; only strikers score
- Players cover 16–18 km per match; strikers finish near-empty stamina
- Ball never goes out of play; misses go to the keeper

Next recommended work:
- M1.9 — utility AI: on-ball candidate generation (pass/carry/shoot) with utility scores and controlled deterministic randomness; off-ball decisions (MoveIntoPosition, SupportBallCarrier, MakeForwardRun, CloseDownOpponent, RecoverPosition, DefendGoalSide); replace placeholder decisions

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (220/220 passing).
