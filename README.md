# Monster Football

[![CI](https://github.com/marcuslowhuiyu/monster-football/actions/workflows/ci.yml/badge.svg)](https://github.com/marcuslowhuiyu/monster-football/actions/workflows/ci.yml)

A fantasy football management/deckbuilding game built in Godot 4 (.NET / C#).

The core is a headless, deterministic 11v11 match simulation (Milestone 1). Godot is only the presentation layer.

## Status

**Milestone 1 (deterministic 11v11 simulation) is complete** — see `docs/milestones/MILESTONE_1_ACCEPTANCE.md`.
Milestone 2 (match flow, tactical understanding, developer viewer) is specified in `docs/milestones/MILESTONE_2.md`.
Headless 4-3-3 football with passing, carrying, tackling, shooting, goalkeeping, restarts, stamina and utility AI;
match statistics and a readable debug log; 300+ automated tests; CI on three operating systems with a
cross-platform determinism check and a 1,000-match health-checked batch.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- Godot **4.7.2 .NET** edition ("mono"; must match `Godot.NET.Sdk/4.7.2`) — for the developer viewer in `godot/` (Milestone 2)

If the SDK was installed with Microsoft's `dotnet-install.sh` to `~/.dotnet`, put it on your `PATH`:

```sh
export PATH="$HOME/.dotnet:$PATH"
```

### Opening the Godot project

Godot finds .NET through `DOTNET_ROOT`, then the machine-wide `/etc/dotnet/install_location`. On Apple Silicon that
location can point at an Intel (x64) install, which Godot cannot load (`libhostfxr.dylib … incompatible architecture`).
Either install the official Arm64 .NET 10 SDK (it registers itself in `/usr/local/share/dotnet`), or launch Godot with
`DOTNET_ROOT` set to an Arm64 SDK:

```sh
DOTNET_ROOT="$HOME/.dotnet" /path/to/Godot_mono.app/Contents/MacOS/Godot --editor --path godot
# headless checks: --headless --editor --path godot --quit   |   --headless --path godot --build-solutions --quit
```

Launching from Finder does not pass `DOTNET_ROOT`; use the command above, or `launchctl setenv DOTNET_ROOT ~/.dotnet`
(lasts until logout).

## Repository layout

```text
src/MonsterFootball.Simulation        headless simulation (no Godot dependency)
src/MonsterFootball.DebugRunner       console runner for matches and batches
tests/MonsterFootball.Simulation.Tests xUnit tests
godot/                                Godot developer viewer (Milestone 2; currently empty)
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

## Formatting

```sh
dotnet format MonsterFootball.slnx                       # fix
dotnet format MonsterFootball.slnx --verify-no-changes   # check (as CI does)
```

## Checking determinism

```sh
dotnet run --project src/MonsterFootball.DebugRunner -- fingerprint 1 2 3
```

Prints a SHA-256 per seed. The same seed must give the same hash on every machine; CI compares Ubuntu, Windows and macOS.

## Running one match

```sh
dotnet run --project src/MonsterFootball.DebugRunner -- match 7          # highlights + statistics
dotnet run --project src/MonsterFootball.DebugRunner -- match 7 --all    # every event
```

Prints a readable log (`04:31 Blue #6 passes to Blue #9`, `27:51 GOAL — Blue #10 (1–0)`), the final score and a statistics table. Under each pass, carry, tackle and shot an indented line shows the ratings and odds the simulation used (e.g. `accuracy 85% · success 34%`, `on target 39% · save 68% · xG 0.12`); add `--brief` to hide them.

## Running batch simulations

```sh
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000          # seeds 1–1000
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 200 --from 5000
```

Plays the matches in parallel, prints averages (goals, shots, passes, pass completion, home/draw/away %, possession, tackles, distance…) and pathology health checks. Exits 1 if any check fails; CI runs `batch 1000` on every pull request.

## Development workflow

Work on a branch, open a pull request, and merge (rebase-and-merge) once CI is green. CI runs on pull requests (docs-only changes skip it): formatting checks, a Release build and the full test suite on Ubuntu, Release builds on Windows and macOS, then checks that all three produced identical matches and that a 1,000-match batch is healthy. See ADR-017 in `docs/DECISIONS.md`.

The SDK version is pinned in `global.json`; NuGet dependencies are locked in `packages.lock.json`.

## Documentation

Start with `CLAUDE.md` and `docs/CURRENT_STATE.md`. Decisions are in `docs/DECISIONS.md`.
