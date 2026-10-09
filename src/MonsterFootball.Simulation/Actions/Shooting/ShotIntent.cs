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

/// <summary>What the goalkeeper did with a saved shot (D5).</summary>
public enum SaveKind
{
    /// <summary>Caught and kept.</summary>
    Held,

    /// <summary>Pushed back into play: the ball comes to rest at <see cref="ShotResult.ReboundPosition"/>.</summary>
    Parried,

    /// <summary>Pushed behind the goal line: a corner.</summary>
    ParriedBehind,
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

    /// <summary>For a save: held, parried back into play or parried behind.</summary>
    public SaveKind? Save { get; init; }

    /// <summary>For a parry back into play: where the ball comes to rest (may be beyond a line).</summary>
    public Vec2? ReboundPosition { get; init; }

    /// <summary>For a miss: the defender in the shooting lane who deflected it behind (a corner), if any.</summary>
    public PlayerId? DeflectedBy { get; init; }

    /// <summary>A penalty kick (resolved by <see cref="ShotResolver.ResolvePenalty"/>).</summary>
    public bool IsPenalty { get; init; }
}
