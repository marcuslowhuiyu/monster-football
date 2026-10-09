using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Passing;

/// <summary>Applies a resolved pass: launches the ball, and later lands it with the decided outcome.</summary>
internal static class PassExecution
{
    /// <summary>
    /// Launches the pass. Unless <paramref name="offsideApplies"/> is false (throw-ins, goal kicks, corners), who is in an
    /// offside position is judged now, as the ball is played (ADR-029).
    /// </summary>
    public static void Launch(MatchContext context, PassResult result, bool offsideApplies = true)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        state.LaunchBall(BallFlight.ForPass(result, tick, tick + BallFlight.TravelTicks(result.TravelDuration, context.Config.TickSeconds)));
        if (offsideApplies && context.Config.OffsideEnabled)
            state.Offside = Laws.Offside.Judge(state, state.Player(result.Passer));

        context.Emit(MatchEventType.PassAttempted,
            actor: result.Passer,
            target: result.IntendedReceiver,
            team: result.Team,
            position: result.StartPosition,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("quality", Format(result.Quality)),
                new KeyValuePair<string, string>("success", Format(result.SuccessProbability)),
                new KeyValuePair<string, string>("stamina", Format(state.Player(result.Passer).CurrentStamina)),
                new KeyValuePair<string, string>("distance", Format(result.Distance)),
                new KeyValuePair<string, string>("threats", result.InterceptionThreats.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("travel", Format(result.TravelDuration)),
            ]));
    }

    public static void Land(MatchContext context, PassResult pass)
    {
        switch (pass.Outcome)
        {
            case PassOutcome.Completed:
                if (!context.GiveBall(pass.IntendedReceiver, PossessionCause.Reception))
                    break; // offside
                context.Emit(MatchEventType.PassCompleted, actor: pass.Passer, target: pass.IntendedReceiver,
                    team: pass.Team, position: context.State.Ball.Position);
                break;

            case PassOutcome.Intercepted:
                var interceptor = pass.Interceptor!.Value;
                context.Emit(MatchEventType.PassIntercepted, actor: interceptor, target: pass.IntendedReceiver,
                    team: context.State.Player(interceptor).Team, position: pass.TargetPosition,
                    metadata: ImmutableSortedDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string>("chance", Format(pass.InterceptionProbability)),
                    ]));
                context.GiveBall(interceptor, PossessionCause.Interception);
                break;

            case PassOutcome.Misplaced:
                var state = context.State;
                var travelled = pass.TargetPosition - pass.StartPosition;
                var roll = LooseBallMotion.Covering(pass.TargetPosition, travelled, RollOnFraction * travelled.Length, state.Clock.Tick);
                state.SetBallRolling(pass.TargetPosition, roll);
                context.Emit(MatchEventType.PassMisplaced, actor: pass.Passer, target: pass.IntendedReceiver,
                    team: pass.Team, position: pass.TargetPosition);
                break;
        }
    }

    /// <summary>A misplaced pass rolls on after landing for this fraction of the distance it flew (D4).</summary>
    public const double RollOnFraction = 0.35;

    /// <summary>A misplaced pass whose flight crosses a line: PassMisplaced at the crossing point, then out of play.</summary>
    public static void LeavePlay(MatchContext context, PassResult pass, Vec2 crossing, Laws.BoundaryLine line)
    {
        context.Emit(MatchEventType.PassMisplaced, actor: pass.Passer, target: pass.IntendedReceiver,
            team: pass.Team, position: crossing);
        Laws.OutOfPlay.Stop(context, crossing, line);
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
