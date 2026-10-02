namespace MonsterFootball.Simulation.Events;

/// <summary>Append-only, ordered history of a match's events.</summary>
public sealed class MatchEventLog
{
    private readonly List<MatchEvent> _events = [];

    public IReadOnlyList<MatchEvent> Events => _events;

    internal void Add(MatchEvent matchEvent) => _events.Add(matchEvent);
}
