using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Laws;

/// <summary>Why a card was shown.</summary>
internal enum CardReason
{
    Reckless,
    PromisingAttack,
    SeriousFoulPlay,

    /// <summary>Denying an obvious goal-scoring opportunity.</summary>
    Dogso,

    SecondYellow,
}

/// <summary>
/// Cards for fouls (Law 12, MILESTONE_2 §14, ADR-032). The referee applies the laws consistently; how bad a foul looks
/// is still a judgement, so reckless and serious foul play are rolls rising with severity:
/// <list type="bullet">
/// <item>DOGSO — the carrier within <see cref="DogsoRange"/> of goal with no outfield defender (other than the tackler)
/// goal-side: a red outside the area; inside it a yellow when the challenge was an attempt to play the ball (not
/// from behind), otherwise a red (D10).</item>
/// <item>Serious foul play — red with chance clamp((severity − 0.7) × 1.2, 0, 0.5).</item>
/// <item>Stopping a promising attack — the carrier in the final third with at most one outfield defender goal-side: yellow.</item>
/// <item>Reckless — yellow with chance clamp(0.07 + (severity − 0.3) × 0.55, 0, 0.6), halved for a player already booked.</item>
/// </list>
/// A second yellow is a red.
/// </summary>
internal static class Discipline
{
    public const double DogsoRange = 25;
    public const double SeriousFoulThreshold = 0.7;
    public const double SeriousFoulPerSeverity = 1.2;
    public const double SeriousFoulMax = 0.5;
    public const double RecklessBase = 0.07;
    public const double RecklessThreshold = 0.3;
    public const double RecklessPerSeverity = 0.55;

    /// <summary>Referees think twice before a second caution for a reckless foul.</summary>
    public const double BookedLeniency = 0.5;
    public const double RecklessMax = 0.6;

    public static (DisciplinaryOutcome Outcome, CardReason? Reason) Decide(MatchState state, PlayerMatchState tackler, PlayerMatchState carrier, TackleResult foul, IMatchRandom random)
    {
        var goal = state.Pitch.OpponentGoalCentre(carrier.Team);
        var defenders = state.TeamPlayers(tackler.Team)
            .Where(p => p != tackler && !p.Role.IsGoalkeeper())
            .Select(p => p.Position);
        var goalSide = Spatial.GoalSideCount(carrier.Position, goal, defenders);
        var inArea = state.Zones.IsIn(tackler.Team, carrier.Position, TacticalAreas.OwnPenaltyArea);

        CardReason? reason = null;
        var red = false;
        if (goalSide == 0 && carrier.Position.DistanceTo(goal) <= DogsoRange)
        {
            reason = CardReason.Dogso;
            red = !inArea || TackleResolver.FromBehind(state, tackler, carrier);
        }
        else if (random.Roll(Math.Clamp((foul.Severity - SeriousFoulThreshold) * SeriousFoulPerSeverity, 0, SeriousFoulMax)))
        {
            reason = CardReason.SeriousFoulPlay;
            red = true;
        }
        else if (goalSide <= 1 && state.Zones.IsIn(carrier.Team, carrier.Position, TacticalAreas.FinalThird))
            reason = CardReason.PromisingAttack;
        else if (random.Roll(Math.Clamp(RecklessBase + (foul.Severity - RecklessThreshold) * RecklessPerSeverity, 0, RecklessMax)
                             * (tackler.YellowCards > 0 ? BookedLeniency : 1)))
            reason = CardReason.Reckless;

        if (reason is null)
            return (DisciplinaryOutcome.None, null);
        if (red)
            return (DisciplinaryOutcome.Red, reason);
        return tackler.YellowCards >= 1 ? (DisciplinaryOutcome.SecondYellow, CardReason.SecondYellow) : (DisciplinaryOutcome.Yellow, reason);
    }
}
