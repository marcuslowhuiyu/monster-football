using System.Collections.Immutable;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Observation;

namespace MonsterFootball.Playback;

/// <summary>What to draw at a moment between two frames: interpolated positions, discrete state from the earlier frame.</summary>
public sealed record ViewState(MatchFrame Frame, ImmutableArray<Vec2> PlayerPositions, Vec2 BallPosition, double Tick);

/// <summary>Linear interpolation between consecutive frames. Never invents football: positions only.</summary>
public static class FrameInterpolator
{
    public static ViewState Interpolate(MatchFrame from, MatchFrame to, double fraction)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var t = Math.Clamp(fraction, 0, 1);
        static Vec2 Lerp(Vec2 a, Vec2 b, double t) => a + (b - a) * t;

        var players = ImmutableArray.CreateBuilder<Vec2>(from.Players.Length);
        for (var i = 0; i < from.Players.Length; i++)
            players.Add(Lerp(from.Players[i].Position, to.Players[i].Position, t));

        return new ViewState(from, players.MoveToImmutable(), Lerp(from.Ball.Position, to.Ball.Position, t), from.Tick + t);
    }
}
