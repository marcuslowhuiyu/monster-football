using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Playback;

/// <summary>Placeholder squads for the developer viewer: one player per 4-3-3 slot, every attribute 5 (as the debug runner).</summary>
public static class SampleTeams
{
    public static TeamDefinition Create(string name, int firstId) =>
        new(name, Enum.GetValues<PlayingPosition>().Select((slot, i) =>
            new PlayerDefinition(new PlayerId(firstId + i), $"{name} {slot}", i + 1, slot, PlayerStats.Uniform(5))));
}
