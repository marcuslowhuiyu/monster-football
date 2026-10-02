# Match Simulation

Detailed documentation of the football simulation. Update whenever the simulation architecture materially changes.

**Status: not yet implemented.** The sections below record the agreed design targets from `milestones/MILESTONE_1.md`; each will be replaced with a description of the actual implementation as the subtask lands.

| Topic | Target design | Subtask |
|---|---|---|
| Fixed timestep | Configurable; default 0.25 simulated seconds (4 ticks = 1 s). Independent of framerate. | M1.3 |
| Match state | Single authoritative `MatchState`: clock, score, teams, players, ball, possession, phase. | M1.2 |
| Player state | `PlayerDefinition` (persistent: id, name, position, stats) vs `PlayerMatchState` (mutable: position, target, stamina, ball, intent). | M1.2 |
| Ball state | Position, possessing player, loose flag. Results carry presentation data (start, target, duration, quality). | M1.2 |
| Pitch | Continuous coordinates, 105 × 68. Home attacks +X, away attacks −X. | M1.2 |
| Phases | Kickoff, OpenPlay, GoalRestart, Finished. | M1.3 |
| Action pattern | Intent → Resolver → Result → state update → event. | M1.5–M1.7 |
| Utility AI | Score each valid action; choose high-utility with controlled deterministic randomness. | M1.9 |
| Events | Immutable `MatchEvent` records in an ordered log. | M1.3+ |
| Deterministic RNG | One seeded `IMatchRandom` owned by the match. | M1.3 |

## Current limitations

Everything — the simulation has no runtime code yet (M1.1 bootstrap only).
