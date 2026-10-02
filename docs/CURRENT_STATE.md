# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.2 — Core domain model (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, `TeamDefinition` (11-player / 4-3-3 validation), `PlayerDefinition`, `PlayerStats`, `PlayerMatchState`, `BallState`, `Score`, `MatchPhase`, `PitchGeometry`, `Vec2`
- Reusable `MatchStateInvariants`
- ADR-007 (possession lives on the ball), ADR-008 (fixed 4-3-3 slots, shirt numbers)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed (not needed until presentation work)
- Tests use a placeholder starting layout (`TestTeams.SpreadInOwnHalf`) until M1.4

Next recommended work:
- M1.3 — `MatchSimulation`, fixed timestep, `MatchClock`, `IMatchRandom`/`MatchRng`, match phases, determinism test; add clock invariant

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (46/46 passing).
