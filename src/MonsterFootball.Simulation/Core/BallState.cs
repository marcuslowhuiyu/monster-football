namespace MonsterFootball.Simulation.Core;

using MonsterFootball.Simulation.Players;

/// <summary>
/// The ball is the single source of truth for possession (ADR-007):
/// a player "has the ball" exactly when they are <see cref="PossessorId"/>.
/// </summary>
public sealed class BallState
{
    internal BallState(Vec2 position)
    {
        Position = position;
    }

    public Vec2 Position { get; private set; }
    public PlayerId? PossessorId { get; private set; }
    public bool IsLoose => PossessorId is null;

    internal void GiveTo(PlayerId playerId, Vec2 position)
    {
        PossessorId = playerId;
        Position = position;
    }

    internal void SetLoose(Vec2 position)
    {
        PossessorId = null;
        Position = position;
    }
}
