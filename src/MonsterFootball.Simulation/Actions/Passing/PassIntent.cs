using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Passing;

/// <summary>A player's wish to pass to a teammate.</summary>
public sealed record PassIntent(PlayerId Passer, PlayerId Receiver);
