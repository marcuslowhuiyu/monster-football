using MonsterFootball.Simulation.Actions.Passing;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Placeholder on-ball decision until utility AI (M1.9): the carrier passes to the selected
/// teammate once they have held the ball for <see cref="HoldSeconds"/>.
/// </summary>
internal sealed class PassDecisionSystem(PassTargetSelector selector, PassResolver resolver)
{
    public const double HoldSeconds = 2.5;

    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.PossessionPlayerId is not { } carrierId)
            return;

        var heldSeconds = (state.Clock.Tick - state.PossessionStartTick) * context.Config.TickSeconds;
        if (heldSeconds < HoldSeconds)
            return;

        var intent = selector.Choose(state, state.Player(carrierId));
        if (intent is null)
            return;

        PassExecution.Launch(context, resolver.Resolve(state, intent, context.Random));
    }
}
