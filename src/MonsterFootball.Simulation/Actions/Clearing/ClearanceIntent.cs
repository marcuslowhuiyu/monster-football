using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Clearing;

/// <summary>The ball carrier's wish to clear the ball: kick it to a spot, with no target player (D6, ADR-035).</summary>
public sealed record ClearanceIntent(PlayerId Kicker) : ActionIntent(Kicker);

public enum ClearanceKind
{
    /// <summary>Towards the nearest touchline, under pressure near the clearer's own goal.</summary>
    Wide,

    /// <summary>Upfield towards the clearer's striker.</summary>
    Long,
}

/// <summary>A resolved clearance, decided when the ball is struck and applied when it lands (ADR-012).</summary>
public sealed record ClearanceResult(
    TeamSide Team,
    PlayerId Kicker,
    ClearanceKind Kind,
    Vec2 StartPosition,
    Vec2 IntendedTarget,
    Vec2 TargetPosition,
    double TravelDuration)
{
    /// <summary>Distance from the kick to where the ball lands, in metres.</summary>
    public double Distance => StartPosition.DistanceTo(TargetPosition);
}
