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

    /// <summary>The challenge was a foul: play stops for a free kick or penalty.</summary>
    Foul,
}

public sealed record TackleResult(
    TackleOutcome Outcome,
    PlayerId Tackler,
    PlayerId Carrier,
    Vec2 Position,
    Vec2 BallPosition,
    double ContactProbability,
    double CleanProbability)
{
    /// <summary>Chance the challenge was a foul, as estimated for it.</summary>
    public double FoulProbability { get; init; }

    /// <summary>How bad the challenge was, 0–1 (Power, pace into the challenge, from behind, missing the ball); for a foul.</summary>
    public double Severity { get; init; }
}
