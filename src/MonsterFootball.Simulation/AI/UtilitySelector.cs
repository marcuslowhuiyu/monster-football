using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Picks one candidate (ADR-018):
/// 1. apply every <see cref="IUtilityModifier"/>;
/// 2. keep candidates scoring more than (best − <see cref="Tolerance"/>) — the plausible options;
/// 3. one plausible option → take it without drawing a random number; several → pick one with
///    probability proportional to how far it clears the cut-off, using one <see cref="IMatchRandom"/> draw.
/// The best option is always the most likely; anything more than <see cref="Tolerance"/> below it is never picked.
/// </summary>
internal sealed class UtilitySelector(IReadOnlyList<IUtilityModifier> modifiers)
{
    public const double Tolerance = 8;

    public UtilitySelector()
        : this([])
    {
    }

    public ActionCandidate Choose(
        IReadOnlyList<ActionCandidate> candidates,
        PlayerMatchState player,
        MatchState state,
        IMatchRandom random)
    {
        if (candidates.Count == 0)
            throw new ArgumentException("At least one candidate is required.", nameof(candidates));

        var scored = modifiers.Count == 0
            ? candidates
            : candidates.Select(c => c.WithUtility(c.Utility + modifiers.Sum(m => m.Adjust(c, player, state)))).ToList();

        var best = scored.Max(c => c.Utility);
        var cutOff = best - Tolerance;
        var plausible = scored.Where(c => c.Utility > cutOff).ToList();

        if (plausible.Count == 1)
            return plausible[0];

        var total = plausible.Sum(c => c.Utility - cutOff);
        var roll = random.NextDouble() * total;
        foreach (var candidate in plausible)
        {
            roll -= candidate.Utility - cutOff;
            if (roll < 0)
                return candidate;
        }

        return plausible[^1];
    }
}
