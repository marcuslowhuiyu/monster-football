using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Actions.Crossing;

public enum AerialOutcome
{
    /// <summary>The defending keeper caught it.</summary>
    Claimed,
    AttackerWins,
    DefenderWins,

    /// <summary>Nobody was close enough: the ball lands loose.</summary>
    Loose,
}

/// <summary>
/// Who wins a cross where it lands (ADR-036), among active players within <see cref="ContestRadius"/>: the defending
/// keeper claims it in their goal area; otherwise the best attacker and best defender by aerial rating contest it
/// (P(attacker) = Sigmoid(0.4 × (A − D) − 0.14) ≈ 0.43 between equals, one draw); a side alone wins; nobody → loose.
/// </summary>
internal static class AerialContest
{
    public const double ContestRadius = 3;
    public const double ContestScale = 0.4;
    public const double ContestBias = -0.14;

    public static double AttackerRating(PlayerStats s) => 0.5 * s.Power + 0.3 * s.Awareness + 0.2 * s.Technique;

    public static double DefenderRating(PlayerStats s) => 0.5 * s.Power + 0.3 * s.Awareness + 0.2 * s.Defending;

    public static double AttackerWinChance(PlayerMatchState attacker, PlayerMatchState defender) =>
        Curves.Sigmoid(ContestScale * (AttackerRating(attacker.Definition.Stats) - DefenderRating(defender.Definition.Stats)) + ContestBias);

    public static (AerialOutcome Outcome, PlayerMatchState? Winner) Decide(MatchState state, TeamSide attackers, Vec2 landing, IMatchRandom random)
    {
        var defenders = attackers.Opponent();
        if (InGoalArea(state.Pitch, defenders, landing) && state.Goalkeeper(defenders) is { } keeper
            && keeper.Position.DistanceTo(landing) <= ContestRadius)
            return (AerialOutcome.Claimed, keeper);

        var attacker = Best(state, attackers, landing, AttackerRating);
        var defender = Best(state, defenders, landing, DefenderRating);
        if (attacker is null && defender is null)
            return (AerialOutcome.Loose, null);
        if (defender is null)
            return (AerialOutcome.AttackerWins, attacker);
        if (attacker is null)
            return (AerialOutcome.DefenderWins, defender);
        return random.Roll(AttackerWinChance(attacker, defender))
            ? (AerialOutcome.AttackerWins, attacker)
            : (AerialOutcome.DefenderWins, defender);
    }

    /// <summary>The side's outfield player within reach with the best rating (ties → nearer, then earlier player).</summary>
    private static PlayerMatchState? Best(MatchState state, TeamSide side, Vec2 landing, Func<PlayerStats, double> rating) =>
        state.TeamPlayers(side)
            .Where(p => !p.Role.IsGoalkeeper() && p.Position.DistanceTo(landing) <= ContestRadius)
            .OrderByDescending(p => rating(p.Definition.Stats))
            .ThenBy(p => p.Position.DistanceTo(landing))
            .FirstOrDefault();

    private static bool InGoalArea(PitchGeometry pitch, TeamSide defending, Vec2 point)
    {
        var local = TeamFrame.Convert(pitch, defending, point);
        return local.X >= 0 && local.X <= PitchGeometry.GoalAreaDepth
               && Math.Abs(local.Y - pitch.Width / 2) <= PitchGeometry.GoalAreaWidth / 2;
    }
}
