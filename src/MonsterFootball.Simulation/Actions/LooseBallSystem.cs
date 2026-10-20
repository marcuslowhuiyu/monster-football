using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions;

/// <summary>
/// A rolling loose ball moves on along its <see cref="LooseBallMotion"/> (going out of play if it crosses a line);
/// then a loose ball is collected by the nearest player within <see cref="ControlRadius"/> (exact ties → one random draw among them).
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
        var inReach = state.ActivePlayers.Where(p => p.Position.DistanceTo(ball) <= ControlRadius).ToList();
        if (inReach.Count == 0)
            return;

        // The nearest player collects. Players exactly as near (e.g. two chasers on a clearance's landing spot) contest
        // it: one draw picks among them, so neither side is favoured by list order (ADR-035).
        var nearest = inReach.Min(p => p.Position.DistanceTo(ball));
        var tied = inReach.Where(p => p.Position.DistanceTo(ball) == nearest).ToList();
        var collector = tied.Count == 1 ? tied[0] : tied[context.Random.NextInt(0, tied.Count)];
        context.GiveBall(collector.PlayerId, PossessionCause.LooseBall);
    }
}
