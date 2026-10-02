using MonsterFootball.Simulation.Actions.Passing;

namespace MonsterFootball.Simulation.Actions;

/// <summary>Advances a ball in flight and lands it on its arrival tick.</summary>
internal sealed class BallFlightSystem
{
    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.Ball.Flight is not { } flight)
            return;

        if (state.Clock.Tick >= flight.ArrivalTick)
            PassExecution.Land(context, flight);
        else
            state.Ball.MoveInFlight(flight.PositionAt(state.Clock.Tick));
    }
}
