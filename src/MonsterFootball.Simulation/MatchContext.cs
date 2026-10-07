using System.Collections.Immutable;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation;

/// <summary>
/// What a system needs to act on a match: state, config, the match's random source and event output.
/// All possession transfers go through <see cref="GiveBall"/> so PossessionChanged is never missed.
/// </summary>
internal sealed class MatchContext(MatchConfig config, MatchState state, IMatchRandom random, MatchEventLog log)
{
    public MatchConfig Config { get; } = config;
    public MatchState State { get; } = state;
    public IMatchRandom Random { get; } = random;

    /// <summary>Events emitted so far, in order.</summary>
    public IReadOnlyList<MatchEvent> Events => log.Events;

    public void Emit(
        MatchEventType type,
        PlayerId? actor = null,
        PlayerId? target = null,
        TeamSide? team = null,
        Vec2? position = null,
        ImmutableSortedDictionary<string, string>? metadata = null) =>
        log.Add(new MatchEvent(State.Clock.Now, type, actor, target, team, position)
        {
            Metadata = metadata ?? ImmutableSortedDictionary<string, string>.Empty,
        });

    /// <summary>
    /// Stops play (ADR-023): records the stoppage, clears every player's ongoing action and plan, and emits
    /// PlayStopped (team = awarded team, position = restart location, metadata <c>reason</c> and <c>restart</c>).
    /// </summary>
    public void StopPlay(StoppageReason reason, RestartType restart, TeamSide awardedTeam, Vec2 location)
    {
        BeginStoppage(reason, restart, awardedTeam, location);
        if (reason == StoppageReason.Goal && State.Clock.Tick < State.Clock.RegularEndTick)
            State.AddedTimeAllowanceSeconds += Config.AddedTime.PerGoalSeconds;

        Emit(MatchEventType.PlayStopped, team: awardedTeam, position: location,
            metadata: ImmutableSortedDictionary.CreateRange(
            [
                new KeyValuePair<string, string>("reason", reason.ToString()),
                new KeyValuePair<string, string>("restart", restart.ToString()),
            ]));
    }

    /// <summary>Records a stoppage and clears every player's ongoing action and plan, without emitting an event.</summary>
    public void BeginStoppage(StoppageReason reason, RestartType restart, TeamSide awardedTeam, Vec2 location)
    {
        State.StopPlay(new Stoppage(reason, restart, awardedTeam, location, State.Clock.Tick));
        foreach (var player in State.Players)
        {
            player.ClearIntent();
            player.OffBallPlan = null;
            player.TackleCooldownUntilTick = 0;
        }
    }

    /// <summary>
    /// Gives the ball to a player, emitting PossessionChanged (with its <paramref name="cause"/>) if control passes
    /// to the other team.
    /// </summary>
    public void GiveBall(PlayerId playerId, PossessionCause cause)
    {
        var previousTeam = State.LastPossessionTeam;
        State.GiveBallTo(playerId);
        var player = State.Player(playerId);
        if (previousTeam is { } previous && previous != player.Team)
            Emit(MatchEventType.PossessionChanged, actor: playerId, team: player.Team, position: player.Position,
                metadata: ImmutableSortedDictionary.CreateRange(
                [
                    new KeyValuePair<string, string>(PossessionCauseExtensions.MetadataKey, cause.ToString()),
                ]));
    }
}
