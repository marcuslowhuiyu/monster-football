using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class StaminaMatchTests
{
    [Fact]
    public void Stamina_falls_during_a_match_and_stays_in_bounds()
    {
        var result = CheckedRun.ToCompletion(
            new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed: 9));

        Assert.All(result.FinalState.Players, p => Assert.InRange(p.CurrentStamina, PlayerMatchState.MinStamina, PlayerMatchState.MaxStamina));
        Assert.All(result.FinalState.Players.Where(p => !p.Definition.Position.IsGoalkeeper()),
            p => Assert.True(p.CurrentStamina < PlayerMatchState.MaxStamina, p.ToString()));
    }

    [Fact]
    public void Fitter_team_ends_the_match_fresher()
    {
        var fit = new PlayerStats(5, 5, 5, 5, 5, 5, 9, 5, 5);
        var unfit = new PlayerStats(5, 5, 5, 5, 5, 5, 2, 5, 5);
        var result = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1, fit), TestTeams.Team("Red", 101, unfit), seed: 4)
            .RunToCompletion();

        var home = result.FinalState.TeamPlayers(TeamSide.Home).Average(p => p.CurrentStamina);
        var away = result.FinalState.TeamPlayers(TeamSide.Away).Average(p => p.CurrentStamina);
        Assert.True(home > away, $"fit {home:0.0} vs unfit {away:0.0}");
    }
}
