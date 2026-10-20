using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Crossing;

public enum CrossKind
{
    OpenPlay,
    Corner,
}

/// <summary>The ball carrier's wish to cross: deliver the ball to a spot in the box (ADR-036).</summary>
public sealed record CrossIntent(PlayerId Crosser, CrossKind Kind = CrossKind.OpenPlay) : ActionIntent(Crosser);

/// <summary>A resolved cross, struck now; the aerial contest is decided where it lands (ADR-036).</summary>
public sealed record CrossResult(
    TeamSide Team,
    PlayerId Crosser,
    CrossKind Kind,
    Vec2 StartPosition,
    Vec2 IntendedTarget,
    Vec2 TargetPosition,
    double TravelDuration)
{
    /// <summary>Distance from the kick to where the ball lands, in metres.</summary>
    public double Distance => StartPosition.DistanceTo(TargetPosition);
}
