using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.AI;

/// <summary>Every behaviour the utility AI can choose, on or off the ball.</summary>
public enum ActionKind
{
    // On the ball.
    Hold,
    Pass,
    Carry,
    Shoot,

    // Off the ball.
    MoveIntoPosition,
    SupportBallCarrier,
    MakeForwardRun,
    CloseDownOpponent,
    RecoverPosition,
    DefendGoalSide,
}

/// <summary>
/// One option for a player, with its utility. On-ball candidates carry the <see cref="Intent"/> to execute;
/// off-ball candidates carry a movement <see cref="Target"/> and, when marking, the <see cref="Subject"/> being marked.
/// </summary>
public sealed record ActionCandidate(
    ActionKind Kind,
    double Utility,
    ActionIntent? Intent = null,
    Vec2? Target = null,
    PlayerId? Subject = null)
{
    public ActionCandidate WithUtility(double utility) => this with { Utility = utility };
}
