using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Actions.Tackling;

/// <summary>
/// Resolves a challenge in two rolls:
/// 1. contact — defender rating vs carrier rating → Lost on failure;
/// 2. clean — Awareness-weighted chance the defender keeps the ball → Won, otherwise LooseBall.
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

    public static double DefenderRating(PlayerStats s) =>
        s.Defending + PowerWeight * (s.Power - 5) + AwarenessWeight * (s.Awareness - 5);

    public static double CarrierRating(PlayerStats s) =>
        s.Technique + PowerWeight * (s.Power - 5) + AwarenessWeight * (s.Awareness - 5);

    public double ContactProbability(PlayerStats tackler, PlayerStats carrier) =>
        Curves.Sigmoid(ContactScale * (DefenderRating(tackler) - CarrierRating(carrier)) + ContactBias);

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

        var contact = ContactProbability(tackler.Definition.Stats, carrier.Definition.Stats);
        var clean = CleanProbability(tackler.Definition.Stats);
        var position = carrier.Position;

        if (!random.Roll(contact))
            return new TackleResult(TackleOutcome.Lost, intent.Tackler, intent.Carrier, position, position, contact, clean);

        if (random.Roll(clean))
            return new TackleResult(TackleOutcome.Won, intent.Tackler, intent.Carrier, position, tackler.Position, contact, clean);

        var ball = LooseBallLanding(state.Pitch, position, random);
        return new TackleResult(TackleOutcome.LooseBall, intent.Tackler, intent.Carrier, position, ball, contact, clean);
    }

    private static Vec2 LooseBallLanding(PitchGeometry pitch, Vec2 from, IMatchRandom random)
    {
        Vec2 direction;
        do
        {
            direction = new Vec2(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
        } while (direction.LengthSquared is > 1 or < 1e-6);

        var distance = LooseBallMinDistance + (LooseBallMaxDistance - LooseBallMinDistance) * random.NextDouble();
        return pitch.Clamp(from + direction.Normalized() * distance);
    }
}
