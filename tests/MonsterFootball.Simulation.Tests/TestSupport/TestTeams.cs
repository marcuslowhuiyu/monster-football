using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

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
        MatchState.Create(Team("Blue", 1), Team("Red", 101), PitchGeometry.Standard, SpreadInOwnHalf);

    /// <summary>Deterministic, valid placeholder layout until formation positioning exists (M1.4).</summary>
    public static Vec2 SpreadInOwnHalf(TeamSide side, PlayerDefinition player)
    {
        var index = (int)player.Position;
        var pitch = PitchGeometry.Standard;
        var depth = 5 + index * 4;
        var x = side == TeamSide.Home ? depth : pitch.Length - depth;
        var y = 4 + index * 6;
        return new Vec2(x, y);
    }
}
