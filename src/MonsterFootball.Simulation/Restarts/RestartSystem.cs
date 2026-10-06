using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Restarts;

/// <summary>Kickoffs: resetting the teams after a goal and taking the kickoff itself.</summary>
internal sealed class RestartSystem
{
    /// <summary>Returns every player to the 4-3-3 kickoff layout and places the ball on the centre spot.</summary>
    public void ResetForKickoff(MatchState state)
    {
        state.SetBallLoose(state.Pitch.Centre);
        state.SetShapeFocus(TeamSide.Home, state.Pitch.Centre);
        state.SetShapeFocus(TeamSide.Away, state.Pitch.Centre);
        foreach (var player in state.Players)
        {
            player.Position = Formation433.KickoffPosition(state.Pitch, player.Team, player.Definition.Position);
            player.MovementTarget = player.Position;
            player.MovementIntensity = MovementIntensity.Jog;
            player.TackleCooldownUntilTick = 0;
            player.ClearIntent();
            player.OffBallPlan = null;
        }

        state.Phase = MatchPhase.Kickoff;
    }

    /// <summary>The kicking side's striker takes the ball on the centre spot; play resumes.</summary>
    public void TakeKickoff(MatchContext context)
    {
        var state = context.State;
        var side = state.KickoffTeam;
        var taker = state.TeamPlayers(side).Single(p => p.Definition.Position == PlayingPosition.ST);
        taker.Position = state.Pitch.Centre;
        taker.MovementTarget = taker.Position;
        context.Emit(MatchEventType.Kickoff, actor: taker.PlayerId, team: side, position: taker.Position);
        context.GiveBall(taker.PlayerId, PossessionCause.Kickoff);
        state.Phase = MatchPhase.OpenPlay;
    }
}
