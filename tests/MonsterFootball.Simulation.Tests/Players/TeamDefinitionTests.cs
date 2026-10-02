using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Players;

public class TeamDefinitionTests
{
    [Fact]
    public void Valid_433_team_has_eleven_players_and_one_goalkeeper()
    {
        var team = TestTeams.Team();

        Assert.Equal(11, team.Players.Count);
        Assert.Equal(PlayingPosition.GK, team.Goalkeeper.Position);
        Assert.Single(team.Players, p => p.Position.IsGoalkeeper());
    }

    [Fact]
    public void Rejects_ten_players()
    {
        var players = TestTeams.Players("Blue", 1);
        players.RemoveAt(players.Count - 1);

        var ex = Assert.Throws<InvalidTeamException>(() => new TeamDefinition("Blue", players));
        Assert.Contains(ex.Errors, e => e.Contains("exactly 11"));
    }

    [Fact]
    public void Rejects_twelve_players()
    {
        var players = TestTeams.Players("Blue", 1);
        players.Add(new PlayerDefinition(new PlayerId(99), "Extra", 99, PlayingPosition.ST, PlayerStats.Uniform(5)));

        Assert.Throws<InvalidTeamException>(() => new TeamDefinition("Blue", players));
    }

    [Fact]
    public void Rejects_two_goalkeepers()
    {
        var players = TestTeams.Players("Blue", 1);
        var striker = players.Single(p => p.Position == PlayingPosition.ST);
        players[players.IndexOf(striker)] =
            new PlayerDefinition(striker.Id, striker.Name, striker.ShirtNumber, PlayingPosition.GK, striker.Stats);

        var ex = Assert.Throws<InvalidTeamException>(() => new TeamDefinition("Blue", players));
        Assert.Contains(ex.Errors, e => e.Contains("exactly one goalkeeper"));
    }

    [Fact]
    public void Rejects_no_goalkeeper()
    {
        var players = TestTeams.Players("Blue", 1);
        var keeper = players.Single(p => p.Position.IsGoalkeeper());
        players[players.IndexOf(keeper)] =
            new PlayerDefinition(keeper.Id, keeper.Name, keeper.ShirtNumber, PlayingPosition.ST, keeper.Stats);

        var ex = Assert.Throws<InvalidTeamException>(() => new TeamDefinition("Blue", players));
        Assert.Contains(ex.Errors, e => e.Contains("but has 0"));
    }

    [Fact]
    public void Rejects_duplicate_player_ids()
    {
        var players = TestTeams.Players("Blue", 1);
        var first = players[0];
        var second = players[1];
        players[1] = new PlayerDefinition(first.Id, second.Name, second.ShirtNumber, second.Position, second.Stats);

        var ex = Assert.Throws<InvalidTeamException>(() => new TeamDefinition("Blue", players));
        Assert.Contains(ex.Errors, e => e.Contains($"Player id {first.Id}"));
    }

    [Fact]
    public void Rejects_duplicate_shirt_numbers()
    {
        var players = TestTeams.Players("Blue", 1);
        var second = players[1];
        players[1] = new PlayerDefinition(second.Id, second.Name, players[0].ShirtNumber, second.Position, second.Stats);

        var ex = Assert.Throws<InvalidTeamException>(() => new TeamDefinition("Blue", players));
        Assert.Contains(ex.Errors, e => e.Contains("Shirt number"));
    }
}
