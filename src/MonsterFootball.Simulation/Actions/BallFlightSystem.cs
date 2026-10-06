using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;

namespace MonsterFootball.Simulation.Actions;

/// <summary>Advances a pass or shot in flight and applies its outcome on the arrival tick.</summary>
internal sealed class BallFlightSystem
{
    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.Ball.Flight is not { } flight)
            return;

        if (state.Clock.Tick < flight.ArrivalTick)
            state.Ball.MoveInFlight(flight.PositionAt(state.Clock.Tick));
        else if (flight.Pass is { } pass)
            PassExecution.Land(context, pass);
        else
            ShotExecution.Land(context, flight.Shot!);
    }
}
