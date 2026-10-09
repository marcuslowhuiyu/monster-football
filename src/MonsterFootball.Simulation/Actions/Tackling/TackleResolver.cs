using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Fitness;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Actions.Tackling;

/// <summary>
/// Resolves a challenge in up to three rolls:
/// 1. contact — defender rating vs carrier rating;
/// 2. foul (D9, ADR-030) — more likely when the defender missed the ball, is tired, sprinted in or came from behind, less
///    likely with Defending and inside their own penalty area → Foul;
/// 3. without contact → Lost; otherwise clean — Awareness-weighted chance the defender keeps the ball → Won, else LooseBall.
/// Ratings: defender = Defending + 0.3·(Power−5) + 0.3·(Awareness−5);
///          carrier  = Technique + 0.3·(Power−5) + 0.3·(Awareness−5).
/// Reads state; never mutates it.
/// </summary>
internal sealed class TackleResolver
{
    public const double PowerWeight = 0.3;
    public const double AwarenessWeight = 0.3;
    public const double ContactScale = 0.3;
    public const double ContactBias = 0.15;
    public const double BaseCleanProbability = 0.6;
    public const double CleanPerAwareness = 0.03;
    public const double LooseBallMinDistance = 2;
    public const double LooseBallMaxDistance = 5;

    // Fouls.
    public const double FoulBase = 0.03;
    public const double FoulMissedBall = 0.06;
    public const double FoulPerDefending = 0.006;
    public const double FoulPerTiredness = 0.06;
    public const double FoulFromBehind = 0.06;
    public const double FoulSprinting = 0.02;
    public const double FoulOwnBoxCaution = 0.15;
    public const double SeverityBase = 0.2;
    public const double SeverityPerPower = 0.06;
    public const double SeverityFromBehind = 0.25;
    public const double SeveritySprinting = 0.15;
    public const double SeverityMissedBall = 0.15;

    /// <summary>A tackler whose position is this far behind the carrier (in the carrier's attacking direction) challenges from behind.</summary>
    public const double FromBehindMetres = 0.5;

    public static double DefenderRating(PlayerStats s) =>
        s.Defending + PowerWeight * (s.Power - 5) + AwarenessWeight * (s.Awareness - 5);

    public static double CarrierRating(PlayerStats s) =>
        s.Technique + PowerWeight * (s.Power - 5) + AwarenessWeight * (s.Awareness - 5);

    public double ContactProbability(PlayerStats tackler, PlayerStats carrier) =>
        Curves.Sigmoid(ContactScale * (DefenderRating(tackler) - CarrierRating(carrier)) + ContactBias);

    /// <summary>Contact probability with each player's rating scaled by their fatigue.</summary>
    public double ContactProbability(PlayerMatchState tackler, PlayerMatchState carrier) =>
        Curves.Sigmoid(ContactScale * (
            DefenderRating(tackler.Definition.Stats) * FatigueModel.EffectivenessFactor(tackler)
            - CarrierRating(carrier.Definition.Stats) * FatigueModel.EffectivenessFactor(carrier)) + ContactBias);

    public double CleanProbability(PlayerStats tackler) =>
        Math.Clamp(BaseCleanProbability + CleanPerAwareness * (tackler.Awareness - 5), 0.1, 0.9);

    public TackleResult Resolve(MatchState state, TackleIntent intent, IMatchRandom random)
    {
        var tackler = state.Player(intent.Tackler);
        var carrier = state.Player(intent.Carrier);
        if (tackler.Team == carrier.Team)
            throw new ArgumentException($"{tackler} cannot tackle a teammate.", nameof(intent));
        if (!state.HasBall(intent.Carrier))
            throw new ArgumentException($"{carrier} does not have the ball.", nameof(intent));

        var contact = ContactProbability(tackler, carrier);
        var clean = CleanProbability(tackler.Definition.Stats);
        var position = carrier.Position;

        var gotTheBall = random.Roll(contact);
        var foul = FoulProbability(state, tackler, carrier, gotTheBall);
        if (random.Roll(foul))
            return new TackleResult(TackleOutcome.Foul, intent.Tackler, intent.Carrier, position, position, contact, clean)
            {
                FoulProbability = foul,
                Severity = Severity(state, tackler, carrier, gotTheBall),
            };

        if (!gotTheBall)
            return new TackleResult(TackleOutcome.Lost, intent.Tackler, intent.Carrier, position, position, contact, clean) { FoulProbability = foul };

        if (random.Roll(clean))
            return new TackleResult(TackleOutcome.Won, intent.Tackler, intent.Carrier, position, tackler.Position, contact, clean) { FoulProbability = foul };

        var ball = LooseBallLanding(position, random);
        return new TackleResult(TackleOutcome.LooseBall, intent.Tackler, intent.Carrier, position, ball, contact, clean) { FoulProbability = foul };
    }

    public static bool FromBehind(MatchState state, PlayerMatchState tackler, PlayerMatchState carrier) =>
        TeamFrame.Convert(state.Pitch, carrier.Team, tackler.Position).X
        < TeamFrame.Convert(state.Pitch, carrier.Team, carrier.Position).X - FromBehindMetres;

    public static double FoulProbability(MatchState state, PlayerMatchState tackler, PlayerMatchState carrier, bool gotTheBall)
    {
        var p = FoulBase
                + (gotTheBall ? 0 : FoulMissedBall)
                - FoulPerDefending * (tackler.Definition.Stats.Defending - 5)
                + FoulPerTiredness * FatigueModel.Tiredness(tackler.CurrentStamina)
                + (FromBehind(state, tackler, carrier) ? FoulFromBehind : 0)
                + (tackler.MovementIntensity == MovementIntensity.Sprint ? FoulSprinting : 0);
        if (state.Zones.IsIn(tackler.Team, carrier.Position, TacticalAreas.OwnPenaltyArea))
            p *= FoulOwnBoxCaution;
        return Math.Clamp(p, 0.005, 0.6);
    }

    public static double Severity(MatchState state, PlayerMatchState tackler, PlayerMatchState carrier, bool gotTheBall) =>
        Math.Clamp(
            SeverityBase
            + SeverityPerPower * (tackler.Definition.Stats.Power - 5)
            + (FromBehind(state, tackler, carrier) ? SeverityFromBehind : 0)
            + (tackler.MovementIntensity == MovementIntensity.Sprint ? SeveritySprinting : 0)
            + (gotTheBall ? 0 : SeverityMissedBall),
            0, 1);

    /// <summary>2–5 m from the challenge in a random direction; may be beyond a line (the ball then goes out).</summary>
    private static Vec2 LooseBallLanding(Vec2 from, IMatchRandom random)
    {
        Vec2 direction;
        do
        {
            direction = new Vec2(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
        } while (direction.LengthSquared is > 1 or < 1e-6);

        var distance = LooseBallMinDistance + (LooseBallMaxDistance - LooseBallMinDistance) * random.NextDouble();
        return from + direction.Normalized() * distance;
    }
}
