using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Shooting;

/// <summary>The ball carrier's wish to shoot at the opponent's goal.</summary>
public sealed record ShotIntent(PlayerId Shooter) : ActionIntent(Shooter);

public enum ShotOutcome
{
    Goal,
    Saved,
    Missed,
}

/// <summary>Resolved shot with the presentation data needed to animate it.</summary>
public sealed record ShotResult(
    ShotOutcome Outcome,
    TeamSide Team,
    PlayerId Shooter,
    PlayerId Goalkeeper,
    Vec2 StartPosition,
    Vec2 TargetPosition,
    double TravelDuration,
    double Quality,
    double GoalProbability)
{
    /// <summary>The goalkeeper's chance of saving if the shot was on target.</summary>
    public double SaveProbability { get; init; }

    /// <summary>Distance from shooter to goal centre, in metres.</summary>
    public double Distance { get; init; }
}
