using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;

namespace MonsterFootball.Simulation.Actions;

/// <summary>
/// Advances a pass or shot in flight and applies its outcome on the arrival tick. A misplaced pass whose path leaves
/// the pitch goes out of play where it crosses the line, before it lands.
/// </summary>
internal sealed class BallFlightSystem
{
    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.Ball.Flight is not { } flight)
            return;

        var next = state.Clock.Tick < flight.ArrivalTick ? flight.PositionAt(state.Clock.Tick) : flight.TargetPosition;
        if (flight.Pass is { } leaving && Laws.OutOfPlay.Crossing(state.Pitch, state.Ball.Position, next) is { } crossing)
            PassExecution.LeavePlay(context, leaving, crossing.Point, crossing.Line);
        else if (state.Clock.Tick < flight.ArrivalTick)
            state.Ball.MoveInFlight(flight.PositionAt(state.Clock.Tick));
        else if (flight.Pass is { } pass)
            PassExecution.Land(context, pass);
        else
            ShotExecution.Land(context, flight.Shot!);
    }
}
