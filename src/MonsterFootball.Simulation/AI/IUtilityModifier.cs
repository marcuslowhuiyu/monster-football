using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Extension point for future utility adjustments from roles, traits, tactics, manager instructions,
/// species and cards (milestone §14). Returns an amount to add to the candidate's utility.
/// No modifiers exist in Milestone 1; <see cref="UtilitySelector"/> applies whatever it is given.
/// </summary>
public interface IUtilityModifier
{
    double Adjust(ActionCandidate candidate, PlayerMatchState player, MatchState state);
}
