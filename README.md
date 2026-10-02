# Monster Football

A fantasy football management/deckbuilding game built in Godot 4 (.NET / C#).

The core is a headless, deterministic 11v11 match simulation (Milestone 1). Godot is only the presentation layer.

## Status

Milestone 1 (deterministic 11v11 simulation) — subtasks M1.1–M1.8 complete:
project setup, domain model, seeded clock/RNG/events, 4-3-3 movement and shape, passing,
carrying, tackling, shooting/goalkeeping/restarts, stamina. 220 automated tests passing.

Next: M1.9 utility AI, then full-match results/statistics (M1.10), 1,000-match batch runs (M1.11)
and the final review (M1.12). See `docs/CURRENT_STATE.md` for details and known issues.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- *(Optional, not needed for Milestone 1)* Godot 4.7 .NET edition — only to open the presentation project in `godot/`

If the SDK was installed with Microsoft's `dotnet-install.sh` to `~/.dotnet`, put it on your `PATH`:

```sh
export PATH="$HOME/.dotnet:$PATH"
```

## Repository layout

```text
src/MonsterFootball.Simulation        headless simulation (no Godot dependency)
src/MonsterFootball.DebugRunner       console runner for matches and batches
tests/MonsterFootball.Simulation.Tests xUnit tests
godot/                                Godot presentation project (placeholder)
docs/                                 design, architecture, decisions, current state
```

## Building

```sh
dotnet build MonsterFootball.slnx
```

## Running tests

```sh
dotnet test MonsterFootball.slnx
```

## Running one match

```sh
dotnet run --project src/MonsterFootball.DebugRunner
```

*Not yet implemented — arrives in M1.10.*

## Running batch simulations

*Not yet implemented — arrives in M1.11.*

## Documentation

Start with `CLAUDE.md` and `docs/CURRENT_STATE.md`. Decisions are in `docs/DECISIONS.md`.
