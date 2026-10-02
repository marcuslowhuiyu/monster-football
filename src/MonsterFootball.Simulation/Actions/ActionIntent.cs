using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions;

/// <summary>Something a player wishes to attempt. Resolvers turn intents into results.</summary>
public abstract record ActionIntent(PlayerId Actor);
