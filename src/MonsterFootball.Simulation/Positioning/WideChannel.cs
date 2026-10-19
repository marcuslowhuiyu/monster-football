using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// The wide channel (ADR-034): when their team has the ball, wide players — LW, RW, LB, RB — stay near their own
/// touchline. Team frame: LW and LB belong to the left touchline (Y = width), RW and RB to the right one (Y = 0).
/// Roles decide, not listed positions (ADR-031).
/// </summary>
internal static class WideChannel
{
    public static bool IsWide(PlayingPosition role) =>
        role is PlayingPosition.LW or PlayingPosition.RW or PlayingPosition.LB or PlayingPosition.RB;

    /// <summary>The Y of <paramref name="role"/>'s touchline in team frame. Wide roles only.</summary>
    public static double TouchlineY(PitchGeometry pitch, PlayingPosition role) => role switch
    {
        PlayingPosition.LW or PlayingPosition.LB => pitch.Width,
        PlayingPosition.RW or PlayingPosition.RB => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Only wide roles have a touchline."),
    };

    /// <summary>
    /// Moves <paramref name="local"/> (team frame) in Y so it is at most <paramref name="channelMetres"/> from the
    /// role's touchline. Central roles are returned unchanged.
    /// </summary>
    public static Vec2 Clamp(PitchGeometry pitch, PlayingPosition role, Vec2 local, double channelMetres)
    {
        if (!IsWide(role))
            return local;

        var touchline = TouchlineY(pitch, role);
        var y = touchline == 0 ? Math.Min(local.Y, channelMetres) : Math.Max(local.Y, touchline - channelMetres);
        return new Vec2(local.X, y);
    }
}
