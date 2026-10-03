using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.AI;

public class UtilitySelectorTests
{
    private readonly MatchState _state = TestTeams.Match();
    private PlayerMatchState Player => _state.Players[5];

    private static ActionCandidate C(ActionKind kind, double utility) => new(kind, utility);

    [Fact]
    public void Clear_winner_is_chosen_without_consuming_randomness()
    {
        var candidates = new[] { C(ActionKind.Pass, 78), C(ActionKind.Carry, 43), C(ActionKind.Shoot, 9) };

        var chosen = new UtilitySelector().Choose(candidates, Player, _state, new ScriptedRandom());

        Assert.Equal(ActionKind.Pass, chosen.Kind);
    }

    [Fact]
    public void Options_far_below_the_best_are_never_chosen()
    {
        var candidates = new[] { C(ActionKind.Pass, 70), C(ActionKind.Carry, 66), C(ActionKind.Shoot, 50) };
        var random = new MatchRng(1);
        var selector = new UtilitySelector();

        var kinds = Enumerable.Range(0, 2000).Select(_ => selector.Choose(candidates, Player, _state, random).Kind).ToList();

        Assert.DoesNotContain(ActionKind.Shoot, kinds);
        Assert.Contains(ActionKind.Carry, kinds);
    }

    [Fact]
    public void Best_plausible_option_is_the_most_likely()
    {
        var candidates = new[] { C(ActionKind.Carry, 64), C(ActionKind.Pass, 70) };
        var random = new MatchRng(2);
        var selector = new UtilitySelector();

        var passes = Enumerable.Range(0, 4000).Count(_ => selector.Choose(candidates, Player, _state, random).Kind == ActionKind.Pass);

        // Weights: pass 70−62 = 8, carry 64−62 = 2 → pass 80 %.
        Assert.InRange(passes / 4000.0, 0.77, 0.83);
    }

    [Fact]
    public void Same_seed_gives_the_same_choices()
    {
        var candidates = new[] { C(ActionKind.Carry, 64), C(ActionKind.Pass, 70), C(ActionKind.Hold, 66) };
        var a = new MatchRng(9);
        var b = new MatchRng(9);
        var selector = new UtilitySelector();

        for (var i = 0; i < 200; i++)
            Assert.Equal(selector.Choose(candidates, Player, _state, a), selector.Choose(candidates, Player, _state, b));
    }

    [Fact]
    public void Modifiers_adjust_utilities_before_selection()
    {
        var candidates = new[] { C(ActionKind.Pass, 70), C(ActionKind.Shoot, 30) };
        var selector = new UtilitySelector([new BoostShooting(100)]);

        var chosen = selector.Choose(candidates, Player, _state, new ScriptedRandom());

        Assert.Equal(ActionKind.Shoot, chosen.Kind);
        Assert.Equal(130, chosen.Utility);
    }

    [Fact]
    public void Empty_candidate_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new UtilitySelector().Choose([], Player, _state, new MatchRng(1)));
    }

    private sealed class BoostShooting(double amount) : IUtilityModifier
    {
        public double Adjust(ActionCandidate candidate, PlayerMatchState player, MatchState state) =>
            candidate.Kind == ActionKind.Shoot ? amount : 0;
    }
}
