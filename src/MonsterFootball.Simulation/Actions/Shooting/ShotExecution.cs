using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Shooting;

/// <summary>
/// Applies a resolved shot immediately (ADR-015). Goals increment the score and move the match to
/// <see cref="MatchPhase.GoalRestart"/>; saves and misses give the ball to the defending goalkeeper.
/// </summary>
internal static class ShotExecution
{
    public static void Apply(MatchContext context, ShotResult result)
    {
        var state = context.State;
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
            ]));

        switch (result.Outcome)
        {
            case ShotOutcome.Goal:
                state.SetBallLoose(result.TargetPosition);
                state.Score.AddGoal(result.Team);
                state.KickoffTeam = result.Team.Opponent();
                state.Phase = MatchPhase.GoalRestart;
                context.Emit(MatchEventType.GoalScored, actor: result.Shooter, team: result.Team, position: result.TargetPosition,
                    metadata: ImmutableSortedDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string>("score", state.Score.ToString()),
                    ]));
                break;

            case ShotOutcome.Saved:
                context.Emit(MatchEventType.ShotSaved, actor: result.Goalkeeper, target: result.Shooter,
                    team: result.Team.Opponent(), position: result.TargetPosition);
                context.GiveBall(result.Goalkeeper);
                break;

            case ShotOutcome.Missed:
                context.Emit(MatchEventType.ShotMissed, actor: result.Shooter, team: result.Team, position: result.TargetPosition);
                // No goal kicks yet: the defending goalkeeper restarts play with the ball.
                context.GiveBall(result.Goalkeeper);
                break;
        }
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
