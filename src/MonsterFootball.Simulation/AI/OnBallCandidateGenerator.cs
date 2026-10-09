using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Scores every option for the ball carrier (ADR-018). Utilities use resolver estimates, so the AI
/// judges options with the same maths that decides outcomes. Rough scale: a safe square pass ≈ 45,
/// a progressive pass ≈ 55–70, an open carry ≈ 55–60, a shot ≈ 22 + 318 × xG (offered from xG 0.03). Values live in OnBallWeights.
/// No pass is offered to a teammate the carrier judges offside (Awareness-dependent margin, D8).
/// </summary>
internal sealed class OnBallCandidateGenerator(
    OnBallWeights weights,
    PassResolver passResolver,
    CarryResolver carryResolver,
    ShotResolver shotResolver)
{
    private OnBallWeights W { get; } = weights;

    public IReadOnlyList<ActionCandidate> Generate(MatchState state, PlayerMatchState carrier, double heldSeconds)
    {
        var pitch = state.Pitch;
        var candidates = new List<ActionCandidate>();
        var isKeeper = carrier.Definition.Position.IsGoalkeeper();
        var opponents = state.TeamPlayers(carrier.Team.Opponent()).ToList();
        var nearest = opponents.Min(o => o.Position.DistanceTo(carrier.Position));
        var pressure = nearest < W.PressureRadius ? 1 - nearest / W.PressureRadius : 0;
        var local = TeamFrame.Convert(pitch, carrier.Team, carrier.Position);

        candidates.Add(new ActionCandidate(ActionKind.Hold, HoldUtility(state, carrier, heldSeconds, pressure)));

        if (!isKeeper && carrier.Position.DistanceTo(pitch.OpponentGoalCentre(carrier.Team)) <= W.MaxShootingDistance)
        {
            var intent = new ShotIntent(carrier.PlayerId);
            var xg = shotResolver.Estimate(state, intent).GoalProbability;
            if (xg >= W.MinShotXg)
                candidates.Add(new ActionCandidate(ActionKind.Shoot, W.ShotBase + W.ShotUtilityPerXg * xg, intent));
        }

        foreach (var receiver in state.TeamPlayers(carrier.Team))
        {
            if (receiver == carrier || receiver.Position.DistanceTo(carrier.Position) < W.MinPassDistance)
                continue;
            if (Laws.Offside.IsOffsidePosition(state, receiver, carrier.Position, W.OffsideMisjudgement(carrier.Definition.Stats.Awareness)))
                continue; // the passer sees them offside

            var intent = new PassIntent(carrier.PlayerId, receiver.PlayerId);
            var success = passResolver.Estimate(state, intent).SuccessProbability;
            var progression = TeamFrame.Convert(pitch, receiver.Team, receiver.Position).X - local.X;
            var utility = W.PassSuccessWeight * success
                + W.PassProgressionWeight * progression
                + W.PassGoalProximityWeight * GoalProximity(state, receiver)
                - (receiver.Definition.Position.IsGoalkeeper() ? W.PassToKeeperPenalty : 0);
            candidates.Add(new ActionCandidate(ActionKind.Pass, utility, intent));
        }

        if (!isKeeper && carrier.ActiveCarry is null && local.X < pitch.Length - CarryResolver.GoalLineMargin - 1)
        {
            var destination = carryResolver.ChooseDestination(state, carrier);
            var gain = TeamFrame.Convert(pitch, carrier.Team, destination).X - local.X;
            var space = SpaceAhead(pitch, carrier, local, opponents);
            var pressureCost = W.PressurePenalty * pressure
                * (1 - W.CarryTechniquePressureRelief * (carrier.Definition.Stats.Technique - 5));
            var utility = W.CarryBase + W.CarryProgressionWeight * gain + W.CarrySpaceWeight * space - pressureCost;
            candidates.Add(new ActionCandidate(ActionKind.Carry, utility, new CarryIntent(carrier.PlayerId, destination)));
        }

        return candidates;
    }

    /// <summary>0 at <see cref="OnBallWeights.GoalProximityRange"/> or further from the opponent goal, 1 on the goal line.</summary>
    public double GoalProximity(MatchState state, PlayerMatchState player) =>
        Math.Max(0, 1 - player.Position.DistanceTo(state.Pitch.OpponentGoalCentre(player.Team)) / W.GoalProximityRange);

    private double HoldUtility(MatchState state, PlayerMatchState carrier, double heldSeconds, double pressure)
    {
        if (carrier.ActiveCarry is { } carry)
        {
            var carrySeconds = (state.Clock.Tick - carry.StartTick) * state.Clock.TickSeconds;
            return W.ContinueCarryBase - W.ContinueCarryDecayPerSecond * carrySeconds - W.PressurePenalty * pressure;
        }

        return W.HoldBase - W.HoldDecayPerSecond * heldSeconds - W.PressurePenalty * pressure;
    }

    /// <summary>Distance to the nearest opponent ahead of the carrier (team frame), capped at <see cref="OnBallWeights.CarrySpaceCap"/>.</summary>
    private double SpaceAhead(PitchGeometry pitch, PlayerMatchState carrier, Vec2 local, IEnumerable<PlayerMatchState> opponents)
    {
        var space = W.CarrySpaceCap;
        foreach (var opponent in opponents)
        {
            var oLocal = TeamFrame.Convert(pitch, carrier.Team, opponent.Position);
            if (oLocal.X > local.X - 1)
                space = Math.Min(space, oLocal.DistanceTo(local));
        }

        return space;
    }
}
