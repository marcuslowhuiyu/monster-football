using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Actions.Crossing;

/// <summary>
/// Resolves a <see cref="CrossIntent"/> (ADR-036): aimed at the teammate in the opposition penalty area with the most
/// space (largest distance to the nearest opponent; ties → earlier player), else the penalty spot. Two draws scatter the
/// landing point sideways (± 3 m + 0.5 m per Passing point below 10) and lengthways (± 3 m). Flight 20 m/s. Reads state.
/// </summary>
internal sealed class CrossResolver
{
    public const double BaseLateralScatter = 3;
    public const double LateralScatterPerPassing = 0.5;
    public const double LengthScatter = 3;
    public const double BallSpeed = 20;

    /// <summary>Where the cross is aimed (pitch coordinates). Deterministic.</summary>
    public Vec2 Aim(MatchState state, PlayerMatchState crosser)
    {
        var opponents = state.TeamPlayers(crosser.Team.Opponent()).ToList();
        PlayerMatchState? best = null;
        var bestSpace = double.NegativeInfinity;
        foreach (var teammate in state.TeamPlayers(crosser.Team))
        {
            if (teammate == crosser || !state.Zones.IsIn(teammate.Team, teammate.Position, TacticalAreas.OppositionPenaltyArea))
                continue;
            var space = opponents.Count == 0 ? double.MaxValue : opponents.Min(o => o.Position.DistanceTo(teammate.Position));
            if (space > bestSpace)
            {
                best = teammate;
                bestSpace = space;
            }
        }

        return best?.Position ?? state.Pitch.PenaltySpot(crosser.Team.Opponent());
    }

    public CrossResult Resolve(MatchState state, CrossIntent intent, IMatchRandom random)
    {
        var crosser = state.Player(intent.Crosser);
        if (!state.HasBall(intent.Crosser))
            throw new ArgumentException($"{crosser} cannot cross without the ball.", nameof(intent));

        var target = Aim(state, crosser);
        var start = crosser.Position;
        var along = (target - start).Normalized();
        var across = new Vec2(-along.Y, along.X);
        var lateralLimit = BaseLateralScatter + LateralScatterPerPassing * (10 - crosser.Definition.Stats.Passing);
        var lateral = (random.NextDouble() * 2 - 1) * lateralLimit;
        var length = (random.NextDouble() * 2 - 1) * LengthScatter;
        var landing = target + across * lateral + along * length;

        return new CrossResult(crosser.Team, crosser.PlayerId, intent.Kind, start, target, landing, start.DistanceTo(landing) / BallSpeed);
    }
}
