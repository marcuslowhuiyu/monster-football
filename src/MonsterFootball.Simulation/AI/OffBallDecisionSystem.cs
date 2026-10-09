using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Utility AI for players without the ball. Runs after <see cref="Positioning.TeamShapeSystem"/>, whose targets
/// are the default (<see cref="ActionKind.MoveIntoPosition"/>). Goalkeepers, the ball carrier, the presser and
/// whoever a pass in flight will reach are left as the shape system set them. Everyone else follows a committed
/// <see cref="OffBallPlan"/> or chooses a new one. Team limits: <see cref="MaxSupporters"/> supporters,
/// <see cref="MaxRunners"/> runners, and each opponent marked by at most one player.
/// </summary>
internal sealed class OffBallDecisionSystem(OffBallCandidateGenerator generator, UtilitySelector selector)
{
    public const int MaxSupporters = 2;
    public const int MaxRunners = 2;
    public const double MarkSprintDistance = 8;

    public static double CommitmentSeconds(ActionKind kind) => kind switch
    {
        ActionKind.MakeForwardRun => 3.0,
        ActionKind.SupportBallCarrier or ActionKind.DefendGoalSide => 1.5,
        _ => 1.0,
    };

    public void Apply(MatchContext context)
    {
        var state = context.State;
        foreach (var side in Enum.GetValues<TeamSide>())
            ApplyTeam(context, side);
    }

    private void ApplyTeam(MatchContext context, TeamSide side)
    {
        var state = context.State;
        var tick = state.Clock.Tick;
        var attacking = state.AttackingTeam == side;
        var players = state.TeamPlayers(side).ToList();

        var deciders = new List<PlayerMatchState>();
        foreach (var player in players)
        {
            if (IsControlledElsewhere(state, player))
            {
                player.OffBallPlan = null;
                continue;
            }

            if (player.OffBallPlan is { } plan && plan.UntilTick > tick && plan.AttackingTeam == state.AttackingTeam
                && (plan.Subject is not { } subject || !state.HasBall(subject)))
                continue;

            player.OffBallPlan = null;
            deciders.Add(player);
        }

        var supporters = players.Count(p => p.OffBallPlan?.Kind == ActionKind.SupportBallCarrier);
        var runners = players.Count(p => p.OffBallPlan?.Kind == ActionKind.MakeForwardRun);
        var marked = players.Where(p => p.OffBallPlan?.Subject is not null).Select(p => p.OffBallPlan!.Subject!.Value).ToHashSet();

        foreach (var player in deciders)
        {
            var candidates = generator.Generate(state, player, attacking, supporters < MaxSupporters, runners < MaxRunners, marked);
            var choice = selector.Choose(candidates, player, state, context.Random);
            var until = tick + (long)Math.Ceiling(CommitmentSeconds(choice.Kind) / context.Config.TickSeconds);
            player.OffBallPlan = new OffBallPlan(choice.Kind, choice.Target ?? player.MovementTarget, choice.Subject, state.AttackingTeam, until);

            if (choice.Kind == ActionKind.SupportBallCarrier) supporters++;
            if (choice.Kind == ActionKind.MakeForwardRun) runners++;
            if (choice.Subject is { } subject) marked.Add(subject);
        }

        foreach (var player in players)
            if (player.OffBallPlan is { } plan)
                Execute(state, player, plan);
    }

    /// <summary>
    /// A runner goes beyond the offside line only once a teammate's pass is in the air (offside was judged as it was
    /// played); until then they wait at the current line, slightly onside for an aware runner and drifting beyond it for an
    /// eager one (<see cref="OffBallWeights.RunHoldOffset"/>) — the main source of offsides.
    /// </summary>
    private Vec2 HoldTheLine(MatchState state, PlayerMatchState runner, Vec2 runTarget)
    {
        if (state.Ball.Flight is { Pass: not null } flight && flight.Team == runner.Team)
            return runTarget;

        var pitch = state.Pitch;
        var local = Positioning.TeamFrame.Convert(pitch, runner.Team, runTarget);
        var holdX = Laws.Offside.LineX(state, runner.Team, state.Ball.Position) + generator.Weights.RunHoldOffset(runner.Definition.Stats.Awareness);
        return local.X <= holdX ? runTarget : Positioning.TeamFrame.Convert(pitch, runner.Team, new Vec2(holdX, local.Y));
    }

    /// <summary>Keeper, carrier, presser and pass receiver: the shape system's sprint assignment marks the latter two.</summary>
    private static bool IsControlledElsewhere(MatchState state, PlayerMatchState player) =>
        player.Definition.Position.IsGoalkeeper()
        || state.HasBall(player.PlayerId)
        || player.MovementIntensity == MovementIntensity.Sprint;

    private void Execute(MatchState state, PlayerMatchState player, OffBallPlan plan)
    {
        switch (plan.Kind)
        {
            case ActionKind.MoveIntoPosition:
                break; // keep the shape target and jog
            case ActionKind.RecoverPosition:
                player.MovementIntensity = MovementIntensity.Sprint; // shape target, at speed
                break;
            case ActionKind.SupportBallCarrier:
                player.MovementTarget = plan.Target;
                player.MovementIntensity = MovementIntensity.Jog;
                break;
            case ActionKind.MakeForwardRun:
                player.MovementTarget = HoldTheLine(state, player, plan.Target);
                player.MovementIntensity = MovementIntensity.Sprint;
                break;
            case ActionKind.DefendGoalSide:
                var attacker = state.Player(plan.Subject!.Value);
                var point = generator.MarkPoint(state, player.Team, attacker);
                player.MovementTarget = point;
                player.MovementIntensity = player.Position.DistanceTo(point) > MarkSprintDistance
                    ? MovementIntensity.Sprint
                    : MovementIntensity.Jog;
                break;
        }
    }
}
