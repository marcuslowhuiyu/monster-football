using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Placeholder on-ball decision until utility AI (M1.9). Once the first touch is taken the carrier:
/// passes immediately when pressed (opponent within <see cref="PressureRadius"/>);
/// carries when there is space ahead; otherwise passes after <see cref="HoldSeconds"/>.
/// A carry in progress is never interrupted here.
/// </summary>
internal sealed class OnBallDecisionSystem(PassTargetSelector passSelector, PassResolver passResolver, CarryResolver carryResolver)
{
    public const double FirstTouchSeconds = 0.5;
    public const double PressureRadius = 3;
    public const double HoldSeconds = 2.5;
    public const double SpaceAheadRadius = 7;

    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.PossessionPlayerId is not { } carrierId)
            return;

        var carrier = state.Player(carrierId);
        var heldSeconds = (state.Clock.Tick - state.PossessionStartTick) * context.Config.TickSeconds;
        if (heldSeconds < FirstTouchSeconds || carrier.ActiveCarry is not null)
            return;

        var opponents = state.TeamPlayers(carrier.Team.Opponent()).ToList();
        var pressed = opponents.Any(o => o.Position.DistanceTo(carrier.Position) < PressureRadius);

        if (pressed)
        {
            TryPass(context, carrier);
            return;
        }

        if (HasSpaceAhead(state.Pitch, carrier, opponents))
        {
            var intent = new CarryIntent(carrierId, carryResolver.ChooseDestination(state, carrier));
            CarrySystem.Start(context, intent, carryResolver.Resolve(state, intent));
            return;
        }

        if (heldSeconds >= HoldSeconds)
            TryPass(context, carrier);
    }

    private void TryPass(MatchContext context, PlayerMatchState carrier)
    {
        var intent = passSelector.Choose(context.State, carrier);
        if (intent is not null)
            PassExecution.Launch(context, passResolver.Resolve(context.State, intent, context.Random));
    }

    /// <summary>No opponent within <see cref="SpaceAheadRadius"/> that is level with or ahead of the carrier.</summary>
    private static bool HasSpaceAhead(PitchGeometry pitch, PlayerMatchState carrier, IEnumerable<PlayerMatchState> opponents)
    {
        var local = TeamFrame.Convert(pitch, carrier.Team, carrier.Position);
        if (local.X > pitch.Length - CarryResolver.GoalLineMargin - 1)
            return false;

        return !opponents.Any(o =>
        {
            var oLocal = TeamFrame.Convert(pitch, carrier.Team, o.Position);
            return oLocal.X > local.X - 1 && oLocal.DistanceTo(local) < SpaceAheadRadius;
        });
    }
}
