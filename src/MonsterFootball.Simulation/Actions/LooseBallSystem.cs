using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions;

/// <summary>
/// A rolling loose ball moves on along its <see cref="LooseBallMotion"/> (going out of play if it crosses a line);
/// then a loose ball is collected by the nearest player within <see cref="ControlRadius"/> (ties → earlier player).
/// </summary>
internal sealed class LooseBallSystem
{
    public const double ControlRadius = 1.5;

    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (!state.Ball.IsLoose)
            return;

        if (state.Ball.Roll is { } roll)
        {
            var tick = state.Clock.Tick;
            var stopped = roll.HasStoppedBy(tick, context.Config.TickSeconds);
            if (Laws.OutOfPlay.MoveLoose(context, roll.PositionAt(tick, context.Config.TickSeconds), stopped ? null : roll))
                return;
        }

        var ball = state.Ball.Position;
        var collector = state.ActivePlayers
            .Where(p => p.Position.DistanceTo(ball) <= ControlRadius)
            .MinBy(p => p.Position.DistanceTo(ball));

        if (collector is not null)
            context.GiveBall(collector.PlayerId, PossessionCause.LooseBall);
    }
}
