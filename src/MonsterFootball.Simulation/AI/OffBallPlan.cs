using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// An off-ball behaviour a player has committed to until <see cref="UntilTick"/>. Commitment stops
/// targets jittering tick to tick. The plan is dropped early if the attacking team changes.
/// When marking, <see cref="Subject"/> is the opponent being marked and the target follows them.
/// </summary>
public sealed record OffBallPlan(ActionKind Kind, Vec2 Target, PlayerId? Subject, TeamSide? AttackingTeam, long UntilTick);
