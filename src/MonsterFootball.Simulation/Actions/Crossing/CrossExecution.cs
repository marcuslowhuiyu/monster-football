using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Crossing;

/// <summary>Applies a cross: launch; out of play if its path leaves the pitch; on arrival the aerial contest (ADR-036).</summary>
internal static class CrossExecution
{
    private static readonly ShotResolver Shots = new();

    /// <summary>Launches the cross. Offside is judged as for a pass, except from a corner (Law 11).</summary>
    public static void Launch(MatchContext context, CrossResult result)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        state.LaunchBall(BallFlight.ForCross(result, tick, tick + BallFlight.TravelTicks(result.TravelDuration, context.Config.TickSeconds)));
        if (result.Kind == CrossKind.OpenPlay && context.Config.OffsideEnabled)
            state.Offside = Laws.Offside.Judge(state, state.Player(result.Crosser));

        context.Emit(MatchEventType.Cross, actor: result.Crosser, team: result.Team, position: result.StartPosition,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("distance", result.Distance.ToString("0.###", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("kind", result.Kind.ToString()),
            ]));
    }

    public static void LeavePlay(MatchContext context, Vec2 crossing, Laws.BoundaryLine line) =>
        Laws.OutOfPlay.Stop(context, crossing, line);

    /// <summary>The contest where the cross lands, then its outcome.</summary>
    public static void Land(MatchContext context, CrossResult result)
    {
        var state = context.State;
        var landing = result.TargetPosition;
        var (outcome, winner) = AerialContest.Decide(state, result.Team, landing, context.Random);
        context.Emit(MatchEventType.CrossContested, actor: winner?.PlayerId, team: winner?.Team ?? result.Team, position: landing,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("outcome", outcome switch
                {
                    AerialOutcome.Claimed => "Claimed",
                    AerialOutcome.AttackerWins => "Shot",
                    AerialOutcome.DefenderWins => "Cleared",
                    _ => "Loose",
                }),
            ]));

        switch (outcome)
        {
            case AerialOutcome.Claimed:
                context.GiveBall(winner!.PlayerId, PossessionCause.Save);
                break;
            case AerialOutcome.AttackerWins:
                if (context.GiveBall(winner!.PlayerId, PossessionCause.Reception)) // false: offside
                    ShotExecution.Strike(context, Shots.ResolveFirstTime(state, new ShotIntent(winner.PlayerId), context.Random));
                break;
            case AerialOutcome.DefenderWins:
                HeaderClearance.Apply(context, winner!, landing);
                break;
            default:
                var travelled = landing - result.StartPosition;
                state.SetBallRolling(landing, LooseBallMotion.Covering(landing, travelled, PassExecution.RollOnFraction * travelled.Length, state.Clock.Tick));
                break;
        }
    }
}
