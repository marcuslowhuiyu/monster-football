using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Core;

/// <summary>A touch of the ball: who touched it and for which team.</summary>
public readonly record struct BallTouch(PlayerId Player, TeamSide Team);
