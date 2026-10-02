using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Core;

public class MatchStateTests
{
    [Fact]
    public void New_match_has_22_unique_players_eleven_per_side()
    {
        var state = TestTeams.Match();

        Assert.Equal(22, state.Players.Count);
        Assert.Equal(22, state.Players.Select(p => p.PlayerId).Distinct().Count());
        Assert.Equal(11, state.TeamPlayers(TeamSide.Home).Count());
        Assert.Equal(11, state.TeamPlayers(TeamSide.Away).Count());
    }

    [Fact]
    public void Each_side_has_exactly_one_goalkeeper()
    {
        var state = TestTeams.Match();

        foreach (var side in Enum.GetValues<TeamSide>())
            Assert.Single(state.TeamPlayers(side), p => p.Definition.Position.IsGoalkeeper());
    }

    [Fact]
    public void New_match_starts_valid()
    {
        var state = TestTeams.Match();

        Assert.Empty(MatchStateInvariants.FindViolations(state));
        Assert.Equal(MatchPhase.Kickoff, state.Phase);
        Assert.Equal(0, state.Score.Home);
        Assert.Equal(0, state.Score.Away);
        Assert.True(state.Ball.IsLoose);
        Assert.Equal(state.Pitch.Centre, state.Ball.Position);
        Assert.Null(state.PossessionTeam);
    }

    [Fact]
    public void All_starting_positions_are_on_the_pitch()
    {
        var state = TestTeams.Match();
        Assert.All(state.Players, p => Assert.True(state.Pitch.Contains(p.Position), p.ToString()));
    }

    [Fact]
    public void Players_start_with_full_stamina()
    {
        var state = TestTeams.Match();
        Assert.All(state.Players, p => Assert.Equal(PlayerMatchState.MaxStamina, p.CurrentStamina));
    }

    [Fact]
    public void Rejects_player_ids_shared_across_teams()
    {
        var home = TestTeams.Team("Blue", 1);
        var away = TestTeams.Team("Red", 1);

        Assert.Throws<ArgumentException>(
            () => MatchState.Create(home, away, PitchGeometry.Standard, TestTeams.SpreadInOwnHalf));
    }

    [Fact]
    public void Rejects_starting_positions_off_the_pitch()
    {
        var ex = Assert.Throws<ArgumentException>(() => MatchState.Create(
            TestTeams.Team("Blue", 1),
            TestTeams.Team("Red", 101),
            PitchGeometry.Standard,
            (_, _) => new Vec2(-1, 34)));

        Assert.Contains("outside", ex.Message);
    }

    [Fact]
    public void Giving_the_ball_sets_possessor_team_and_ball_position()
    {
        var state = TestTeams.Match();
        var player = state.TeamPlayers(TeamSide.Away).First();

        state.GiveBallTo(player.PlayerId);

        Assert.True(state.HasBall(player.PlayerId));
        Assert.Equal(player.PlayerId, state.PossessionPlayerId);
        Assert.Equal(TeamSide.Away, state.PossessionTeam);
        Assert.Equal(player.Position, state.Ball.Position);
        Assert.False(state.Ball.IsLoose);
    }

    [Fact]
    public void Only_one_player_can_have_the_ball()
    {
        var state = TestTeams.Match();
        var first = state.Players[0];
        var second = state.Players[15];

        state.GiveBallTo(first.PlayerId);
        state.GiveBallTo(second.PlayerId);

        Assert.Single(state.Players, p => state.HasBall(p.PlayerId));
        Assert.True(state.HasBall(second.PlayerId));
        Assert.Equal(second.Team, state.PossessionTeam);
    }

    [Fact]
    public void Setting_ball_loose_clears_possession()
    {
        var state = TestTeams.Match();
        state.GiveBallTo(state.Players[3].PlayerId);

        state.SetBallLoose(new Vec2(40, 20));

        Assert.True(state.Ball.IsLoose);
        Assert.Null(state.PossessionTeam);
        Assert.Equal(new Vec2(40, 20), state.Ball.Position);
    }

    [Fact]
    public void Unknown_player_lookup_names_the_id()
    {
        var state = TestTeams.Match();
        var ex = Assert.Throws<KeyNotFoundException>(() => state.Player(new PlayerId(9999)));
        Assert.Contains("P9999", ex.Message);
    }
}
