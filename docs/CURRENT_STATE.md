# Current Development State

Current milestone:
M2 — Match Flow, Tactical Understanding & Developer Match Viewer — **in progress** (`milestones/MILESTONE_2.md`, plan `milestones/MILESTONE_2_PLAN.md`)
M1 — Deterministic Match Simulation — complete (see `milestones/MILESTONE_1_ACCEPTANCE.md`)

Current subtask:
M2.2 frames, playback library and basic Godot viewer (ADR-025, ADR-026) — done in PR; next M2.3 tactical zones

What exists:
- Headless deterministic 11v11 simulation: 4-3-3 shape, movement, passing (ball flight, interceptions), carrying, two-roll tackling, shooting and goalkeeping, stamina/fatigue, utility AI on and off the ball
- Match flow (M2.1): possession causes, shots in flight, stoppages and restarts (kickoffs), two halves with added time ending at a stoppage, ball-in-play statistics
- Observation and viewer (M2.2): immutable `MatchFrame`s, `MonsterFootball.Playback`, Godot developer viewer (`tools/viewer.sh`)
- `MatchResult` with events, final state and statistics; `DebugRunner` `match`, `batch`, `fingerprint`
- 327 tests; CI on Ubuntu/Windows/macOS with format check, cross-platform determinism and a 1,000-match health-checked batch
- Decisions ADR-001 – ADR-026

In progress:
- nothing

Known issues (calibration, all within health bands):
- Strikers score ~82 % of goals
- ~1,670 passes and ~295 tackles per 90 continuous minutes (busy)
- 15.3 km per outfield player (real ≈ 10–12 km)
- Shot volume is steep in `ShotBase`
- The ball never goes out of play; misses go to the keeper — so ~96 % of halves end at the (interim, 60 s) safety cap and outfield distance (15.8 km) is close to its 16 km band
- Test suite with coverage takes ~4.5 min in CI

Environment notes:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot 4.7.2 .NET (mono) is at `~/Downloads/Godot_mono.app`; `godot/` opens and builds in it headlessly (M2.1e) **only with `DOTNET_ROOT=$HOME/.dotnet`**: `/etc/dotnet/install_location` points at an x64 .NET in `/usr/local/share/dotnet/x64`, which the arm64 Godot cannot load (see README)
- Branch protection unavailable on the current GitHub plan for this private repo; PR workflow enforced by convention (ADR-017)
- Open Dependabot PRs: test tooling updates (#4, #5, #7)

Decisions taken for M2 (to be recorded as ADRs when implemented):
- Tactical analysis, the viewer and debug tools read immutable per-tick `MatchFrame`s, never the live `MatchState` (accepted 2026-10-05)
- Periods end only at a stoppage after added time (replaces "match ends at exactly 90:00")

Next recommended work:
- Watch matches: `tools/viewer.sh --seed=7 --autoplay`
- Continue the M2 plan: M2.3 tactical zones (and the tackle danger-zone fix)
- Do not tune the M1 calibration issues before stoppages exist (M2 §47)

Last verified:
2026-10-05 — M2 baseline on `main` @ `6e4e799`: Release build 0 warnings, 269/269 tests, format clean, `match 7`, `fingerprint 1 2 3` and `batch 1000` (HEALTHY, figures identical to M1 acceptance).
