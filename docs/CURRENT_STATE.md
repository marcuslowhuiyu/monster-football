# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.4 — 11v11 movement and positioning (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3 loop: `MatchSimulation`, fixed-timestep clock, `MatchRng`, phases, coin-toss kickoff, event log, determinism guards
- M1.4: `MovementSystem` (Pace + fatigue, jog/sprint), `TeamShapeSystem` (ball-shifted in/out-of-possession 4-3-3, flat back four, single presser), `GoalkeeperPositioning` (Awareness-scaled), team-frame symmetry, ball follows carrier
- ADR-011 (team-frame shape)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed (not needed until presentation work)
- No on-ball actions: kickoff taker holds the ball all match; opposing presser stands on it; every match is 0–0

Next recommended work:
- M1.5 — passing: `PassIntent`, `PassResolver`, teammate selection, interception, possession transfer, pass events, presentation data on results

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (133/133 passing).
