# Monster Football — Progress Report

**Period:** 2 – 5 October 2026
**Scope:** Milestone 1 — Deterministic Match Simulation Foundation (`docs/milestones/MILESTONE_1.md`)
**Status:** Milestone 1 complete. All acceptance criteria are met, with the caveats listed in §7 (`docs/milestones/MILESTONE_1_ACCEPTANCE.md`).
**Repository:** https://github.com/marcuslowhuiyu/monster-football (private) · 49 commits · 6 merged pull requests

---

## 1. Summary

Starting from the Milestone 1 specification in an empty folder, the project now has a headless, deterministic 11v11
football simulation in C#/.NET 10, alongside an empty Godot presentation project. The same seed plays a bit-identical
match on Ubuntu, Windows and macOS. Over 1,000 matches between evenly matched sides the simulation produces plausible
football, and a CI pipeline gates every change on tests, formatting, cross-platform determinism and a 1,000-match
health-checked batch.

| | |
|---|---|
| Simulation source | 67 files, ~3,700 lines of C# |
| Tests | 265 automated tests (~3,500 lines) |
| Documentation | `CLAUDE.md`, README, 7 project-memory docs, 20 decision records, acceptance review |
| CI | 6 jobs on every pull request |

## 2. What was built, subtask by subtask

| Subtask | Delivered |
|---|---|
| **M1.1** Bootstrap | Solution with the simulation library, debug runner, xUnit tests and an isolated Godot project (`godot/`). Architecture test: the simulation may never reference Godot. Project-memory docs and first decisions. |
| **M1.2** Domain model | `MatchState`, the nine-attribute `PlayerStats` (1–10), validated 4-3-3 teams, pitch geometry, ball. Possession is stored only on the ball. Reusable state invariants. |
| **M1.3** Clock and randomness | `MatchSimulation` with `Step()` / `RunToCompletion()`, a fixed 0.25 s tick, and a project-owned xoshiro256\*\* RNG (pinned and cross-checked). Match phases, coin-toss kickoff, an event log, and a guard test banning non-deterministic APIs. |
| **M1.4** Movement and shape | Pace- and fatigue-based movement. Ball-shifted 4-3-3 shape computed in team frame (mirror-symmetric): flat back four, single presser. Awareness-driven goalkeeper positioning. |
| **M1.5** Passing | Intent → resolver → result. Accuracy and interception rolls; the ball travels before the outcome applies. Misplaced passes, loose-ball collection, `PossessionChanged`. |
| **M1.6** Carrying and tackling | Carries that keep the ball. Two-roll tackles (Won / Lost / LooseBall). Intents are cleared centrally whenever the ball leaves a player. |
| **M1.7** Shooting and goalkeeping | On-target and save rolls (Keeping, Awareness, keeper positioning). Goals, saves, misses; reset and kickoff to the conceding side. |
| **M1.8** Stamina | A single fatigue model: drain from metres covered by intensity plus action costs read from events; the Stamina attribute slows drain; small speed and effectiveness penalties. |
| **M1.9** Utility AI | One selector picks among options within 8 points of the best, using the seeded RNG. On the ball: hold, pass, carry, shoot. Off the ball: support, forward runs, goal-side marking, recovery, pressing. All weights are data (`AiTuning`); a modifier hook is in place for future roles, traits and cards. |
| **M1.10** Full match | `MatchResult.Statistics` derived entirely from the event log. `DebugRunner match` prints a readable log and statistics table. |
| **M1.11** Batch simulation | `DebugRunner batch` (parallel, seed-ordered aggregation) with 13 pathology health bands; a 1,000-match CI job. Batch-driven tuning of movement, shape and AI. |
| **M1.12** Review | Docs audited against the code (17 corrections), decision records annotated, README verified from a fresh clone, acceptance review written. |

### Example output (`DebugRunner match 7`)

```text
02:15 Blue #10 shoots
02:15 GOAL — Blue #10 (1–0)
...
FULL TIME
Blue 2–1 Red
```

## 3. Results — 1,000 matches between equal teams

| Figure | Value |
|---|---|
| Goals per match | 2.91 |
| Shots / on target | 27.8 / 32 % |
| Passes, completion | 1,667, 82 % |
| Home win / draw / away win | 39.3 % / 22.9 % / 37.8 % |
| Home possession | 50.0 % |
| Distance per outfield player | 15.3 km |
| Health bands | 13 / 13 pass |

How the numbers moved during the project:

| Stage | Goals | Shots | Non-striker goals | Distance |
|---|---|---|---|---|
| Placeholder decisions (M1.7–M1.8) | 2.0 | 34 | 0 % | 16–18 km |
| Utility AI (M1.9) | 2.3 | 43 | ~20 % | 16–21 km |
| Batch-tuned (M1.11) | 2.9 | 28 | ~18 % | 15.3 km |

