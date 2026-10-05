namespace MonsterFootball.Simulation.Players;

using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;

/// <summary>
/// Mutable per-match player state. Possession is not stored here;
/// use <see cref="MatchState.HasBall"/> (ADR-007).
/// </summary>
public sealed class PlayerMatchState
{
    public const double MinStamina = 0;
    public const double MaxStamina = 100;

    internal PlayerMatchState(PlayerDefinition definition, TeamSide team, Vec2 position)
    {
        Definition = definition;
        Team = team;
        Position = position;
        MovementTarget = position;
    }

    public PlayerDefinition Definition { get; }
    public PlayerId PlayerId => Definition.Id;
    public TeamSide Team { get; }

    public Vec2 Position { get; internal set; }
    public Vec2 MovementTarget { get; internal set; }
    public MovementIntensity MovementIntensity { get; internal set; } = MovementIntensity.Jog;
    public double CurrentStamina { get; internal set; } = MaxStamina;

    /// <summary>Metres moved during the most recent tick (drives stamina drain).</summary>
    public double LastTickDistance { get; internal set; }

    /// <summary>Total metres moved this match.</summary>
    public double DistanceCovered { get; internal set; }

    /// <summary>The ongoing action this player is committed to, if any (currently only carries persist).</summary>
    public ActionIntent? CurrentIntent { get; internal set; }

    /// <summary>The carry in progress; only ever set on the ball carrier.</summary>
    public CarryResult? ActiveCarry { get; internal set; }

    /// <summary>The off-ball behaviour this player is committed to, if any.</summary>
    public OffBallPlan? OffBallPlan { get; internal set; }

    /// <summary>The player may not attempt a tackle before this tick.</summary>
    public long TackleCooldownUntilTick { get; internal set; }

    internal void ClearIntent()
    {
        CurrentIntent = null;
        ActiveCarry = null;
    }

    public override string ToString() =>
        $"{Team} #{Definition.ShirtNumber} {Definition.Name} ({PlayerId}, {Definition.Position}) at {Position}";
}
