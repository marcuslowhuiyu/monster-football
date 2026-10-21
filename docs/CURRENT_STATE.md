# Current Development State

Current milestone:
M2 — Match Flow, Tactical Understanding & Developer Match Viewer — **in progress** (`milestones/MILESTONE_2.md`, plan `milestones/MILESTONE_2_PLAN.md`)
M1 — Deterministic Match Simulation — complete (see `milestones/MILESTONE_1_ACCEPTANCE.md`)

Current subtask:
M2.7b match realism (aiming at real football) — steps 1–3 width, clearances and crosses merged (#32–#34); step 4 dead-ball times done (ADR-037, `feat/m2-7b-dead-ball-times`, provisional values, PR pending); next step 5, the calibration pass; then M2.8 tactical geometry

What exists:
- Headless deterministic 11v11 simulation: 4-3-3 shape, movement, passing (ball flight, interceptions), carrying, two-roll tackling, shooting and goalkeeping, stamina/fatigue, utility AI on and off the ball
- Match flow (M2.1): possession causes, shots in flight, stoppages and restarts (kickoffs), two halves with added time ending at a stoppage, ball-in-play statistics
- Dead-ball times (M2.7b): each stoppage draws its own dead-ball time (type average, spread, game state, location, cards); a leading side's wasted time is added to the added time
- Crosses (M2.7b): wide players cross into the box and corners are crosses; an aerial contest decides each (first-time shot, headed clearance, keeper claim, loose)
- Clearances (M2.7b): defenders under pressure in their own third clear wide or long; contested loose balls decided by a fair draw
- Width (M2.7b): wingers and full-backs hold their touchline in possession (shape, runs, support, carries)
- Cards and reduced teams (M2.7): yellow/red cards under Law 12 (DOGSO, serious foul play, promising attack, reckless, second yellow), dismissals with reduced-team shapes, abandonment below seven players
- Fouls (M2.6): fouls from tackles with severity, direct free kicks (wall, shoot or pass), penalties
- Offside (M2.5): judged at the pass, penalised on involvement, indirect free kicks; offside-aware passing and runs
- Ball out of play (M2.4): last touch, straight-path crossings, rolling misplaced passes, held/parried saves, deflections; throw-ins, goal kicks and corners (`docs/LAWS_OF_THE_GAME.md`)
- Tactical zones (M2.3): 20 team-relative zones and overlapping areas (`MatchState.Zones`, `docs/TACTICAL_ANALYSIS.md`)
- Observation and viewer (M2.2): immutable `MatchFrame`s, `MonsterFootball.Playback`, Godot developer viewer (`tools/viewer.sh`)
- `MatchResult` with events, final state and statistics; `DebugRunner` `match`, `batch`, `fingerprint`
- 572 tests; CI on Ubuntu/Windows/macOS with format check, cross-platform determinism and a 1,000-match health-checked batch
- Decisions ADR-001 – ADR-037

In progress:
- M2.7b step 4 dead-ball times — PR from `feat/m2-7b-dead-ball-times`

Known issues (calibration, all within health bands):
- Goals ~3.4 per match (real ≈ 2.7): crosses and dead-ball times are provisionally tuned; deliberately left for the M2.7b calibration pass
- Strikers score ~67 % of goals; full-backs receive under 1 % of passes each
- ~1,000 passes and ~200 tackles per match (busy)
- 11.6 km per outfield player (real ≈ 10–12 km)
- Shot volume is steep in `ShotBase`
- Stoppages: 22.3 throw-ins (real ≈ 40; shorter ball-in-play time lowered every in-play count), 10.2 corners, 39.1 clearances and 44.3 crosses per match; ball in play 61.3 min (real 55–60); ~2 % of halves still end at the 180 s safety cap — interim bands until the calibration pass (ADR-033)
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
- M2.7b step 5: the calibration pass (tune with all systems in place — goals, throw-ins, crosses, dead-ball times; tighten the interim bands, ADR-033); then M2.8
- Do not tune the M1 calibration issues before stoppages exist (M2 §47)

Last verified:
2026-10-19 — M2.7 on `main` @ `36bf1da`: CI green on #27 (Ubuntu tests, Windows/macOS fingerprints, cross-platform determinism, format, `batch 1000` HEALTHY); locally Release build 0 warnings, 453/453 tests.
