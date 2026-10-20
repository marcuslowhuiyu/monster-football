using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Clearing;

/// <summary>Applies a resolved clearance: launches it, and later lands it loose or takes it out of play (ADR-035).</summary>
internal static class ClearanceExecution
{
    /// <summary>Launches the clearance; offside is judged now, as for a pass (ADR-029).</summary>
    public static void Launch(MatchContext context, ClearanceResult result)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        state.LaunchBall(BallFlight.ForClearance(result, tick, tick + BallFlight.TravelTicks(result.TravelDuration, context.Config.TickSeconds)));
        if (context.Config.OffsideEnabled)
            state.Offside = Laws.Offside.Judge(state, state.Player(result.Kicker));

        context.Emit(MatchEventType.Clearance,
            actor: result.Kicker,
            team: result.Team,
            position: result.StartPosition,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("distance", result.Distance.ToString("0.###", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("kind", result.Kind.ToString()),
            ]));
    }

    /// <summary>The ball lands loose and rolls on, like a misplaced pass (D4).</summary>
    public static void Land(MatchContext context, ClearanceResult result)
    {
        var state = context.State;
        var travelled = result.TargetPosition - result.StartPosition;
        var roll = LooseBallMotion.Covering(result.TargetPosition, travelled, PassExecution.RollOnFraction * travelled.Length, state.Clock.Tick);
        state.SetBallRolling(result.TargetPosition, roll);
    }

    /// <summary>A clearance whose flight crosses a line goes out there; the clearer touched it last.</summary>
    public static void LeavePlay(MatchContext context, Vec2 crossing, Laws.BoundaryLine line) =>
        Laws.OutOfPlay.Stop(context, crossing, line);
}
