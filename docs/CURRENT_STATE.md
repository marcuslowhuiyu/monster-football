# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.6 — Carrying and tackling (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3 loop: `MatchSimulation`, fixed-timestep clock, `MatchRng`, phases, coin-toss kickoff, event log, determinism guards
- M1.4 movement and 4-3-3 shape, goalkeeper positioning
- M1.5 passing: resolver with interceptions, target selection, ball flight, loose balls, `PossessionChanged`
- M1.6: `CarryIntent`/`CarryResolver`/`CarrySystem`, `TackleIntent`/`TackleResolver`/`TackleSystem`, `CurrentIntent`, `OnBallDecisionSystem` placeholder (pass/carry), central intent clearing on ball release
- ADR-014 (carries keep the ball; two-roll tackles)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed (not needed until presentation work)
- Placeholder decisions make matches too busy: ~2 000 passes, ~460 tackles, ~300 possession changes per match
- Ball never goes out of play (misplaced passes / loose balls clamp to the pitch)
- No shooting yet; every match is 0–0

Next recommended work:
- M1.7 — shooting and goalkeeper: `ShotIntent`, `ShotResolver`, keeper contest, goals, saves, misses, restarts (GoalRestart → kickoff to conceding team)

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (183/183 passing).
