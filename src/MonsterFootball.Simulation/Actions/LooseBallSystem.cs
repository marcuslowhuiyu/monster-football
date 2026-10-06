using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions;

/// <summary>A loose ball is collected by the nearest player within <see cref="ControlRadius"/> (ties → earlier player).</summary>
internal sealed class LooseBallSystem
{
    public const double ControlRadius = 1.5;

    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (!state.Ball.IsLoose)
            return;

        var ball = state.Ball.Position;
        var collector = state.Players
            .Where(p => p.Position.DistanceTo(ball) <= ControlRadius)
            .MinBy(p => p.Position.DistanceTo(ball));

        if (collector is not null)
            context.GiveBall(collector.PlayerId, PossessionCause.LooseBall);
    }
}
