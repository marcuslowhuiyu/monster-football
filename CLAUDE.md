# Monster Football — Agent Instructions

Fantasy football management/deckbuilding game. Current focus: headless deterministic match simulation (Milestone 1).

## Core Project Rules

- Simulation is authoritative.
- Presentation never determines football outcomes.
- Core simulation must run headlessly.
- `src/MonsterFootball.Simulation` must never reference Godot or `godot/` (enforced by tests).
- All simulation randomness must use MatchRng (`IMatchRandom`). Never use `System.Random`, `Guid.NewGuid`, or wall-clock time in simulation code.
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

Inspect → Plan → Implement → Test → Review invariants → Update docs → Update `CURRENT_STATE.md` → Commit.

Commits: small, one system per commit, conventional style (`feat(sim): ...`, `test(pass): ...`, `docs(sim): ...`).

## Commands

The .NET SDK is installed at `~/.dotnet` (add it to `PATH` if `dotnet` is not found).

```sh
dotnet build MonsterFootball.slnx
dotnet test MonsterFootball.slnx
dotnet run --project src/MonsterFootball.DebugRunner
```

## Key docs

- `docs/milestones/MILESTONE_1.md` — current milestone spec
- `docs/ARCHITECTURE.md`, `docs/MATCH_SIMULATION.md`, `docs/DECISIONS.md`, `docs/GLOSSARY.md`
