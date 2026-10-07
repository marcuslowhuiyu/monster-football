using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Restarts;

/// <summary>
/// Runs a stoppage (ADR-023). While play is stopped every player heads for their restart position
/// (<see cref="Position"/>). <see cref="Advance"/> moves the dead ball on to the restart setup once the minimum
/// dead-ball time has passed, and takes the restart once everyone is in position — or, after
/// <see cref="RestartTuning.SetupTimeoutSeconds"/>, places any stragglers directly and takes it anyway.
/// Kickoffs that start a period skip the walk: the teams are already lined up.
/// </summary>
internal sealed class RestartSystem
{
    /// <summary>Sets every player's movement target to their restart position.</summary>
    public void Position(MatchState state)
    {
        var stoppage = state.CurrentStoppage!;
        foreach (var player in state.Players)
        {
            player.MovementTarget = Target(state, stoppage, player);
            player.MovementIntensity = MovementIntensity.Jog;
        }
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

        var lineUp = stoppage.Reason == StoppageReason.MatchStart;
        var timedOut = elapsed >= deadBall + tuning.SetupTimeoutSeconds - 1e-9;
        if (lineUp || timedOut)
            PlaceAll(state, stoppage);
        else if (!AllInPosition(state, stoppage, tuning))
            return;

        TakeKickoff(context, stoppage);
    }

    private static double DeadBallSeconds(Stoppage stoppage, RestartTuning tuning) => stoppage.Reason switch
    {
        StoppageReason.Goal => tuning.GoalDeadBallSeconds,
        _ => 0,
    };

    /// <summary>Kickoff positions: the 4-3-3 kickoff layout (legal by construction), the kicking side's striker on the ball.</summary>
    private static Vec2 Target(MatchState state, Stoppage stoppage, PlayerMatchState player) =>
        IsTaker(stoppage, player)
            ? stoppage.Location
            : Formation433.KickoffPosition(state.Pitch, player.Team, player.Definition.Position);

    private static bool IsTaker(Stoppage stoppage, PlayerMatchState player) =>
        player.Team == stoppage.AwardedTeam && player.Definition.Position == PlayingPosition.ST;

    private static bool AllInPosition(MatchState state, Stoppage stoppage, RestartTuning tuning) =>
        state.Players.All(p => p.Position.DistanceTo(Target(state, stoppage, p)) <= tuning.InPositionTolerance);

    private static void PlaceAll(MatchState state, Stoppage stoppage)
    {
        foreach (var player in state.Players)
        {
            player.Position = Target(state, stoppage, player);
            player.MovementTarget = player.Position;
        }
    }

    /// <summary>The kicking side's striker takes the ball on the centre spot; play resumes.</summary>
    private static void TakeKickoff(MatchContext context, Stoppage stoppage)
    {
        var state = context.State;
        var taker = state.TeamPlayers(stoppage.AwardedTeam).Single(p => p.Definition.Position == PlayingPosition.ST);
        taker.Position = stoppage.Location;
        taker.MovementTarget = taker.Position;
        state.SetShapeFocus(TeamSide.Home, stoppage.Location);
        state.SetShapeFocus(TeamSide.Away, stoppage.Location);
        state.SetBallLoose(stoppage.Location);
        state.ResumePlay();
        context.Emit(MatchEventType.Kickoff, actor: taker.PlayerId, team: stoppage.AwardedTeam, position: taker.Position);
        context.GiveBall(taker.PlayerId, PossessionCause.Kickoff);
    }
}
