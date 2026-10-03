using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Carrying;

/// <summary>The ball carrier's wish to run with the ball toward <see cref="Destination"/>.</summary>
public sealed record CarryIntent(PlayerId Carrier, Vec2 Destination) : ActionIntent(Carrier);

/// <summary>A planned carry: where, how fast, until when. The carrier keeps the ball throughout.</summary>
public sealed record CarryResult(
    PlayerId Carrier,
    Vec2 StartPosition,
    Vec2 TargetPosition,
    double Speed,
    double Duration,
    long StartTick,
    long EndTick,
    double Quality);
