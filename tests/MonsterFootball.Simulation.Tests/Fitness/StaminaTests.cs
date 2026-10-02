using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Fitness;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Fitness;

public class StaminaTests
{
    private const double Tick = 0.25;

    private static PlayerStats WithStamina(int stamina) => new(5, 5, 5, 5, 5, 5, stamina, 5, 5);

    /// <summary>Runs the given player toward a far target for <paramref name="ticks"/> ticks of movement + stamina.</summary>
    private static void Run(Scenario s, PlayerMatchState player, MovementIntensity intensity, int ticks)
    {
        var movement = new MovementSystem();
        var stamina = new StaminaSystem();
        for (var i = 0; i < ticks; i++)
        {
            player.MovementTarget = player.Position + new Vec2(player.Team == TeamSide.Home ? 5 : -5, 0);
            if (player.MovementTarget.X > 100) player.Position = new Vec2(10, player.Position.Y);
            player.MovementIntensity = intensity;
            movement.Apply(s.State, Tick);
            stamina.Apply(s.Context);
        }
    }

    [Fact]
    public void Movement_reduces_stamina()
    {
        var s = new Scenario();
        var player = s.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);

        Run(s, player, MovementIntensity.Jog, 200);

        Assert.True(player.CurrentStamina < PlayerMatchState.MaxStamina);
    }

    [Fact]
    public void Standing_still_costs_nothing()
    {
        var s = new Scenario();
        var player = s.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);
        var stamina = new StaminaSystem();

        for (var i = 0; i < 200; i++)
        {
            new MovementSystem().Apply(s.State, Tick);
            stamina.Apply(s.Context);
        }

        Assert.Equal(PlayerMatchState.MaxStamina, player.CurrentStamina);
    }

    [Fact]
    public void Sprinting_costs_more_than_jogging_for_the_same_time()
    {
        var jog = new Scenario();
        var jogger = jog.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);
        Run(jog, jogger, MovementIntensity.Jog, 200);

        var sprint = new Scenario();
        var sprinter = sprint.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);
        Run(sprint, sprinter, MovementIntensity.Sprint, 200);

        Assert.True(sprinter.CurrentStamina < jogger.CurrentStamina);
    }

    [Fact]
    public void High_stamina_players_fatigue_more_slowly_under_equivalent_conditions()
    {
        var fit = new Scenario(homeStats: WithStamina(9));
        var fitPlayer = fit.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);
        Run(fit, fitPlayer, MovementIntensity.Sprint, 400);

        var unfit = new Scenario(homeStats: WithStamina(2));
        var unfitPlayer = unfit.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);
        Run(unfit, unfitPlayer, MovementIntensity.Sprint, 400);

        var fitDrain = PlayerMatchState.MaxStamina - fitPlayer.CurrentStamina;
        var unfitDrain = PlayerMatchState.MaxStamina - unfitPlayer.CurrentStamina;
        Assert.True(fitDrain < unfitDrain, $"fit drained {fitDrain:0.00}, unfit {unfitDrain:0.00}");
    }

    [Fact]
    public void Stamina_never_drops_below_zero()
    {
        var s = new Scenario(homeStats: WithStamina(1));
        var player = s.Place(TeamSide.Home, PlayingPosition.CM, 20, 34);
        player.CurrentStamina = 0.01;

        Run(s, player, MovementIntensity.Sprint, 50);

        Assert.Equal(PlayerMatchState.MinStamina, player.CurrentStamina);
        MatchStateInvariants.Verify(s.State);
    }

    [Fact]
    public void Actions_cost_stamina_via_their_events()
    {
        var s = new Scenario();
        var passer = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var tackler = s.State.Slot(TeamSide.Away, PlayingPosition.CM);
        var shooter = s.State.Slot(TeamSide.Home, PlayingPosition.ST);

        s.Context.Emit(MatchEventType.PassAttempted, actor: passer.PlayerId);
        s.Context.Emit(MatchEventType.TackleAttempted, actor: tackler.PlayerId, target: passer.PlayerId);
        s.Context.Emit(MatchEventType.ShotTaken, actor: shooter.PlayerId);
        new StaminaSystem().Apply(s.Context);

        Assert.Equal(100 - StaminaSystem.PassCost - StaminaSystem.DuelCost, passer.CurrentStamina, precision: 9);
        Assert.Equal(100 - StaminaSystem.TackleCost, tackler.CurrentStamina, precision: 9);
        Assert.Equal(100 - StaminaSystem.ShotCost, shooter.CurrentStamina, precision: 9);
    }

    [Fact]
    public void Each_event_is_charged_only_once()
    {
        var s = new Scenario();
        var passer = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var system = new StaminaSystem();

        s.Context.Emit(MatchEventType.PassAttempted, actor: passer.PlayerId);
        system.Apply(s.Context);
        system.Apply(s.Context);

        Assert.Equal(100 - StaminaSystem.PassCost, passer.CurrentStamina, precision: 9);
    }

    [Theory]
    [InlineData(1, 1.24)]
    [InlineData(5, 1.0)]
    [InlineData(10, 0.7)]
    public void Drain_multiplier_falls_with_the_stamina_attribute(int attribute, double expected)
    {
        Assert.Equal(expected, FatigueModel.DrainMultiplier(attribute), precision: 9);
    }

    [Fact]
    public void Fatigue_penalties_are_small_and_bounded()
    {
        Assert.Equal(1, FatigueModel.EffectivenessFactor(100), precision: 9);
        Assert.Equal(1 - FatigueModel.MaxEffectivenessPenalty, FatigueModel.EffectivenessFactor(0), precision: 9);
        Assert.Equal(1 - FatigueModel.MaxSpeedPenalty, FatigueModel.SpeedFactor(0), precision: 9);
        Assert.InRange(FatigueModel.MaxEffectivenessPenalty, 0.05, 0.25);
    }

    [Fact]
    public void Tired_passers_are_less_accurate()
    {
        static double Accuracy(double stamina)
        {
            var s = new Scenario();
            var passer = s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
            var receiver = s.Place(TeamSide.Home, PlayingPosition.ST, 75, 34);
            passer.CurrentStamina = stamina;
            return new PassResolver().Estimate(s.State, new PassIntent(passer.PlayerId, receiver.PlayerId)).AccuracyProbability;
        }

        Assert.True(Accuracy(10) < Accuracy(100));
    }

    [Fact]
    public void Tired_tacklers_make_contact_less_often()
    {
        var s = new Scenario();
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var tackler = s.State.Slot(TeamSide.Away, PlayingPosition.CM);
        var resolver = new TackleResolver();

        var fresh = resolver.ContactProbability(tackler, carrier);
        tackler.CurrentStamina = 10;
        var tired = resolver.ContactProbability(tackler, carrier);

        Assert.True(tired < fresh);
    }

    [Fact]
    public void Tired_shooters_and_keepers_perform_worse()
    {
        static ShotEstimate Estimate(double shooterStamina, double keeperStamina)
        {
            var s = new Scenario();
            var shooter = s.Place(TeamSide.Home, PlayingPosition.ST, 90, 34);
            var keeper = s.Place(TeamSide.Away, PlayingPosition.GK, 103, 34);
            shooter.CurrentStamina = shooterStamina;
            keeper.CurrentStamina = keeperStamina;
            return new ShotResolver().Estimate(s.State, new ShotIntent(shooter.PlayerId));
        }

        Assert.True(Estimate(10, 100).OnTargetProbability < Estimate(100, 100).OnTargetProbability);
        Assert.True(Estimate(100, 10).SaveProbability < Estimate(100, 100).SaveProbability);
    }

    [Fact]
    public void Fresher_teammate_presses_instead_of_an_exhausted_one_at_similar_distance()
    {
        var s = new Scenario();
        s.Place(TeamSide.Away, PlayingPosition.CM, 50, 34);
        s.GiveBall(TeamSide.Away, PlayingPosition.CM);
        var tired = s.Place(TeamSide.Home, PlayingPosition.CM, 46, 34);
        var fresh = s.Place(TeamSide.Home, PlayingPosition.LCM, 45, 35);
        tired.CurrentStamina = 5;

        new TeamShapeSystem().Apply(s.State);

        Assert.Equal(MovementIntensity.Sprint, fresh.MovementIntensity);
        Assert.NotEqual(MovementIntensity.Sprint, tired.MovementIntensity);
    }
}
