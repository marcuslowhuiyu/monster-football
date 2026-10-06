using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Utility AI for the ball carrier. After the first touch, every tick: generate candidates
/// (hold, pass to each teammate, carry, shoot), let <see cref="UtilitySelector"/> pick one, execute it.
/// </summary>
internal sealed class OnBallDecisionSystem(
    OnBallCandidateGenerator generator,
    UtilitySelector selector,
    PassResolver passResolver,
    CarryResolver carryResolver,
    ShotResolver shotResolver)
{
    public const double FirstTouchSeconds = 0.5;

    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.PossessionPlayerId is not { } carrierId)
            return;

        var heldSeconds = (state.Clock.Tick - state.PossessionStartTick) * context.Config.TickSeconds;
        if (heldSeconds < FirstTouchSeconds)
            return;

        var carrier = state.Player(carrierId);
        var candidates = generator.Generate(state, carrier, heldSeconds);
        var choice = selector.Choose(candidates, carrier, state, context.Random);

        switch (choice.Intent)
        {
            case ShotIntent shot:
                ShotExecution.Strike(context, shotResolver.Resolve(state, shot, context.Random));
                break;
            case PassIntent pass:
                PassExecution.Launch(context, passResolver.Resolve(state, pass, context.Random));
                break;
            case CarryIntent carry:
                CarrySystem.Start(context, carry, carryResolver.Resolve(state, carry));
                break;
        }
    }
}
