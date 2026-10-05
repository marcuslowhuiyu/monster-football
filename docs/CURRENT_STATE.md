# Current Development State

Current milestone:
M1 — Deterministic Match Simulation — **complete** (see `milestones/MILESTONE_1_ACCEPTANCE.md`)

Current subtask:
none — awaiting the Milestone 2 specification

What exists:
- Headless deterministic 11v11 simulation: 4-3-3 shape, movement, passing (ball flight, interceptions), carrying, two-roll tackling, shooting and goalkeeping, goals and restarts, stamina/fatigue, utility AI on and off the ball
- `MatchResult` with events, final state and statistics; `DebugRunner` `match`, `batch`, `fingerprint`
- 265 tests; CI on Ubuntu/Windows/macOS with format check, cross-platform determinism and a 1,000-match health-checked batch
- Decisions ADR-001 – ADR-020

In progress:
- nothing

Known issues (calibration, all within health bands):
- Strikers score ~82 % of goals
- ~1,670 passes and ~295 tackles per 90 continuous minutes (busy)
- 15.3 km per outfield player (real ≈ 10–12 km)
- Shot volume is steep in `ShotBase`
- The ball never goes out of play; misses go to the keeper
- Test suite with coverage takes ~4.5 min in CI

Environment notes:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot editor not installed; `godot/` has only been built, never opened
- Branch protection unavailable on the current GitHub plan for this private repo; PR workflow enforced by convention (ADR-017)
- Open Dependabot PRs: test tooling updates (#4, #5, #7)

Next recommended work:
- Write the Milestone 2 specification (candidates from the game design: Opportunity detection, cards/Playbook, squad-as-deck, presentation)
- Optionally address the calibration issues above with batch-driven tuning

Last verified:
2026-10-05 — fresh clone following README: build 0 warnings, 265/265 tests, format clean, `match`, `fingerprint` and `batch` (HEALTHY) all working.
