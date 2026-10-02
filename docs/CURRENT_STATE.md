# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.7 — Shooting and goalkeeper (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3 loop: `MatchSimulation`, fixed-timestep clock, `MatchRng`, phases, coin-toss kickoff, event log, determinism guards
- M1.4 movement and 4-3-3 shape, goalkeeper positioning
- M1.5 passing: resolver with interceptions, target selection, ball flight, loose balls, `PossessionChanged`
- M1.6 carrying and tackling, `CurrentIntent`, central intent clearing
- M1.7: `ShotIntent`/`ShotResolver`/`ShotExecution`, keeper contest (Keeping, Awareness, positioning), goals/saves/misses, `RestartSystem` (goal reset + kickoff to conceding side), keepers protected in own area
- ADR-015 (immediate shots; keeper restarts after saves/misses)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed (not needed until presentation work)
- Placeholder decisions: ~34 shots, ~2 goals, ~2 000 passes, ~450 tackles per match; only strikers score; shot threshold sits on a cliff
- Ball never goes out of play; misses go to the keeper

Next recommended work:
- M1.8 — stamina: consumption by movement intensity and actions, Stamina attribute effect, fatigue penalties, tests

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (203/203 passing).
