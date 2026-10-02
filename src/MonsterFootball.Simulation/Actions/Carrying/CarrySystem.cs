using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Actions.Carrying;

/// <summary>Starts carries and ends them when their time is up, the target is reached, or the ball is gone.</summary>
internal sealed class CarrySystem
{
    public static void Start(MatchContext context, CarryIntent intent, CarryResult result)
    {
        var carrier = context.State.Player(intent.Carrier);
        carrier.CurrentIntent = intent;
        carrier.ActiveCarry = result;
        context.Emit(MatchEventType.CarryStarted, actor: intent.Carrier, team: carrier.Team, position: result.StartPosition);
    }

    /// <summary>Clears finished or invalid carries.</summary>
    public void Apply(MatchContext context)
    {
        var state = context.State;
        foreach (var player in state.Players)
        {
            if (player.ActiveCarry is not { } carry)
                continue;

            var finished = !state.HasBall(player.PlayerId)
                || state.Clock.Tick >= carry.EndTick
                || player.Position == carry.TargetPosition;

            if (finished)
                player.ClearIntent();
        }
    }
}
