namespace MonsterFootball.Simulation.Core;

using MonsterFootball.Simulation.Actions;
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

    /// <summary>The pass or shot currently travelling, if any. A ball in flight has no possessor.</summary>
    public BallFlight? Flight { get; private set; }

    public bool IsInFlight => Flight is not null;

    /// <summary>The roll of a loose ball still moving, if any (D4). Null once it stops or anyone has the ball.</summary>
    public LooseBallMotion? Roll { get; private set; }

    /// <summary>Nobody has the ball and it is not travelling: anyone may pick it up.</summary>
    public bool IsLoose => PossessorId is null && Flight is null;

    internal void GiveTo(PlayerId playerId, Vec2 position)
    {
        PossessorId = playerId;
        Flight = null;
        Roll = null;
        Position = position;
    }

    internal void SetLoose(Vec2 position, LooseBallMotion? roll = null)
    {
        PossessorId = null;
        Flight = null;
        Roll = roll;
        Position = position;
    }

    internal void Launch(BallFlight flight)
    {
        PossessorId = null;
        Flight = flight;
        Roll = null;
        Position = flight.StartPosition;
    }

    internal void MoveInFlight(Vec2 position) => Position = position;
}
