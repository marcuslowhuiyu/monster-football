# Current Development State

Current milestone:
M2 — Match Flow, Tactical Understanding & Developer Match Viewer — **in progress** (`milestones/MILESTONE_2.md`, plan `milestones/MILESTONE_2_PLAN.md`)
M1 — Deterministic Match Simulation — complete (see `milestones/MILESTONE_1_ACCEPTANCE.md`)

Current subtask:
M2.7b match realism (aiming at real football) — step 1 width (ADR-034, `feat/m2-7b-width`); next clearances (D6), crosses, dead-ball times; then M2.8 tactical geometry

What exists:
- Headless deterministic 11v11 simulation: 4-3-3 shape, movement, passing (ball flight, interceptions), carrying, two-roll tackling, shooting and goalkeeping, stamina/fatigue, utility AI on and off the ball
- Match flow (M2.1): possession causes, shots in flight, stoppages and restarts (kickoffs), two halves with added time ending at a stoppage, ball-in-play statistics
- Width (M2.7b): wingers and full-backs hold their touchline in possession (shape, runs, support, carries)
- Cards and reduced teams (M2.7): yellow/red cards under Law 12 (DOGSO, serious foul play, promising attack, reckless, second yellow), dismissals with reduced-team shapes, abandonment below seven players
- Fouls (M2.6): fouls from tackles with severity, direct free kicks (wall, shoot or pass), penalties
- Offside (M2.5): judged at the pass, penalised on involvement, indirect free kicks; offside-aware passing and runs
- Ball out of play (M2.4): last touch, straight-path crossings, rolling misplaced passes, held/parried saves, deflections; throw-ins, goal kicks and corners (`docs/LAWS_OF_THE_GAME.md`)
- Tactical zones (M2.3): 20 team-relative zones and overlapping areas (`MatchState.Zones`, `docs/TACTICAL_ANALYSIS.md`)
- Observation and viewer (M2.2): immutable `MatchFrame`s, `MonsterFootball.Playback`, Godot developer viewer (`tools/viewer.sh`)
- `MatchResult` with events, final state and statistics; `DebugRunner` `match`, `batch`, `fingerprint`
- 477 tests; CI on Ubuntu/Windows/macOS with format check, cross-platform determinism and a 1,000-match health-checked batch
- Decisions ADR-001 – ADR-034

In progress:
- nothing

Known issues (calibration, all within health bands):
- Goals ~1.95 per match since width (real ≈ 2.7): wide attacks have no crosses yet (M2.7b step 3)
- Strikers score ~76 % of goals; full-backs receive under 1 % of passes each
- ~1,370 passes and ~265 tackles per match (busy)
- 14.0 km per outfield player (real ≈ 10–12 km)
- Shot volume is steep in `ShotBase`
- Stoppages: 23.4 throw-ins (real ≈ 40) per match; ball in play ~78 min (real 55–60); ~6 % of halves still end at the 180 s safety cap — interim bands until the M2.7b steps land (ADR-033)
- Test suite with coverage takes ~4.5 min in CI

Environment notes:
- `dotnet` is installed at `~/.dotnet` and may not be on `PATH` in every shell
- Godot 4.7.2 .NET (mono) is at `~/Downloads/Godot_mono.app`; `godot/` opens and builds in it headlessly (M2.1e) **only with `DOTNET_ROOT=$HOME/.dotnet`**: `/etc/dotnet/install_location` points at an x64 .NET in `/usr/local/share/dotnet/x64`, which the arm64 Godot cannot load (see README)
- Branch protection unavailable on the current GitHub plan for this private repo; PR workflow enforced by convention (ADR-017)
- The repository is **public until the 1 Nov Actions quota reset** (free minutes); then private again with cheaper CI

Decisions taken for M2 (to be recorded as ADRs when implemented):
- Tactical analysis, the viewer and debug tools read immutable per-tick `MatchFrame`s, never the live `MatchState` (accepted 2026-10-05)
- Periods end only at a stoppage after added time (replaces "match ends at exactly 90:00")

Next recommended work:
- Watch matches: `tools/viewer.sh --seed=7 --autoplay`
- M2.7b step 2: defensive clearances (D6); then crosses and dead-ball times, tightening the interim bands (ADR-033)
- Do not tune the M1 calibration issues before stoppages exist (M2 §47)

Last verified:
2026-10-19 — M2.7 on `main` @ `36bf1da`: CI green on #27 (Ubuntu tests, Windows/macOS fingerprints, cross-platform determinism, format, `batch 1000` HEALTHY); locally Release build 0 warnings, 453/453 tests.
