# Monster Football — Agent Instructions

Fantasy football management/deckbuilding game. Current focus: Milestone 2 — match flow (laws, stoppages, restarts), tactical understanding and a Godot developer viewer, on top of the headless deterministic simulation from Milestone 1.

## Core Project Rules

- Simulation is authoritative.
- Presentation never determines football outcomes.
- Core simulation must run headlessly.
- `src/MonsterFootball.Simulation` must never reference Godot or `godot/` (enforced by tests).
- All simulation randomness must use MatchRng (`IMatchRandom`). Never use `System.Random`, `Guid.NewGuid`, or wall-clock time in simulation code (enforced by tests).
- Changing the order of random draws changes every seeded match — do it deliberately.
- Tune behaviour with `batch` runs, never single matches; a failing health band is a failing build (ADR-020).
- The simulation must remain deterministic by seed.
- Use the approved player stat model only (ADR-002).
- Do not implement systems belonging to future milestones.
- Prefer composition over deep inheritance.
- Add automated tests for football resolution systems.
- Read `docs/CURRENT_STATE.md` before beginning substantial work.
- Read relevant architecture/design documentation before modifying a system.
- Update `docs/CURRENT_STATE.md` after substantial completed work.
- Record significant architectural or design decisions in `docs/DECISIONS.md`.
- Do not silently override an accepted decision.

## Working procedure

Inspect → Plan → Implement → Test → Review invariants → Update docs → Update `CURRENT_STATE.md` → Commit → PR.

- **Never commit to `main`.** Start each subtask on a branch: `feat/m1-9-utility-ai`, `fix/…`, `chore/…`, `docs/…`.
- Commits: small, one system per commit, conventional style (`feat(sim): ...`, `test(pass): ...`, `docs(sim): ...`). **Every commit must build.**
- Before pushing: `dotnet format MonsterFootball.slnx --verify-no-changes`, `dotnet build`, `dotnet test`.
- Open a PR (`gh pr create`, fill in the template), wait for CI to pass, then **rebase-and-merge** (ADR-017). CI runs on PRs only (not on push to `main`) to save Actions minutes, so rebase the branch on current `main` before merging; if `main` moved since the last green run, let CI run again. Docs-only PRs skip CI.
- Adding a NuGet package updates `packages.lock.json` — commit it.

## Commands

The .NET SDK is installed at `~/.dotnet` (add it to `PATH` if `dotnet` is not found).

```sh
dotnet build MonsterFootball.slnx
dotnet test MonsterFootball.slnx
dotnet format MonsterFootball.slnx --verify-no-changes
dotnet run --project src/MonsterFootball.DebugRunner -- match 7 [--all]
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000   # must print HEALTHY
dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3
tools/viewer.sh --seed=7 --autoplay            # Godot developer viewer (needs Godot 4.7.2 .NET; see README)
```

## Key docs

- `docs/milestones/MILESTONE_2.md` — current milestone spec (Milestone 1: `MILESTONE_1.md`, complete)
- `docs/ARCHITECTURE.md`, `docs/MATCH_SIMULATION.md`, `docs/TACTICAL_ANALYSIS.md`, `docs/DECISIONS.md`, `docs/GLOSSARY.md`
