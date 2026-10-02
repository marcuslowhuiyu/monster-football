using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// Converts between pitch coordinates and a team's own frame, in which the team
/// defends X = 0 and attacks +X with its left flank at +Y. For Home the frames are
/// identical; for Away the frame is rotated 180°. Shape logic is written once, in team frame.
/// </summary>
internal static class TeamFrame
{
    private const double StandardLength = 105;
    private const double StandardWidth = 68;

    /// <summary>The conversion is its own inverse, so this works in both directions.</summary>
    public static Vec2 Convert(PitchGeometry pitch, TeamSide side, Vec2 point) =>
        side == TeamSide.Home ? point : new Vec2(pitch.Length - point.X, pitch.Width - point.Y);

    /// <summary>Scales a point authored on a standard 105 × 68 pitch to the actual pitch.</summary>
    public static Vec2 FromStandard(PitchGeometry pitch, double x, double y) =>
        new(x * pitch.Length / StandardLength, y * pitch.Width / StandardWidth);
}
