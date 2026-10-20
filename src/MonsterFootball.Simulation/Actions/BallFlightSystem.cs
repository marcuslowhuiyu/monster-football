using MonsterFootball.Simulation.Actions.Clearing;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;

namespace MonsterFootball.Simulation.Actions;

/// <summary>
/// Advances a pass, shot or clearance in flight and applies its outcome on the arrival tick. A misplaced pass or a
/// clearance whose path leaves the pitch goes out of play where it crosses the line, before it lands.
/// </summary>
internal sealed class BallFlightSystem
{
    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.Ball.Flight is not { } flight)
            return;

        var next = state.Clock.Tick < flight.ArrivalTick ? flight.PositionAt(state.Clock.Tick) : flight.TargetPosition;
        var crossing = flight.Shot is null ? Laws.OutOfPlay.Crossing(state.Pitch, state.Ball.Position, next) : null;
        if (crossing is { } leaving)
        {
            if (flight.Pass is { } pass)
                PassExecution.LeavePlay(context, pass, leaving.Point, leaving.Line);
            else
                ClearanceExecution.LeavePlay(context, leaving.Point, leaving.Line);
        }
        else if (state.Clock.Tick < flight.ArrivalTick)
            state.Ball.MoveInFlight(flight.PositionAt(state.Clock.Tick));
        else if (flight.Pass is { } landing)
            PassExecution.Land(context, landing);
        else if (flight.Clearance is { } clearance)
            ClearanceExecution.Land(context, clearance);
        else
            ShotExecution.Land(context, flight.Shot!);
    }
}
