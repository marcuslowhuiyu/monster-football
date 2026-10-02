using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Actions.Passing;

/// <summary>Deterministic, RNG-free assessment of a pass, shared by resolution and target selection.</summary>
internal sealed record PassEstimate(
    double Ability,
    double Distance,
    double PressurePenalty,
    double AccuracyProbability,
    double TravelDuration,
    IReadOnlyList<InterceptionThreat> Threats)
{
    /// <summary>Probability the pass is accurate and no opponent intercepts it.</summary>
    public double SuccessProbability =>
        Threats.Aggregate(AccuracyProbability, (p, threat) => p * (1 - threat.Probability));
}

/// <summary>An opponent who could cut out the pass, ordered by how early along the lane they meet it.</summary>
internal sealed record InterceptionThreat(PlayerId Opponent, Vec2 Point, double AlongLane, double Probability);

/// <summary>
/// Resolves a <see cref="PassIntent"/>:
/// 1. accuracy roll (ability vs distance + pressure) → Misplaced on failure;
/// 2. otherwise one roll per opponent near the lane, earliest first → Intercepted on success;
/// 3. otherwise Completed.
/// Reads state; never mutates it.
/// </summary>
internal sealed class PassResolver
{
    // Ability = Passing + modifiers from Technique and Awareness (5 is neutral).
    public const double TechniqueWeight = 0.3;
    public const double AwarenessWeight = 0.3;

    // Accuracy difficulty.
    public const double DistancePenaltyPerMetre = 0.1;
    public const double PressureRadius = 5;
    public const double PressurePenaltyPerMetre = 0.4;
    public const double AccuracyScale = 0.7;
    public const double AccuracyOffset = 1.0;

    // Interception.
    public const double MinLaneFraction = 0.05;
    public const double BaseReach = 1.0;
    public const double ReachPerAwareness = 0.1;
    public const double ReachRunFraction = 0.5;
    public const double BaseInterception = 0.3;
    public const double InterceptionPerDefending = 0.06;

    // Ball travel.
    public const double BaseBallSpeed = 12;
    public const double BallSpeedPerMetre = 0.25;
    public const double MaxBallSpeed = 25;

    public PassEstimate Estimate(MatchState state, PassIntent intent)
    {
        var passer = state.Player(intent.Passer);
        var receiver = state.Player(intent.Receiver);
        if (passer.Team != receiver.Team)
            throw new ArgumentException($"Pass receiver {receiver} is not a teammate of {passer}.", nameof(intent));

        var start = passer.Position;
        var target = receiver.Position;
        var stats = passer.Definition.Stats;
        var ability = stats.Passing
            + TechniqueWeight * (stats.Technique - 5)
            + AwarenessWeight * (stats.Awareness - 5);

        var distance = start.DistanceTo(target);
        var opponents = state.TeamPlayers(passer.Team.Opponent()).ToList();
        var nearestOpponent = opponents.Min(o => o.Position.DistanceTo(start));
        var pressure = Math.Max(0, PressureRadius - nearestOpponent) * PressurePenaltyPerMetre;

        var quality = ability - distance * DistancePenaltyPerMetre - pressure;
        var accuracy = Curves.Sigmoid(AccuracyScale * (quality + AccuracyOffset));

        var travel = TravelDuration(distance);
        var interceptionFactor = Math.Max(0.2, 1.1 - 0.06 * ability);
        var threats = new List<InterceptionThreat>();

        foreach (var opponent in opponents)
        {
            var point = Vec2.ClosestPointOnSegment(start, target, opponent.Position, out var t);
            if (t < MinLaneFraction)
                continue;

            var laneDistance = opponent.Position.DistanceTo(point);
            var reach = BaseReach
                + ReachPerAwareness * opponent.Definition.Stats.Awareness
                + ReachRunFraction * MovementSystem.TopSpeed(opponent) * t * travel;
            if (laneDistance >= reach)
                continue;

            var probability = (1 - laneDistance / reach)
                * (BaseInterception + InterceptionPerDefending * opponent.Definition.Stats.Defending)
                * interceptionFactor;
            threats.Add(new InterceptionThreat(opponent.PlayerId, point, t, Math.Clamp(probability, 0, 0.95)));
        }

        // Earliest along the lane first; stable on ties because the source order is stable.
        var ordered = threats.OrderBy(threat => threat.AlongLane).ToList();
        return new PassEstimate(ability, distance, pressure, accuracy, travel, ordered);
    }

    public PassResult Resolve(MatchState state, PassIntent intent, IMatchRandom random)
    {
        var estimate = Estimate(state, intent);
        var passer = state.Player(intent.Passer);
        var start = passer.Position;
        var intendedTarget = state.Player(intent.Receiver).Position;

        if (!random.Roll(estimate.AccuracyProbability))
        {
            var landing = MisplacedLanding(state.Pitch, intendedTarget, estimate.Distance, random);
            return new PassResult(PassOutcome.Misplaced, passer.Team, intent.Passer, intent.Receiver, null,
                start, intendedTarget, landing, TravelDuration(start.DistanceTo(landing)), estimate.AccuracyProbability);
        }

        foreach (var threat in estimate.Threats)
        {
            if (!random.Roll(threat.Probability))
                continue;

            return new PassResult(PassOutcome.Intercepted, passer.Team, intent.Passer, intent.Receiver, threat.Opponent,
                start, intendedTarget, threat.Point, estimate.TravelDuration * threat.AlongLane, estimate.AccuracyProbability);
        }

        return new PassResult(PassOutcome.Completed, passer.Team, intent.Passer, intent.Receiver, null,
            start, intendedTarget, intendedTarget, estimate.TravelDuration, estimate.AccuracyProbability);
    }

    public static double TravelDuration(double distance)
    {
        var speed = Math.Min(MaxBallSpeed, BaseBallSpeed + BallSpeedPerMetre * distance);
        return distance / speed;
    }

    /// <summary>Lands the ball 3–(3 + 0.3 × distance) m from the intended target in a random direction, kept on the pitch.</summary>
    private static Vec2 MisplacedLanding(PitchGeometry pitch, Vec2 intendedTarget, double distance, IMatchRandom random)
    {
        // Uniform direction without trigonometry: rejection-sample the unit disc.
        Vec2 direction;
        do
        {
            direction = new Vec2(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
        } while (direction.LengthSquared is > 1 or < 1e-6);

        var error = 3 + 0.3 * distance * random.NextDouble();
        return pitch.Clamp(intendedTarget + direction.Normalized() * error);
    }
}
