using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Core;

public class MatchStateInvariantsTests
{
    [Fact]
    public void Detects_player_outside_pitch_and_names_them()
    {
        var state = TestTeams.Match();
        var player = state.Players[4];
        player.Position = new Vec2(200, 34);

        var violations = MatchStateInvariants.FindViolations(state);

        var violation = Assert.Single(violations);
        Assert.Contains(player.PlayerId.ToString(), violation);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PlayerMatchState.MaxStamina + 0.5)]
    public void Detects_stamina_out_of_range(double stamina)
    {
        var state = TestTeams.Match();
        state.Players[0].CurrentStamina = stamina;

        Assert.Contains(MatchStateInvariants.FindViolations(state), v => v.Contains("Stamina"));
    }

    [Fact]
    public void Verify_throws_with_all_violations()
    {
        var state = TestTeams.Match();
        state.Players[0].CurrentStamina = -5;
        state.Players[1].Position = new Vec2(-3, -3);

        var ex = Assert.Throws<InvariantViolationException>(() => MatchStateInvariants.Verify(state));

        Assert.Equal(2, ex.Violations.Count);
    }

    [Fact]
    public void Verify_passes_on_valid_state()
    {
        MatchStateInvariants.Verify(TestTeams.Match());
    }
}
