using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Fitness;

/// <summary>
/// Drains stamina each tick from (a) metres covered, priced by movement intensity, and
/// (b) actions taken, read from the events emitted since the last tick. Cost is scaled by
/// <see cref="FatigueModel.DrainMultiplier"/>. No recovery yet.
/// </summary>
internal sealed class StaminaSystem
{
    // Stamina points per metre covered.
    public const double JogCostPerMetre = 0.002;
    public const double CarryCostPerMetre = 0.003;
    public const double SprintCostPerMetre = 0.004;

    // Stamina points per action.
    public const double PassCost = 0.05;
    public const double CarryStartCost = 0.1;
    public const double TackleCost = 0.2;
    public const double DuelCost = 0.1;
    public const double ShotCost = 0.3;

    private int _processedEvents;

    public static double CostPerMetre(MovementIntensity intensity) => intensity switch
    {
        MovementIntensity.Sprint => SprintCostPerMetre,
        MovementIntensity.Carry => CarryCostPerMetre,
        _ => JogCostPerMetre,
    };

    public void Apply(MatchContext context)
    {
        var state = context.State;

        foreach (var player in state.ActivePlayers)
            Drain(player, player.LastTickDistance * CostPerMetre(player.MovementIntensity));

        var events = context.Events;
        for (; _processedEvents < events.Count; _processedEvents++)
            ApplyActionCost(state, events[_processedEvents]);
    }

    private static void ApplyActionCost(Core.MatchState state, MatchEvent e)
    {
        switch (e.Type)
        {
            case MatchEventType.PassAttempted:
                Drain(state.Player(e.ActorPlayerId!.Value), PassCost);
                break;
            case MatchEventType.CarryStarted:
                Drain(state.Player(e.ActorPlayerId!.Value), CarryStartCost);
                break;
            case MatchEventType.TackleAttempted:
                Drain(state.Player(e.ActorPlayerId!.Value), TackleCost);
                Drain(state.Player(e.TargetPlayerId!.Value), DuelCost);
                break;
            case MatchEventType.ShotTaken:
                Drain(state.Player(e.ActorPlayerId!.Value), ShotCost);
                break;
        }
    }

    private static void Drain(PlayerMatchState player, double baseCost)
    {
        if (baseCost <= 0)
            return;

        var cost = baseCost * FatigueModel.DrainMultiplier(player.Definition.Stats.Stamina);
        player.CurrentStamina = Math.Max(PlayerMatchState.MinStamina, player.CurrentStamina - cost);
    }
}
