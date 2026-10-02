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

    /// <summary>Gives the ball to a player, emitting PossessionChanged if control passes to the other team.</summary>
    public void GiveBall(PlayerId playerId)
    {
        var previousTeam = State.LastPossessionTeam;
        State.GiveBallTo(playerId);
        var player = State.Player(playerId);
        if (previousTeam is { } previous && previous != player.Team)
            Emit(MatchEventType.PossessionChanged, actor: playerId, team: player.Team, position: player.Position);
    }
}
