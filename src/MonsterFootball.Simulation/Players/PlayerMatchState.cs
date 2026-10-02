namespace MonsterFootball.Simulation.Players;

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
    public double CurrentStamina { get; internal set; } = MaxStamina;

    public override string ToString() =>
        $"{Team} #{Definition.ShirtNumber} {Definition.Name} ({PlayerId}, {Definition.Position}) at {Position}";
}
