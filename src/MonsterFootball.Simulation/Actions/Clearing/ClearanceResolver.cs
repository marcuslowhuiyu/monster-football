using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Actions.Clearing;

/// <summary>
/// Resolves a <see cref="ClearanceIntent"/> (D6, ADR-035). Wide — pressed (pressure ≥ 0.5) within 25 m of the
/// clearer's own goal — aims at the nearest touchline 25 m further upfield; Long aims towards the clearer's striker
/// (straight upfield if there is none ahead). Distance 35 m + 2 m per Power point above 5 caps both. Two draws scatter
/// the landing point sideways (± 4 m + 0.8 m per Technique point below 10) and in length (± 5 m). Reads state only.
/// </summary>
internal sealed class ClearanceResolver
{
    /// <summary>Same pressure measure as the on-ball AI: 1 − nearest opponent / radius, 0 beyond it.</summary>
    public const double PressureRadius = 3;
    public const double WidePressure = 0.5;
    public const double WideRange = 25;
    public const double WideAdvance = 25;
    public const double BaseDistance = 35;
    public const double DistancePerPower = 2;
    public const double BaseLateralScatter = 4;
    public const double LateralScatterPerTechnique = 0.8;
    public const double LengthScatter = 5;
    public const double BallSpeed = 18;

    /// <summary>How far <paramref name="kicker"/> can clear, in metres.</summary>
    public static double Distance(PlayerMatchState kicker) =>
        BaseDistance + DistancePerPower * (kicker.Definition.Stats.Power - 5);

    /// <summary>The kind of clearance and where it is aimed (pitch coordinates). Deterministic.</summary>
    public (ClearanceKind Kind, Vec2 Target) Aim(MatchState state, PlayerMatchState kicker)
    {
        var pitch = state.Pitch;
        var local = TeamFrame.Convert(pitch, kicker.Team, kicker.Position);
        var distance = Distance(kicker);
        var nearest = state.TeamPlayers(kicker.Team.Opponent()).Min(o => o.Position.DistanceTo(kicker.Position));
        var pressure = nearest < PressureRadius ? 1 - nearest / PressureRadius : 0;

        if (pressure >= WidePressure && kicker.Position.DistanceTo(pitch.OwnGoalCentre(kicker.Team)) <= WideRange)
        {
            var aim = new Vec2(local.X + WideAdvance, local.Y >= pitch.Width / 2 ? pitch.Width : 0);
            var reach = Math.Min(distance, local.DistanceTo(aim));
            return (ClearanceKind.Wide, TeamFrame.Convert(pitch, kicker.Team, local + (aim - local).Normalized() * reach));
        }

        var direction = new Vec2(1, 0);
        var striker = state.TeamPlayers(kicker.Team).FirstOrDefault(p => p.Role == PlayingPosition.ST && p != kicker);
        if (striker is not null)
        {
            var strikerLocal = TeamFrame.Convert(pitch, kicker.Team, striker.Position);
            if (strikerLocal.X > local.X)
                direction = (strikerLocal - local).Normalized();
        }

        return (ClearanceKind.Long, TeamFrame.Convert(pitch, kicker.Team, local + direction * distance));
    }

    public ClearanceResult Resolve(MatchState state, ClearanceIntent intent, IMatchRandom random)
    {
        var kicker = state.Player(intent.Kicker);
        if (!state.HasBall(intent.Kicker))
            throw new ArgumentException($"{kicker} cannot clear without the ball.", nameof(intent));

        var (kind, target) = Aim(state, kicker);
        var start = kicker.Position;
        var along = (target - start).Normalized();
        var across = new Vec2(-along.Y, along.X);
        var lateralLimit = BaseLateralScatter + LateralScatterPerTechnique * (10 - kicker.Definition.Stats.Technique);
        var lateral = (random.NextDouble() * 2 - 1) * lateralLimit;
        var length = (random.NextDouble() * 2 - 1) * LengthScatter;
        var landing = target + across * lateral + along * length;

        return new ClearanceResult(kicker.Team, kicker.PlayerId, kind, start, target, landing, start.DistanceTo(landing) / BallSpeed);
    }
}
