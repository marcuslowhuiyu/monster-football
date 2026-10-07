using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// 4-3-3 anchor positions per slot, authored in team frame on a standard 105 × 68 pitch
/// (attacking +X, left flank +Y) and scaled to the actual pitch.
/// Three layouts: kickoff, in possession, out of possession. The in/out anchors assume the
/// ball is at the centre spot; <see cref="TeamShapeSystem"/> shifts them with the ball.
/// </summary>
public static class Formation433
{
    public static Vec2 KickoffPosition(PitchGeometry pitch, TeamSide side, PlayingPosition position) =>
        TeamFrame.Convert(pitch, side, Kickoff(pitch, position));

    internal static Vec2 Kickoff(PitchGeometry pitch, PlayingPosition position)
    {
        var (x, y) = position switch
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
            PlayingPosition.ST => (42, 34), // outside the centre circle; the kicking side's ST walks to the ball
            PlayingPosition.RW => (48, 8),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };
        return TeamFrame.FromStandard(pitch, x, y);
    }

    /// <summary>Attacking shape: CBs split, fullbacks push on, midfield staggered, wingers wide, ST high.</summary>
    internal static Vec2 InPossession(PitchGeometry pitch, PlayingPosition position)
    {
        var (x, y) = position switch
        {
            PlayingPosition.GK => (6, 34),
            PlayingPosition.LCB => (24, 45),
            PlayingPosition.RCB => (24, 23),
            PlayingPosition.LB => (38, 61),
            PlayingPosition.RB => (38, 7),
            PlayingPosition.CM => (42, 34),
            PlayingPosition.LCM => (54, 46),
            PlayingPosition.RCM => (54, 22),
            PlayingPosition.LW => (68, 63),
            PlayingPosition.ST => (72, 34),
            PlayingPosition.RW => (68, 5),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };
        return TeamFrame.FromStandard(pitch, x, y);
    }

    /// <summary>Defensive shape: flat back four, compact central midfield, forwards screening.</summary>
    internal static Vec2 OutOfPossession(PitchGeometry pitch, PlayingPosition position)
    {
        var (x, y) = position switch
        {
            PlayingPosition.GK => (5, 34),
            PlayingPosition.LB => (20, 55),
            PlayingPosition.LCB => (20, 41),
            PlayingPosition.RCB => (20, 27),
            PlayingPosition.RB => (20, 13),
            PlayingPosition.LCM => (33, 45),
            PlayingPosition.CM => (30, 34),
            PlayingPosition.RCM => (33, 23),
            PlayingPosition.LW => (45, 54),
            PlayingPosition.ST => (48, 34),
            PlayingPosition.RW => (45, 14),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };
        return TeamFrame.FromStandard(pitch, x, y);
    }
}
