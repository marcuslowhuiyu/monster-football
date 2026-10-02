using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Passing;

/// <summary>Applies a resolved pass: launches the ball, and later lands it with the decided outcome.</summary>
internal static class PassExecution
{
    public static void Launch(MatchContext context, PassResult result)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        var travelTicks = Math.Max(1, (long)Math.Ceiling(result.TravelDuration / context.Config.TickSeconds - 1e-9));
        state.Ball.Launch(new BallFlight(result, tick, tick + travelTicks));

        context.Emit(MatchEventType.PassAttempted,
            actor: result.Passer,
            target: result.IntendedReceiver,
            team: result.Team,
            position: result.StartPosition,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("quality", Format(result.Quality)),
                new KeyValuePair<string, string>("travel", Format(result.TravelDuration)),
            ]));
    }

    public static void Land(MatchContext context, BallFlight flight)
    {
        var pass = flight.Pass;
        switch (pass.Outcome)
        {
            case PassOutcome.Completed:
                context.GiveBall(pass.IntendedReceiver);
                context.Emit(MatchEventType.PassCompleted, actor: pass.Passer, target: pass.IntendedReceiver,
                    team: pass.Team, position: context.State.Ball.Position);
                break;

            case PassOutcome.Intercepted:
                var interceptor = pass.Interceptor!.Value;
                context.Emit(MatchEventType.PassIntercepted, actor: interceptor, target: pass.IntendedReceiver,
                    team: context.State.Player(interceptor).Team, position: pass.TargetPosition);
                context.GiveBall(interceptor);
                break;

            case PassOutcome.Misplaced:
                context.State.SetBallLoose(pass.TargetPosition);
                context.Emit(MatchEventType.PassMisplaced, actor: pass.Passer, target: pass.IntendedReceiver,
                    team: pass.Team, position: pass.TargetPosition);
                break;
        }
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
