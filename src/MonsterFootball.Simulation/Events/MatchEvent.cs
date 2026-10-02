using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Events;

/// <summary>An immutable record of something that happened in the match.</summary>
public sealed record MatchEvent(
    MatchTime Timestamp,
    MatchEventType Type,
    PlayerId? ActorPlayerId = null,
    PlayerId? TargetPlayerId = null,
    TeamSide? Team = null,
    Vec2? Position = null)
{
    /// <summary>Extra details, sorted by key so rendering is deterministic.</summary>
    public ImmutableSortedDictionary<string, string> Metadata { get; init; } =
        ImmutableSortedDictionary<string, string>.Empty;

    /// <summary>Canonical single-line form; used by determinism tests and debugging.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{Timestamp.Seconds:0.###} {Type}");
        if (Team is { } team) sb.Append($" team={team}");
        if (ActorPlayerId is { } actor) sb.Append($" actor={actor}");
        if (TargetPlayerId is { } target) sb.Append($" target={target}");
        if (Position is { } position) sb.Append($" at={position}");
        foreach (var (key, value) in Metadata)
            sb.Append($" {key}={value}");
        return sb.ToString();
    }
}
