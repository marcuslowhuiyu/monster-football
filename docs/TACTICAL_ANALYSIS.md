# Tactical Analysis

How the simulation describes the pitch tactically. Grows through M2.3–M2.10 (geometry, snapshots, Opportunities);
only implemented parts are documented here.

## Coordinates (implemented)

Positions are continuous metres and are never snapped to a grid. Tactical classification happens in the analysing
team's **team frame**: own goal line at X = 0, attacking +X, left flank +Y (`Positioning.TeamFrame`). For Home the
team frame equals pitch coordinates; for Away it is rotated 180°. Everything below is written once, in team frame,
and Home/Away mirror exactly (tested over a 0.5 m grid).

## 20 primary zones (implemented, M2.3)

`TacticalZone = (LongitudinalBand, VerticalChannel)` — a composition, not a 20-value enum (ADR-027).

| | Left wide | Left half-space | Centre | Right half-space | Right wide |
|---|---|---|---|---|---|
| **Attacking** (75–100 %) | | | | | |
| **Opposition midfield** (50–75 %) | | | | | |
| **Own midfield** (25–50 %) | | | | | |
| **Defensive** (0–25 %) | | | | | |

- Bands are fractions of pitch length from the team's own goal line; channels are fractions of width from its right
  touchline. Defaults: equal quarters and fifths. All splits live in `ZoneBoundaries` (`MatchConfig.Zones`), the one
  place to tune them; splits must increase strictly within (0, 1).
- A point on a boundary belongs to the band further from the team's own goal and the channel further left. Points
  off the pitch take the zone of the nearest point on it, so every position has exactly one valid zone.
- The same point has different zones for the two teams: (100, 2) is `Attacking / RightWide` for Home and
  `Defensive / LeftWide` for Away.

## Overlapping areas (implemented, M2.3)

`TacticalAreas` flags, relative to the analysing team; a point can be in several at once.

| Area | Definition (team frame) |
|---|---|
| `OwnHalf` / `OppositionHalf` | X ≤ halfway / X > halfway — the halfway line is in each team's own half (Law 11) |
| `FinalThird` | X ≥ ⅔ of pitch length |
| `OwnPenaltyArea` / `OppositionPenaltyArea` | 16.5 m deep × 40.32 m wide; lines included; agrees with `PitchGeometry.IsInPenaltyArea` |
| `OwnGoalArea` / `OppositionGoalArea` | 5.5 m deep × 18.32 m wide; lines included |
| `CentralPenaltyArea` / `WidePenaltyArea` | the opposition penalty area split at the goal area's width |

The laws use these as they arrive (penalty area, goal area, halves); the tackle danger bonus already uses
`FinalThird` (§6.5 of the milestone).

## API

`MatchState.Zones` (`TacticalZones`, built from `MatchConfig.Pitch` and `MatchConfig.Zones`):

- `ZoneOf(side, point)` → `TacticalZone`
- `AreasOf(side, point)` → `TacticalAreas`; `IsIn(side, point, area)`
- `BoundsOf(side, zone)` → pitch-coordinate rectangle, for overlays
- `FinalThirdX` — the final-third line in team frame; `DefensiveThirdX` — the end of a team's own third

Match report detail lines end with the acting player's zone, e.g. `… · xG 0.12 · Attacking / Centre`.
