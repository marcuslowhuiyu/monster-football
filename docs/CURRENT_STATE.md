# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.5 — Passing (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3 loop: `MatchSimulation`, fixed-timestep clock, `MatchRng`, phases, coin-toss kickoff, event log, determinism guards
- M1.4 movement and 4-3-3 shape, goalkeeper positioning
- M1.5 passing: `PassIntent`, `PassResolver` (estimate + resolve), `PassTargetSelector`, ball flight, interceptions, misplaced → loose ball, loose-ball collection, `PossessionChanged`, `MatchContext`
- ADR-012 (resolve at kick, apply on arrival), ADR-013 (no transcendental maths in outcomes)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed (not needed until presentation work)
- Pass decision is a placeholder (pass every 2.5 s held) → ~1 500 passes/match
- Interceptions < 1 % of passes: selector avoids risky lanes; no forcing pressure yet
- Ball never goes out of play (misplaced passes clamp to the pitch)

Next recommended work:
- M1.6 — carrying and tackling: `CarryIntent`, `TackleIntent`, resolvers, possession changes, events, tests

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (160/160 passing).
