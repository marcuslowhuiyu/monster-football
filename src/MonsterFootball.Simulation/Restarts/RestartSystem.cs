using System.Collections.Immutable;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tactical;

namespace MonsterFootball.Simulation.Restarts;

/// <summary>
/// Runs a stoppage (ADR-023, ADR-028). While play is stopped every player heads for their restart position
/// (<see cref="Position"/>, via <see cref="RestartLayout"/>). <see cref="Advance"/> moves the dead ball on to the
/// restart setup once the restart type's minimum dead-ball time has passed, and takes the restart once it is ready —
/// or, after <see cref="RestartTuning.SetupTimeoutSeconds"/>, places any stragglers directly and takes it anyway.
/// A kickoff is ready when everyone is in position; any other restart when the taker is on the ball and every
/// opponent respects the law's distance. Kickoffs that start a period skip the walk: the teams are already lined up.
/// The taker is chosen once, when play stops (<see cref="RestartTakers"/>).
/// </summary>
internal sealed class RestartSystem(PassResolver passResolver, OnBallWeights weights)
{
    private readonly TeamShapeSystem _shape = new();
    private Stoppage? _takerStoppage;
    private PlayerId _taker;

    /// <summary>Sets every player's movement target to their restart position.</summary>
    public void Position(MatchContext context)
    {
        var state = context.State;
        var stoppage = state.CurrentStoppage!;
        RestartLayout.Assign(state, _shape, context.Config.Restarts, stoppage, Taker(state, stoppage));
    }

    /// <summary>Dead ball → restart setup → restart taken, as far as the clock and positions allow this tick.</summary>
    public void Advance(MatchContext context)
    {
        var state = context.State;
        var tuning = context.Config.Restarts;
        var stoppage = state.CurrentStoppage!;
        var elapsed = (state.Clock.Tick - stoppage.StoppedAtTick) * context.Config.TickSeconds;
        var deadBall = DeadBallSeconds(stoppage, tuning);

        if (state.Phase == MatchPhase.Stopped && elapsed >= deadBall - 1e-9)
            state.BeginRestartSetup();
        if (state.Phase != MatchPhase.RestartSetup)
            return;

        var taker = Taker(state, stoppage);
        var lineUp = stoppage.Reason is StoppageReason.MatchStart or StoppageReason.PeriodStart;
        var timedOut = elapsed >= deadBall + tuning.SetupTimeoutSeconds - 1e-9;
        if (lineUp || timedOut)
            PlaceStragglers(state, tuning, stoppage, taker, everyone: lineUp || stoppage.Restart == RestartType.Kickoff);
        else if (!IsReady(state, tuning, stoppage, taker))
            return;

        Take(context, stoppage, taker);
    }

    private static double DeadBallSeconds(Stoppage stoppage, RestartTuning tuning) => stoppage.Reason switch
    {
        StoppageReason.Goal => tuning.GoalDeadBallSeconds,
        StoppageReason.BallOutOfPlay => stoppage.Restart switch
        {
            RestartType.ThrowIn => tuning.ThrowInDeadBallSeconds,
            RestartType.GoalKick => tuning.GoalKickDeadBallSeconds,
            RestartType.Corner => tuning.CornerDeadBallSeconds,
            _ => 0,
        },
        _ => 0,
    };

    private PlayerMatchState Taker(MatchState state, Stoppage stoppage)
    {
        if (!ReferenceEquals(_takerStoppage, stoppage))
        {
            _takerStoppage = stoppage;
            _taker = RestartTakers.For(state, stoppage).PlayerId;
        }

        return state.Player(_taker);
    }

    private static bool IsReady(MatchState state, RestartTuning tuning, Stoppage stoppage, PlayerMatchState taker)
    {
        if (stoppage.Restart == RestartType.Kickoff)
            return state.Players.All(p => p.Position.DistanceTo(p.MovementTarget) <= tuning.InPositionTolerance);

        return taker.Position.DistanceTo(stoppage.Location) <= tuning.InPositionTolerance
               && state.TeamPlayers(stoppage.AwardedTeam.Opponent())
                   .All(p => RestartLayout.IsLegalForOpponent(state, tuning, stoppage, p.Position));
    }

