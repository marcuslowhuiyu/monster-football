using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Actions.Carrying;

/// <summary>
/// Plans carries. <see cref="ChooseDestination"/> picks where to go (forward, steering away from the
/// nearest opponent by Awareness); <see cref="Resolve"/> turns a <see cref="CarryIntent"/> into a
/// <see cref="CarryResult"/>. Carrying never loses the ball by itself; defenders must tackle.
/// No randomness.
/// </summary>
internal sealed class CarryResolver
{
    public const double SegmentLength = 10;
    public const double TouchlineMargin = 3;
    public const double GoalLineMargin = 3;
    public const double AvoidanceRadius = 12;

    /// <summary>Default destination for a carry: up to one segment forward (toward goal in the final third), steered away from the nearest opponent.</summary>
    public Vec2 ChooseDestination(MatchState state, Players.PlayerMatchState carrier)
    {
        var pitch = state.Pitch;
        var local = TeamFrame.Convert(pitch, carrier.Team, carrier.Position);
        // Head straight up the pitch, bending toward the goal in the final third.
        var goalLocal = new Vec2(pitch.Length, pitch.Width / 2);
        var finalThirdBlend = Math.Clamp((local.X - state.Zones.FinalThirdX) / (pitch.Length - state.Zones.FinalThirdX), 0, 1);
        var forward = (new Vec2(1, 0) * (1 - finalThirdBlend) + (goalLocal - local).Normalized() * finalThirdBlend).Normalized();

        var nearest = state.TeamPlayers(carrier.Team.Opponent()).MinBy(o => o.Position.DistanceTo(carrier.Position));
        var steer = Vec2.Zero;
        if (nearest is not null)
        {
            var away = local - TeamFrame.Convert(pitch, carrier.Team, nearest.Position);
            var distance = away.Length;
            if (distance < AvoidanceRadius)
            {
                var awareness = carrier.Definition.Stats.Awareness / 10.0;
                steer = new Vec2(0, away.Normalized().Y) * (awareness * (1 - distance / AvoidanceRadius) * 1.5);
            }
        }

        // Drift toward the middle when near a touchline so the carry stays on the pitch.
        var centreBias = (pitch.Width / 2 - local.Y) / pitch.Width * 0.5;
        var direction = (forward + steer + new Vec2(0, centreBias)).Normalized();
        var destinationLocal = local + direction * SegmentLength;
        return TeamFrame.Convert(pitch, carrier.Team, ClampLocal(pitch, destinationLocal));
    }

    public CarryResult Resolve(MatchState state, CarryIntent intent)
    {
        var carrier = state.Player(intent.Carrier);
        if (!state.HasBall(intent.Carrier))
            throw new ArgumentException($"{carrier} cannot carry without the ball.", nameof(intent));

        var start = carrier.Position;
        var offset = intent.Destination - start;
        var target = offset.Length > SegmentLength ? start + offset.Normalized() * SegmentLength : intent.Destination;
        target = state.Pitch.Clamp(target);

        var speed = MovementSystem.CarrySpeed(carrier);
        var duration = start.DistanceTo(target) / speed;
        var ticks = Math.Max(1, (long)Math.Ceiling(duration / state.Clock.TickSeconds - 1e-9));
        var quality = MovementSystem.CarrySpeedFraction(carrier.Definition.Stats.Technique);

        return new CarryResult(intent.Carrier, start, target, speed, duration, state.Clock.Tick, state.Clock.Tick + ticks, quality);
    }

    private static Vec2 ClampLocal(PitchGeometry pitch, Vec2 local) =>
        new(Math.Clamp(local.X, GoalLineMargin, pitch.Length - GoalLineMargin),
            Math.Clamp(local.Y, TouchlineMargin, pitch.Width - TouchlineMargin));
}
