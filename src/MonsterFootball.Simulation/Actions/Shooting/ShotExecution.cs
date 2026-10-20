using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Shooting;

/// <summary>
/// Applies a resolved shot in two steps, like a pass (ADR-022): <see cref="Strike"/> emits ShotTaken and launches the
/// ball; <see cref="Land"/> applies the outcome decided at the strike when the ball arrives. A goal increments the
/// score and stops play with a kickoff for the conceding side. A save is held, parried back into play (the ball is
/// knocked loose to its rebound spot) or parried behind for a corner; a miss goes behind for a goal kick, or for a
/// corner when a defender in the lane deflected it (D5, ADR-028).
/// </summary>
internal static class ShotExecution
{
    public static void Strike(MatchContext context, ShotResult result)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        context.Emit(MatchEventType.ShotTaken, actor: result.Shooter, team: result.Team, position: result.StartPosition,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("xg", Format(result.GoalProbability)),
                new KeyValuePair<string, string>("onTarget", Format(result.Quality)),
                new KeyValuePair<string, string>("save", Format(result.SaveProbability)),
                new KeyValuePair<string, string>("distance", Format(result.Distance)),
                new KeyValuePair<string, string>("stamina", Format(state.Player(result.Shooter).CurrentStamina)),
                new KeyValuePair<string, string>("keeperStamina", Format(state.Player(result.Goalkeeper).CurrentStamina)),
                new KeyValuePair<string, string>("travel", Format(result.TravelDuration)),
            ]).SetItems(Flags(result)));
        state.LaunchBall(BallFlight.ForShot(result, tick, tick + BallFlight.TravelTicks(result.TravelDuration, context.Config.TickSeconds)));
    }

    public static void Land(MatchContext context, ShotResult result)
    {
        var state = context.State;
        switch (result.Outcome)
        {
            case ShotOutcome.Goal:
                state.SetBallLoose(result.TargetPosition);
                state.Score.AddGoal(result.Team);
                context.Emit(MatchEventType.GoalScored, actor: result.Shooter, team: result.Team, position: result.TargetPosition,
                    metadata: ImmutableSortedDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string>("score", state.Score.ToString()),
                    ]).SetItems(Flags(result)));
                context.StopPlay(StoppageReason.Goal, RestartType.Kickoff, result.Team.Opponent(), state.Pitch.Centre);
                break;

            case ShotOutcome.Saved:
                var save = result.Save ?? SaveKind.Held;
                context.Emit(MatchEventType.ShotSaved, actor: result.Goalkeeper, target: result.Shooter,
                    team: result.Team.Opponent(), position: result.TargetPosition,
                    metadata: ImmutableSortedDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string>("save", save.ToString()),
                    ]));
                switch (save)
                {
                    case SaveKind.Held:
                        context.GiveBall(result.Goalkeeper, PossessionCause.Save);
                        break;
                    case SaveKind.ParriedBehind:
                        state.RecordTouch(result.Goalkeeper);
                        var side = result.TargetPosition.Y < state.Pitch.Width / 2 ? -1 : 1;
                        var behindPost = new Vec2(result.TargetPosition.X, state.Pitch.Width / 2 + side * (PitchGeometry.GoalWidth / 2 + 1));
                        Laws.OutOfPlay.Stop(context, behindPost, Laws.BoundaryLine.GoalLine);
                        break;
                    case SaveKind.Parried:
                        state.SetBallLoose(result.TargetPosition);
                        state.RecordTouch(result.Goalkeeper);
                        Laws.OutOfPlay.MoveLoose(context, result.ReboundPosition!.Value);
                        break;
                }

                break;

            case ShotOutcome.Missed:
                var metadata = result.DeflectedBy is not null
                    ? ImmutableSortedDictionary.CreateRange([new KeyValuePair<string, string>("deflected", "true")])
                    : null;
                context.Emit(MatchEventType.ShotMissed, actor: result.Shooter, target: result.DeflectedBy, team: result.Team,
                    position: result.TargetPosition, metadata: metadata);
                if (result.DeflectedBy is { } defender)
                    state.RecordTouch(defender);
                Laws.OutOfPlay.Stop(context, result.TargetPosition, Laws.BoundaryLine.GoalLine);
                break;
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> Flags(ShotResult result)
    {
        if (result.IsPenalty)
            yield return new KeyValuePair<string, string>("penalty", "true");
        if (result.FromCross)
            yield return new KeyValuePair<string, string>("cross", "true");
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
