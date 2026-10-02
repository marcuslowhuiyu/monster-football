using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Tests.TestSupport;

/// <summary>Builds valid 4-3-3 teams and matches for tests.</summary>
internal static class TestTeams
{
    private static readonly PlayingPosition[] Slots = Enum.GetValues<PlayingPosition>();

    public static List<PlayerDefinition> Players(string teamName, int firstId, PlayerStats? stats = null) =>
        Slots.Select((slot, i) => new PlayerDefinition(
                new PlayerId(firstId + i),
                $"{teamName} {slot}",
                shirtNumber: i + 1,
                slot,
                stats ?? PlayerStats.Uniform(5)))
            .ToList();

    public static TeamDefinition Team(string name = "Blue", int firstId = 1) => new(name, Players(name, firstId));

    public static MatchState Match() =>
        MatchState.Create(MatchConfig.Default, Team("Blue", 1), Team("Red", 101), KickoffLayout);

    public static Vec2 KickoffLayout(TeamSide side, PlayerDefinition player) =>
        Formation433.KickoffPosition(PitchGeometry.Standard, side, player.Position);
}
