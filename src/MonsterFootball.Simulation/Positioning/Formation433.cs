using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// Static 4-3-3 kickoff layout. Anchors are authored for the Home side on a standard
/// 105 × 68 pitch (attacking +X, so the left flank is +Y), scaled to the actual pitch,
/// and rotated 180° for the Away side. Dynamic shape arrives in M1.4.
/// </summary>
public static class Formation433
{
    private const double StandardLength = 105;
    private const double StandardWidth = 68;

    public static Vec2 KickoffPosition(PitchGeometry pitch, TeamSide side, PlayingPosition position)
    {
        var (x, y) = HomeAnchor(position);
        var scaled = new Vec2(x * pitch.Length / StandardLength, y * pitch.Width / StandardWidth);
        return side == TeamSide.Home
            ? scaled
            : new Vec2(pitch.Length - scaled.X, pitch.Width - scaled.Y);
    }

    private static (double X, double Y) HomeAnchor(PlayingPosition position) => position switch
    {
        PlayingPosition.GK => (4, 34),
        PlayingPosition.LB => (20, 58),
        PlayingPosition.LCB => (16, 42),
        PlayingPosition.RCB => (16, 26),
        PlayingPosition.RB => (20, 10),
        PlayingPosition.LCM => (34, 46),
        PlayingPosition.CM => (30, 34),
        PlayingPosition.RCM => (34, 22),
        PlayingPosition.LW => (48, 60),
        PlayingPosition.ST => (49, 34),
        PlayingPosition.RW => (48, 8),
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };
}
