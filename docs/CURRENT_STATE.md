# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.3 — Deterministic simulation clock (complete)

Completed:
- M1.1 bootstrap: solution, Godot placeholder, architecture test, project-memory docs
- M1.2 domain model: `MatchState`, teams/players/stats with validation, ball, pitch, invariants
- M1.3: `MatchSimulation` (`Step`, `RunToCompletion`), `MatchConfig`, `MatchClock`/`MatchTime`, `IMatchRandom`/`MatchRng` (xoshiro256**), phases Kickoff → OpenPlay → Finished, coin-toss kickoff, `MatchEvent` log, `MatchResult`
- Static 4-3-3 kickoff layout (`Formation433`) pulled forward from M1.4
- Guards: determinism test (full-match fingerprint), non-deterministic type ban, pinned RNG sequence
- ADR-009 (RNG), ADR-010 (Step after finish throws)

In progress:
- nothing

Known issues:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed (not needed until presentation work)
- Players are idle during open play; every match is 0–0

Next recommended work:
- M1.4 — movement (Pace + fatigue), movement targets, in/out-of-possession 4-3-3 shape, goalkeeper positioning, shape tests

Last verified:
2026-10-02 — `dotnet build` (0 warnings) and `dotnet test` (88/88 passing).
