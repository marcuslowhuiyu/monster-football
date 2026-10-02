# Match Simulation

Detailed documentation of the football simulation. Update whenever the simulation architecture materially changes.

**Status:** domain model implemented (M1.2). No simulation loop yet. "Design targets" below lists agreed designs from `milestones/MILESTONE_1.md` that are not yet built.

## Pitch (implemented)

`PitchGeometry` — continuous coordinates in metres, default 105 × 68. Origin is a corner.

- X runs along the length. **Home defends X = 0 and attacks +X; Away defends X = 105 and attacks −X.** `PitchGeometry.AttackDirection(side)` returns ±1.
- Landmarks: `Centre`, `HalfwayX`, `OwnGoalCentre(side)`, `OpponentGoalCentre(side)`, `IsInPenaltyArea(point, defendingSide)`, `IsInOwnHalf`.
- Constants: goal width 7.32, penalty area 16.5 deep × 40.32 wide.
- `Contains` / `Clamp` for boundary checks. Zones are not modelled yet; add classification methods here when needed.

`Vec2` is a `double` value type. Only basic arithmetic and `Math.Sqrt` are used (IEEE-exact, deterministic on a given runtime). Avoid introducing trigonometric functions into outcome-affecting code without considering cross-platform determinism.

## Match state (implemented)

`MatchState.Create(home, away, pitch, startingPosition)` builds the authoritative state:

- validates that player ids are unique across both teams and that every starting position is on the pitch;
- `Players` is an ordered list of 22 `PlayerMatchState` — home then away, each in team-definition order. **Iterate this list, never a dictionary, when order can affect outcomes.**
- `Ball` starts loose at the centre spot; `Score` 0–0; `Phase` = `Kickoff`.
- The clock is added in M1.3.

## Player state (implemented)

- `PlayerDefinition` — immutable: `Id`, `Name`, `ShirtNumber`, `Position` (4-3-3 slot, ADR-008), `Stats`.
- `PlayerStats` — the nine ADR-002 attributes, each validated to 1–10.
- `PlayerMatchState` — mutable: `Position`, `MovementTarget`, `CurrentStamina` (0–100, starts at 100). `CurrentIntent` will be added with the intent system (M1.5).

## Ball and possession (implemented)

`BallState` holds `Position` and `PossessorId`; `IsLoose` is derived. Possession is stored **only** on the ball (ADR-007):
`MatchState.HasBall(id)`, `PossessionPlayerId` and `PossessionTeam` are computed from it, so two players can never both have the ball.
Ball-in-flight and presentation data (start/target/duration/quality) arrive with passing (M1.5).

## Invariants (implemented)

`MatchStateInvariants.FindViolations(state)` / `Verify(state)` check: 22 active players, 11 per side, unique ids, possessor is an active player, non-negative score, players on the pitch, stamina within bounds. Each message names the offending player. The "clock never decreases" invariant is added with the clock (M1.3).

## Design targets

| Topic | Target design | Subtask |
|---|---|---|
| Fixed timestep | Configurable; default 0.25 simulated seconds (4 ticks = 1 s). Independent of framerate. | M1.3 |
| Phases | Kickoff, OpenPlay, GoalRestart, Finished. | M1.3 |
| Action pattern | Intent → Resolver → Result → state update → event. | M1.5–M1.7 |
| Utility AI | Score each valid action; choose high-utility with controlled deterministic randomness. | M1.9 |
| Events | Immutable `MatchEvent` records in an ordered log. | M1.3+ |
| Deterministic RNG | One seeded `IMatchRandom` owned by the match. | M1.3 |

## Current limitations

- No simulation loop, clock, RNG, movement, actions, AI or events yet.
- Starting positions are supplied by the caller; the 4-3-3 formation layout arrives in M1.4.