    /// <summary>Places the taker on the ball and every opponent breaking the law at their target (or everyone, for kickoffs).</summary>
    private static void PlaceStragglers(MatchState state, RestartTuning tuning, Stoppage stoppage, PlayerMatchState taker, bool everyone)
    {
        foreach (var player in state.Players)
        {
            var mustMove = everyone || player == taker
                || (player.Team != stoppage.AwardedTeam && !RestartLayout.IsLegalForOpponent(state, tuning, stoppage, player.Position));
            if (!mustMove)
                continue;
            player.Position = player.MovementTarget;
        }
    }

    private void Take(MatchContext context, Stoppage stoppage, PlayerMatchState taker)
    {
        var state = context.State;
        taker.Position = stoppage.Location;
        taker.MovementTarget = taker.Position;
        state.SetShapeFocus(TeamSide.Home, stoppage.Location);
        state.SetShapeFocus(TeamSide.Away, stoppage.Location);
        state.SetBallLoose(stoppage.Location);
        state.ResumePlay();

        if (stoppage.Restart == RestartType.Kickoff)
        {
            context.Emit(MatchEventType.Kickoff, actor: taker.PlayerId, team: stoppage.AwardedTeam, position: taker.Position);
            context.GiveBall(taker.PlayerId, PossessionCause.Kickoff);
            return;
        }

        context.Emit(MatchEventType.RestartTaken, actor: taker.PlayerId, team: stoppage.AwardedTeam, position: taker.Position,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("restart", stoppage.Restart.ToString()),
            ]));
        context.GiveBall(taker.PlayerId, stoppage.Restart switch
        {
            RestartType.ThrowIn => PossessionCause.ThrowIn,
            RestartType.GoalKick => PossessionCause.GoalKick,
            _ => PossessionCause.Corner,
        });

        if (ChooseReceiver(state, context.Config.Restarts, stoppage, taker) is { } receiver)
            PassExecution.Launch(context, passResolver.Resolve(state, new PassIntent(taker.PlayerId, receiver.PlayerId), context.Random));
    }

    /// <summary>
    /// The teammate the restart is played to: the best pass by the on-ball AI's pass utility (no randomness), within
    /// throwing range for a throw-in and among attackers in the penalty area for a corner (anyone if nobody is there).
    /// Null if nobody qualifies; the taker then keeps the ball and open-play decisions take over.
    /// </summary>
    private PlayerMatchState? ChooseReceiver(MatchState state, RestartTuning tuning, Stoppage stoppage, PlayerMatchState taker)
    {
        var pitch = state.Pitch;
        var takerX = TeamFrame.Convert(pitch, taker.Team, taker.Position).X;
        var candidates = state.TeamPlayers(taker.Team)
            .Where(p => p != taker && p.Position.DistanceTo(taker.Position) >= weights.MinPassDistance)
            .Where(p => stoppage.Restart != RestartType.ThrowIn || p.Position.DistanceTo(taker.Position) <= tuning.ThrowInMaxRange)
            .ToList();
        if (stoppage.Restart == RestartType.Corner
            && candidates.Where(p => state.Zones.IsIn(p.Team, p.Position, TacticalAreas.OppositionPenaltyArea)).ToList() is { Count: > 0 } inBox)
            candidates = inBox;

        double Utility(PlayerMatchState receiver)
        {
            var success = passResolver.Estimate(state, new PassIntent(taker.PlayerId, receiver.PlayerId)).SuccessProbability;
            var progression = TeamFrame.Convert(pitch, receiver.Team, receiver.Position).X - takerX;
            var goalProximity = Math.Max(0, 1 - receiver.Position.DistanceTo(pitch.OpponentGoalCentre(receiver.Team)) / weights.GoalProximityRange);
            return weights.PassSuccessWeight * success
                   + weights.PassProgressionWeight * progression
                   + weights.PassGoalProximityWeight * goalProximity
                   - (receiver.Definition.Position.IsGoalkeeper() ? weights.PassToKeeperPenalty : 0);
        }

        return candidates.MaxBy(Utility);
    }
}
