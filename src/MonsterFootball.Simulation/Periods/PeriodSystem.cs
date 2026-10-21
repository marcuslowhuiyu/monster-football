using System.Collections.Immutable;
using System.Globalization;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Periods;

/// <summary>
/// Periods and added time (ADR-024), checked once per tick after the clock advances:
/// at the end of regular time the period's added time is announced (allowances rounded up to whole minutes);
/// once regular plus added time is up, the period ends at the first moment play is stopped — a ball in flight is never
/// cut off, and a penalty is taken first — or, if play has not stopped within the safety cap, at the next tick with no ball in flight.
/// The last period's end finishes the match; otherwise the next period starts straight away (half-time takes no
/// ticks) with a kickoff for the side that did not take the opening one.
/// </summary>
internal sealed class PeriodSystem
{
    public void Apply(MatchContext context)
    {
        var state = context.State;
        var clock = state.Clock;
        if (state.Phase == MatchPhase.Finished)
            return; // abandoned

        if (clock.AddedTimeTicks is null && clock.Tick >= clock.RegularEndTick)
            AnnounceAddedTime(context);

        if (clock.AddedTimeTicks is not { } added || clock.Tick < clock.RegularEndTick + added)
            return;

        // A penalty awarded as time runs out is still taken (Law 14): wait for it, even past the safety cap — its
        // dead-ball time (ADR-037) and setup timeout are bounded.
        var penaltyPending = state.CurrentStoppage?.Restart == RestartType.Penalty;
        var stopped = state.Phase is MatchPhase.Stopped or MatchPhase.RestartSetup && !penaltyPending;
        var capReached = clock.Tick >= clock.RegularEndTick + added + context.Config.TicksFor(context.Config.AddedTime.SafetyCapSeconds)
                         && !state.Ball.IsInFlight && !penaltyPending;
        if (stopped || capReached)
            EndPeriod(context, stopped ? "Stoppage" : "SafetyCap");
    }

    private static void AnnounceAddedTime(MatchContext context)
    {
        var state = context.State;
        var minutes = (int)Math.Ceiling(state.AddedTimeAllowanceSeconds / 60 - 1e-9);
        state.Clock.AnnounceAddedTime(context.Config.TicksFor(minutes * 60));
        context.Emit(MatchEventType.AddedTimeAnnounced,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("minutes", minutes.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("period", state.Clock.Period.ToString(CultureInfo.InvariantCulture)),
            ]));
    }

    private static void EndPeriod(MatchContext context, string end)
    {
        var state = context.State;
        context.Emit(MatchEventType.PeriodEnded,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("end", end),
                new KeyValuePair<string, string>("period", state.Clock.Period.ToString(CultureInfo.InvariantCulture)),
            ]));

        if (state.Clock.Period >= context.Config.Periods)
        {
            state.Phase = MatchPhase.Finished;
            context.Emit(MatchEventType.MatchFinished);
            return;
        }

        state.Clock.StartNextPeriod();
        state.AddedTimeAllowanceSeconds = 0;
        context.BeginStoppage(StoppageReason.PeriodStart, RestartType.Kickoff, state.OpeningKickoffTeam.Opponent(), state.Pitch.Centre);
    }
}
