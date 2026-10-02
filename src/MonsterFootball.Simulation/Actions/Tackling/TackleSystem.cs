using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Tackling;

/// <summary>
/// Placeholder challenge decision until utility AI (M1.9): the nearest eligible outfield opponent
/// within <see cref="ChallengeRange"/> of the carrier commits to a tackle with probability
/// <see cref="AttemptRatePerSecond"/> × tick length. At most one tackle per tick.
/// Cooldowns stop the same players challenging repeatedly.
/// </summary>
internal sealed class TackleSystem(TackleResolver resolver)
{
    public const double ChallengeRange = 1.8;
    public const double FirstTouchProtectionSeconds = 0.75;
    public const double AttemptRatePerSecond = 1.0;
    public const double AttemptCooldownSeconds = 1.5;
    public const double BeatenCooldownSeconds = 3.0;
    public const double DispossessedCooldownSeconds = 2.0;

    public void Apply(MatchContext context)
    {
        var state = context.State;
        if (state.PossessionPlayerId is not { } carrierId)
            return;

        var tick = state.Clock.Tick;
        var tickSeconds = context.Config.TickSeconds;
        if ((tick - state.PossessionStartTick) * tickSeconds < FirstTouchProtectionSeconds)
            return;

        var carrier = state.Player(carrierId);
        if (carrier.Definition.Position.IsGoalkeeper() && state.Pitch.IsInPenaltyArea(carrier.Position, carrier.Team))
            return; // a keeper holding the ball in their own area cannot be challenged

        var tackler = state.TeamPlayers(carrier.Team.Opponent())
            .Where(p => !p.Definition.Position.IsGoalkeeper()
                        && p.TackleCooldownUntilTick <= tick
                        && p.Position.DistanceTo(carrier.Position) <= ChallengeRange)
            .MinBy(p => p.Position.DistanceTo(carrier.Position));

        if (tackler is null || !context.Random.Roll(Math.Min(1, AttemptRatePerSecond * tickSeconds)))
            return;

        var result = resolver.Resolve(state, new TackleIntent(tackler.PlayerId, carrierId), context.Random);
        Execute(context, result, tackler, carrier);
    }

    private static void Execute(MatchContext context, TackleResult result, PlayerMatchState tackler, PlayerMatchState carrier)
    {
        var state = context.State;
        long Ticks(double seconds) => (long)Math.Ceiling(seconds / context.Config.TickSeconds);
        var tick = state.Clock.Tick;

        context.Emit(MatchEventType.TackleAttempted, actor: tackler.PlayerId, target: carrier.PlayerId,
            team: tackler.Team, position: result.Position);
        tackler.TackleCooldownUntilTick = tick + Ticks(AttemptCooldownSeconds);

        switch (result.Outcome)
        {
            case TackleOutcome.Won:
                context.Emit(MatchEventType.TackleWon, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: result.Position);
                context.GiveBall(tackler.PlayerId);
                carrier.TackleCooldownUntilTick = tick + Ticks(DispossessedCooldownSeconds);
                break;

            case TackleOutcome.Lost:
                context.Emit(MatchEventType.TackleLost, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: result.Position);
                tackler.TackleCooldownUntilTick = tick + Ticks(BeatenCooldownSeconds);
                break;

            case TackleOutcome.LooseBall:
                state.SetBallLoose(result.BallPosition);
                context.Emit(MatchEventType.TackleLooseBall, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: result.BallPosition);
                carrier.TackleCooldownUntilTick = tick + Ticks(DispossessedCooldownSeconds);
                break;
        }
    }
}
