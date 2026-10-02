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

    public static TeamDefinition Team(string name = "Blue", int firstId = 1, PlayerStats? stats = null) =>
        new(name, Players(name, firstId, stats));

    public static MatchState Match(PlayerStats? homeStats = null, PlayerStats? awayStats = null) =>
        MatchState.Create(MatchConfig.Default, Team("Blue", 1, homeStats), Team("Red", 101, awayStats), KickoffLayout);

    public static PlayerMatchState Slot(this MatchState state, TeamSide side, PlayingPosition position) =>
        state.TeamPlayers(side).Single(p => p.Definition.Position == position);

    public static Vec2 KickoffLayout(TeamSide side, PlayerDefinition player) =>
        Formation433.KickoffPosition(PitchGeometry.Standard, side, player.Position);
}
