using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Tactical;

/// <summary>
/// Pure, input-agnostic tactical geometry (MILESTONE_2 §22): plain positions in, numbers out. The laws and, later,
/// tactical analysis use the same functions so each football concept is implemented once. Grows in M2.8.
/// </summary>
public static class Spatial
{
    /// <summary>How many of <paramref name="defenders"/> are goal-side of <paramref name="attacker"/>: nearer <paramref name="goal"/>.</summary>
    public static int GoalSideCount(Vec2 attacker, Vec2 goal, IEnumerable<Vec2> defenders)
    {
        var attackerDistance = attacker.DistanceTo(goal);
        return defenders.Count(d => d.DistanceTo(goal) < attackerDistance);
    }
}
