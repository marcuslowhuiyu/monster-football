# Current Development State

Current milestone:
M2 — Match Flow, Tactical Understanding & Developer Match Viewer — **in progress** (`milestones/MILESTONE_2.md`, plan `milestones/MILESTONE_2_PLAN.md`)
M1 — Deterministic Match Simulation — complete (see `milestones/MILESTONE_1_ACCEPTANCE.md`)

Current subtask:
M2.5 offside (ADR-029, `feat/m2-5-offside`), M2.6 fouls, free kicks, penalties (ADR-030, `feat/m2-6-fouls`) and M2.7 cards, reduced teams, calibration checkpoint (ADR-031–033, `feat/m2-7-discipline`) — stacked branches, PRs to open and merge in order; next D6 defensive clearances (triggered at the checkpoint), then M2.8 tactical geometry

What exists:
- Headless deterministic 11v11 simulation: 4-3-3 shape, movement, passing (ball flight, interceptions), carrying, two-roll tackling, shooting and goalkeeping, stamina/fatigue, utility AI on and off the ball
- Match flow (M2.1): possession causes, shots in flight, stoppages and restarts (kickoffs), two halves with added time ending at a stoppage, ball-in-play statistics
- Cards and reduced teams (M2.7): yellow/red cards under Law 12 (DOGSO, serious foul play, promising attack, reckless, second yellow), dismissals with reduced-team shapes, abandonment below seven players
- Fouls (M2.6): fouls from tackles with severity, direct free kicks (wall, shoot or pass), penalties
- Offside (M2.5): judged at the pass, penalised on involvement, indirect free kicks; offside-aware passing and runs
- Ball out of play (M2.4): last touch, straight-path crossings, rolling misplaced passes, held/parried saves, deflections; throw-ins, goal kicks and corners (`docs/LAWS_OF_THE_GAME.md`)
- Tactical zones (M2.3): 20 team-relative zones and overlapping areas (`MatchState.Zones`, `docs/TACTICAL_ANALYSIS.md`)
- Observation and viewer (M2.2): immutable `MatchFrame`s, `MonsterFootball.Playback`, Godot developer viewer (`tools/viewer.sh`)
- `MatchResult` with events, final state and statistics; `DebugRunner` `match`, `batch`, `fingerprint`
- 453 tests; CI on Ubuntu/Windows/macOS with format check, cross-platform determinism and a 1,000-match health-checked batch
- Decisions ADR-001 – ADR-033

In progress:
- nothing

Known issues (calibration, all within health bands):
- Strikers score ~70 % of goals
- ~1,470 passes and ~270 tackles per match (busy)
- 14.2 km per outfield player (real ≈ 10–12 km)
- Shot volume is steep in `ShotBase`
- Few stoppages: 8.1 throw-ins (real ≈ 40) per match; ball in play ~79 min (real 55–60); ~10 % of halves still end at the 180 s safety cap — interim bands until D6 clearances (ADR-033)
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
- Open the M2.5, M2.6 and M2.7 PRs (merge in order); then D6 defensive clearances
- Do not tune the M1 calibration issues before stoppages exist (M2 §47)

Last verified:
2026-10-05 — M2 baseline on `main` @ `6e4e799`: Release build 0 warnings, 269/269 tests, format clean, `match 7`, `fingerprint 1 2 3` and `batch 1000` (HEALTHY, figures identical to M1 acceptance).
