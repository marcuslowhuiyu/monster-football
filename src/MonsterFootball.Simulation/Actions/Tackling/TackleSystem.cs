using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Tackling;

/// <summary>
/// Challenge decision for the defending team's CloseDownOpponent behaviour: the nearest eligible outfield
/// opponent within <see cref="ChallengeRange"/> of the carrier commits to a tackle with probability
/// rate × tick length, where rate = <see cref="AI.OffBallWeights.TackleBaseRatePerSecond"/> × (contact chance ÷ an even duel's)
/// × (1 + <see cref="AI.OffBallWeights.TackleDangerBonus"/> when the carrier is in its <see cref="Tactical.TacticalAreas.FinalThird"/>, i.e. the defender's own third). Defenders who are likely
/// to win the ball, or who are close to their own goal, commit sooner. At most one tackle per tick; cooldowns
/// stop the same players challenging repeatedly.
/// </summary>
internal sealed class TackleSystem(TackleResolver resolver, AI.OffBallWeights weights)
{
    public const double ChallengeRange = 1.8;
    public const double FirstTouchProtectionSeconds = 0.75;
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

        if (tackler is null || !context.Random.Roll(AttemptProbability(state, tackler, carrier, tickSeconds)))
            return;

        var result = resolver.Resolve(state, new TackleIntent(tackler.PlayerId, carrierId), context.Random);
        Execute(context, result, tackler, carrier);
    }

    private double AttemptProbability(MatchState state, PlayerMatchState tackler, PlayerMatchState carrier, double tickSeconds)
    {
        var evenDuel = resolver.ContactProbability(PlayerStats.Uniform(5), PlayerStats.Uniform(5));
        var confidence = resolver.ContactProbability(tackler, carrier) / evenDuel;
        var dangerous = state.Zones.IsIn(carrier.Team, carrier.Position, Tactical.TacticalAreas.FinalThird);
        var rate = weights.TackleBaseRatePerSecond * confidence * (dangerous ? 1 + weights.TackleDangerBonus : 1);
        return Math.Min(1, rate * tickSeconds);
    }

    private static void Execute(MatchContext context, TackleResult result, PlayerMatchState tackler, PlayerMatchState carrier)
    {
        var state = context.State;
        long Ticks(double seconds) => (long)Math.Ceiling(seconds / context.Config.TickSeconds);
        var tick = state.Clock.Tick;

        context.Emit(MatchEventType.TackleAttempted, actor: tackler.PlayerId, target: carrier.PlayerId,
            team: tackler.Team, position: result.Position,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("contact", result.ContactProbability.ToString("0.###", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("clean", result.CleanProbability.ToString("0.###", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("stamina", tackler.CurrentStamina.ToString("0.###", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("carrierStamina", carrier.CurrentStamina.ToString("0.###", CultureInfo.InvariantCulture)),
            ]));
        tackler.TackleCooldownUntilTick = tick + Ticks(AttemptCooldownSeconds);

        switch (result.Outcome)
        {
            case TackleOutcome.Won:
                context.Emit(MatchEventType.TackleWon, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: result.Position);
                context.GiveBall(tackler.PlayerId, PossessionCause.Tackle);
                carrier.TackleCooldownUntilTick = tick + Ticks(DispossessedCooldownSeconds);
                break;

            case TackleOutcome.Lost:
                context.Emit(MatchEventType.TackleLost, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: result.Position);
                tackler.TackleCooldownUntilTick = tick + Ticks(BeatenCooldownSeconds);
                break;

            case TackleOutcome.Foul:
                var spot = carrier.Position;
                context.Emit(MatchEventType.FoulCommitted, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: spot,
                    metadata: ImmutableSortedDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string>("chance", result.FoulProbability.ToString("0.###", CultureInfo.InvariantCulture)),
                        new KeyValuePair<string, string>("severity", result.Severity.ToString("0.###", CultureInfo.InvariantCulture)),
                    ]));
                if (state.Zones.IsIn(tackler.Team, spot, Tactical.TacticalAreas.OwnPenaltyArea))
                    context.StopPlay(StoppageReason.Foul, RestartType.Penalty, carrier.Team, state.Pitch.PenaltySpot(tackler.Team), tackler.PlayerId, carrier.PlayerId);
                else
                    context.StopPlay(StoppageReason.Foul, RestartType.DirectFreeKick, carrier.Team, spot, tackler.PlayerId, carrier.PlayerId);
                break;

            case TackleOutcome.LooseBall:
                state.SetBallLoose(state.Ball.Position);
                state.RecordTouch(tackler.PlayerId);
                var crossing = Laws.OutOfPlay.Crossing(state.Pitch, state.Ball.Position, result.BallPosition);
                context.Emit(MatchEventType.TackleLooseBall, actor: tackler.PlayerId, target: carrier.PlayerId,
                    team: tackler.Team, position: crossing?.Point ?? result.BallPosition);
                carrier.TackleCooldownUntilTick = tick + Ticks(DispossessedCooldownSeconds);
                Laws.OutOfPlay.MoveLoose(context, result.BallPosition);
                break;
        }
    }
}
