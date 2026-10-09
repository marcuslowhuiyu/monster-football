# Current Development State

Current milestone:
M2 — Match Flow, Tactical Understanding & Developer Match Viewer — **in progress** (`milestones/MILESTONE_2.md`, plan `milestones/MILESTONE_2_PLAN.md`)
M1 — Deterministic Match Simulation — complete (see `milestones/MILESTONE_1_ACCEPTANCE.md`)

Current subtask:
M2.4 ball out of play, throw-ins, goal kicks and corners (ADR-028) — done in PR; next M2.5 offside

What exists:
- Headless deterministic 11v11 simulation: 4-3-3 shape, movement, passing (ball flight, interceptions), carrying, two-roll tackling, shooting and goalkeeping, stamina/fatigue, utility AI on and off the ball
- Match flow (M2.1): possession causes, shots in flight, stoppages and restarts (kickoffs), two halves with added time ending at a stoppage, ball-in-play statistics
- Ball out of play (M2.4): last touch, straight-path crossings, rolling misplaced passes, held/parried saves, deflections; throw-ins, goal kicks and corners (`docs/LAWS_OF_THE_GAME.md`)
- Tactical zones (M2.3): 20 team-relative zones and overlapping areas (`MatchState.Zones`, `docs/TACTICAL_ANALYSIS.md`)
- Observation and viewer (M2.2): immutable `MatchFrame`s, `MonsterFootball.Playback`, Godot developer viewer (`tools/viewer.sh`)
- `MatchResult` with events, final state and statistics; `DebugRunner` `match`, `batch`, `fingerprint`
- 396 tests; CI on Ubuntu/Windows/macOS with format check, cross-platform determinism and a 1,000-match health-checked batch
- Decisions ADR-001 – ADR-028

In progress:
- nothing

Known issues (calibration, all within health bands):
- Strikers score ~75 % of goals
- ~1,560 passes and ~285 tackles per match (busy)
- 15.0 km per outfield player (real ≈ 10–12 km)
- Shot volume is steep in `ShotBase`
- Few stoppages: 8.7 throw-ins (real ≈ 40), 16.2 goal kicks, 6.4 corners per match; ball in play ~85 min (real 55–60); ~64 % of halves still end at the interim 60 s safety cap — calibration checkpoint at M2.7 (D6 clearances, wider play)
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
- Continue the M2 plan: M2.5 offside
- Do not tune the M1 calibration issues before stoppages exist (M2 §47)

Last verified:
2026-10-05 — M2 baseline on `main` @ `6e4e799`: Release build 0 warnings, 269/269 tests, format clean, `match 7`, `fingerprint 1 2 3` and `batch 1000` (HEALTHY, figures identical to M1 acceptance).
