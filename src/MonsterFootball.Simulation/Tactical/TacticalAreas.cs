namespace MonsterFootball.Simulation.Tactical;

/// <summary>
/// Overlapping tactical areas, relative to the analysing team. A point can be in several at once (e.g. opposition
/// half, final third, opposition penalty area and central penalty area), so these are flags rather than a type
/// hierarchy. Penalty-area and goal-area lines belong to the area; the halfway line belongs to each team's own half,
/// as in Law 11. <see cref="CentralPenaltyArea"/> and <see cref="WidePenaltyArea"/> split the opposition penalty
/// area at the width of the goal area.
/// </summary>
[Flags]
public enum TacticalAreas
{
    None = 0,
    OwnHalf = 1 << 0,
    OppositionHalf = 1 << 1,
    FinalThird = 1 << 2,
    OwnPenaltyArea = 1 << 3,
    OppositionPenaltyArea = 1 << 4,
    OwnGoalArea = 1 << 5,
    OppositionGoalArea = 1 << 6,
    CentralPenaltyArea = 1 << 7,
    WidePenaltyArea = 1 << 8,
}