## 4. Key decisions

All twenty are recorded in `docs/DECISIONS.md`. The most consequential:

- **ADR-001/003 — The simulation is authoritative and Godot-free.** It is a plain .NET library, and a test enforces the dependency rule.
- **ADR-007 — Possession lives only on the ball.** "Has ball", the possession team and the possessor are derived from it, so they can never disagree.
- **ADR-009/013 — Determinism by construction.** The project owns its RNG algorithm, so seeds survive .NET upgrades. Outcome maths avoids `exp`/`sin`/`cos`, so every platform produces the same results; CI verifies this.
- **ADR-012 — Passes are decided at the kick and applied on arrival.** The presentation layer gets the true result to animate.
- **ADR-018 — Utility AI with bounded randomness and weights as data.**
- **ADR-019 — Statistics are derived from events.** Events are the single source of truth.
- **ADR-017/020 — Every change goes through a PR gated by multi-OS CI, a determinism check and a 1,000-match health check.**

## 5. Engineering process

- **Workflow:** feature branch → small conventional commits that each build and pass tests → PR from a template → CI green → rebase-merge.
- **CI** (`.github/workflows/ci.yml`):
  - format check;
  - Release build and full tests on Ubuntu, Windows and macOS;
  - cross-platform determinism comparison;
  - 1,000-match batch.
- **Reproducibility and supply chain:**
  - pinned SDK (`global.json`) and locked NuGet restores;
  - `.editorconfig` enforced in the build;
  - CI actions pinned to commit SHAs;
  - Dependabot for dependency updates.
- **Project memory:** `CLAUDE.md` holds rules for coding agents; `docs/CURRENT_STATE.md` holds short-term state; ADRs hold history.
- **Reusable skill:** `~/.claude/skills/shipping-changes-via-pr-and-ci` captures this workflow for future projects.

## 6. Problems encountered and how they were handled

| Problem | Resolution |
|---|---|
| The first 28 commits went straight to `main`, with no CI. | Added the full pipeline in PR #1, adopted the branch/PR workflow (ADR-017), recorded the lesson as a reusable skill. |
| Some commits were split so they didn't build on their own. | Rebuilt those local commits; every commit is now verified in a temporary worktree before pushing. |
| The determinism guard failed only in CI: coverage instrumentation injects `DateTime`/`Guid` references. | Reproduced it locally, then pointed the guard at the uninstrumented assembly. The workflow now says to run exactly what CI runs. |
| New state invariants caught real bugs, e.g. a carry surviving the loss of the ball. | Fixed centrally in `MatchState`, so no code path can skip it. |
| Placeholder rules produced threshold cliffs (6 vs 34 shots from a 0.01 change), striker-only scoring and tackle storms. | Replaced them with continuous utility scoring (M1.9). |
| Hand-tuning one constant at a time oscillated (0.7 to 10 goals per match). | Made the weights data, ran random searches, then 1,000-match batches. |
| Players ran 18–21 km per match. | Diagnosed by breaking distance down per behaviour: the team shape chased every square pass. Fixed with arrival slowdown and a per-team smoothed shape focus. |
| Branch protection is unavailable for this private repo on the current GitHub plan. | Restricted merges to rebase-only with branch auto-delete; documented in ADR-017. |

## 7. Known limitations

All of these are within the health bands:

1. Strikers score about 82 % of goals.
2. Matches are busy: about 1,670 passes and 295 tackles per 90 continuous minutes (no stoppages are simulated).
3. Outfield players run 15.3 km (real football ≈ 10–12 km).
4. Shot volume is very sensitive to one AI weight (`ShotBase`).
5. The ball never goes out of play; misses go to the goalkeeper (set pieces are out of scope for M1).
6. The Godot project builds in CI but has never been opened in the editor (not installed).
7. Branch protection depends on a GitHub plan upgrade or making the repo public.
8. Three Dependabot PRs (#4, #5, #7) are awaiting review.

## 8. How to use it

```sh
export PATH="$HOME/.dotnet:$PATH"                                       # SDK installed in ~/.dotnet
dotnet test MonsterFootball.slnx                                        # 265 tests
dotnet run --project src/MonsterFootball.DebugRunner -- match 7         # one match: log + statistics
dotnet run --project src/MonsterFootball.DebugRunner -c Release -- batch 1000   # 1,000 matches + health checks
```

## 9. Recommended next steps

1. **Write the Milestone 2 specification.** The game design points to Opportunity detection, the card/Playbook system (squad-as-deck) or the Godot 2.5D presentation.
2. **Optional calibration pass** on the limitations above, driven by batch runs.
3. **Housekeeping:** review the Dependabot PRs; add `dotnet` to the shell `PATH`; decide on branch protection.
