using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Tackling;

/// <summary>A defender's wish to challenge the ball carrier.</summary>
public sealed record TackleIntent(PlayerId Tackler, PlayerId Carrier) : ActionIntent(Tackler);

public enum TackleOutcome
{
    /// <summary>Defender takes the ball.</summary>
    Won,

    /// <summary>Carrier keeps the ball; defender is beaten.</summary>
    Lost,

    /// <summary>Ball knocked loose; nobody has it.</summary>
    LooseBall,
}

public sealed record TackleResult(
    TackleOutcome Outcome,
    PlayerId Tackler,
    PlayerId Carrier,
    Vec2 Position,
    Vec2 BallPosition,
    double ContactProbability,
    double CleanProbability);
