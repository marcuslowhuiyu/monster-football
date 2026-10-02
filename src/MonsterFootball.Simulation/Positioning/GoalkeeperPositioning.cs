using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// Goalkeeper target in team frame (own goal at X = 0).
/// Out of possession the keeper stands on the line from goal centre to ball; Awareness sets
/// how far off the line they come (low Awareness stays too deep). In possession the keeper
/// offers deep support. The keeper never leaves the penalty area.
/// </summary>
internal static class GoalkeeperPositioning
{
    private const double DepthPerMetreOfBallDistance = 0.15;
    private const double MinDepth = 1.0;
    private const double MaxDepth = 11.0;

    public static Vec2 Target(PitchGeometry pitch, Vec2 ballLocal, int awareness, bool inPossession)
    {
        var goal = new Vec2(0, pitch.Width / 2);
        Vec2 target;

        if (inPossession)
        {
            target = new Vec2(
                4 + 0.12 * ballLocal.X,
                goal.Y + 0.15 * (ballLocal.Y - goal.Y));
        }
        else
        {
            var awarenessFactor = 0.6 + 0.04 * awareness; // 0.64 at 1, 1.0 at 10
            var depth = Math.Clamp(goal.DistanceTo(ballLocal) * DepthPerMetreOfBallDistance, MinDepth, MaxDepth) * awarenessFactor;
            target = goal + (ballLocal - goal).Normalized() * depth;
        }

        return ClampToPenaltyArea(pitch, target);
    }

    private static Vec2 ClampToPenaltyArea(PitchGeometry pitch, Vec2 point)
    {
        const double margin = 0.5;
        var halfWidth = PitchGeometry.PenaltyAreaWidth / 2 - margin;
        return new Vec2(
            Math.Clamp(point.X, margin, PitchGeometry.PenaltyAreaDepth - margin),
            Math.Clamp(point.Y, pitch.Width / 2 - halfWidth, pitch.Width / 2 + halfWidth));
    }
}
