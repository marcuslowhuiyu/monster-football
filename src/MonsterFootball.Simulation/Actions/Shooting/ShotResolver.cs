using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Actions.Shooting;

/// <summary>RNG-free assessment of a shot, shared by resolution and the shooting decision.</summary>
internal sealed record ShotEstimate(
    double Ability,
    double Distance,
    double PressurePenalty,
    double AnglePenalty,
    double OnTargetProbability,
    double SaveProbability)
{
    /// <summary>Expected goals: on target and not saved.</summary>
    public double GoalProbability => OnTargetProbability * (1 - SaveProbability);
}

/// <summary>
/// Resolves a <see cref="ShotIntent"/> in two rolls:
/// 1. on target — shooter ability vs distance, angle and pressure → Missed on failure;
/// 2. save — goalkeeper rating vs shot power and closeness → Saved, otherwise Goal.
/// Reads state; never mutates it.
/// </summary>
internal sealed class ShotResolver
{
    // Shooter.
    public const double TechniqueWeight = 0.3;
    public const double PowerWeight = 0.2;
    public const double DistancePenaltyPerMetre = 0.15;
    public const double PressureRadius = 3;
    public const double PressurePenaltyPerMetre = 0.5;
    public const double AnglePenaltyScale = 1.5;
    public const double MaxAnglePenalty = 4;
    public const double OnTargetScale = 0.35;
    public const double OnTargetBias = -1.0;

    // Goalkeeper.
    public const double KeeperAwarenessWeight = 0.3;
    public const double OutOfPositionPenaltyPerMetre = 0.3;
    public const double CloseRangeDistance = 20;
    public const double CloseRangePerMetre = 0.2;
    public const double SaveScale = 0.35;
    public const double SaveBias = 0.74;

    // Ball travel.
    public const double BaseShotSpeed = 18;
    public const double ShotSpeedPerPower = 1.2;

    public ShotEstimate Estimate(MatchState state, ShotIntent intent)
    {
        var shooter = state.Player(intent.Shooter);
        var keeper = OpposingKeeper(state, shooter);
        var pitch = state.Pitch;
        var stats = shooter.Definition.Stats;

        var goal = pitch.OpponentGoalCentre(shooter.Team);
        var distance = shooter.Position.DistanceTo(goal);
        var local = TeamFrame.Convert(pitch, shooter.Team, shooter.Position);
        var toGoalLine = Math.Max(1, pitch.Length - local.X);
        var lateral = Math.Abs(local.Y - pitch.Width / 2);
        var anglePenalty = Math.Min(MaxAnglePenalty, AnglePenaltyScale * lateral / toGoalLine);

        var nearestOpponent = state.TeamPlayers(shooter.Team.Opponent())
            .Where(p => !p.Definition.Position.IsGoalkeeper())
            .Min(p => p.Position.DistanceTo(shooter.Position));
        var pressure = Math.Max(0, PressureRadius - nearestOpponent) * PressurePenaltyPerMetre;

        var ability = stats.Finishing + TechniqueWeight * (stats.Technique - 5) + PowerWeight * (stats.Power - 5);
        var onTarget = Curves.Sigmoid(OnTargetScale * (ability - DistancePenaltyPerMetre * distance - pressure - anglePenalty) + OnTargetBias);

        var keeperStats = keeper.Definition.Stats;
        var ballLocalForKeeper = TeamFrame.Convert(pitch, keeper.Team, shooter.Position);
        var idealLocal = GoalkeeperPositioning.Target(pitch, ballLocalForKeeper, keeperStats.Awareness, inPossession: false);
        var outOfPosition = keeper.Position.DistanceTo(TeamFrame.Convert(pitch, keeper.Team, idealLocal));
        var keeperRating = keeperStats.Keeping
            + KeeperAwarenessWeight * (keeperStats.Awareness - 5)
            - OutOfPositionPenaltyPerMetre * Math.Min(outOfPosition, 10);
        var shotPower = 0.5 * stats.Finishing + 0.5 * stats.Power
            + CloseRangePerMetre * Math.Max(0, CloseRangeDistance - distance);
        var save = Curves.Sigmoid(SaveScale * (keeperRating - shotPower) + SaveBias);

        return new ShotEstimate(ability, distance, pressure, anglePenalty, onTarget, save);
    }

    public ShotResult Resolve(MatchState state, ShotIntent intent, IMatchRandom random)
    {
        if (!state.HasBall(intent.Shooter))
            throw new ArgumentException($"{state.Player(intent.Shooter)} cannot shoot without the ball.", nameof(intent));

        var estimate = Estimate(state, intent);
        var shooter = state.Player(intent.Shooter);
        var keeper = OpposingKeeper(state, shooter);
        var pitch = state.Pitch;
        var goalLineX = pitch.OwnGoalLineX(keeper.Team);
        var centreY = pitch.Width / 2;
        var halfGoal = PitchGeometry.GoalWidth / 2;
        var speed = BaseShotSpeed + ShotSpeedPerPower * shooter.Definition.Stats.Power;

        ShotResult Result(ShotOutcome outcome, Vec2 target) => new(
            outcome, shooter.Team, intent.Shooter, keeper.PlayerId, shooter.Position, target,
            shooter.Position.DistanceTo(target) / speed, estimate.OnTargetProbability, estimate.GoalProbability);

        if (!random.Roll(estimate.OnTargetProbability))
        {
            // Wide of a post by 0.5–4 m; side chosen at random. No detailed placement (M1 scope).
            var side = random.Roll(0.5) ? 1 : -1;
            var wide = halfGoal + 0.5 + 3.5 * random.NextDouble();
            return Result(ShotOutcome.Missed, pitch.Clamp(new Vec2(goalLineX, centreY + side * wide)));
        }

        var onTargetY = centreY + (random.NextDouble() * 2 - 1) * (halfGoal - 0.2);
        var target = new Vec2(goalLineX, onTargetY);
        return random.Roll(estimate.SaveProbability)
            ? Result(ShotOutcome.Saved, target)
            : Result(ShotOutcome.Goal, target);
    }

    private static PlayerMatchState OpposingKeeper(MatchState state, PlayerMatchState shooter) =>
        state.TeamPlayers(shooter.Team.Opponent()).Single(p => p.Definition.Position.IsGoalkeeper());
}
