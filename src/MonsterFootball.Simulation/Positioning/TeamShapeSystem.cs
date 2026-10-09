using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Fitness;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// Assigns every player's <see cref="PlayerMatchState.MovementTarget"/> and intensity each tick
/// from the 4-3-3 anchors, shifted with the ball. The attacking team (in possession, or whose pass is
/// in flight) uses the attacking shape, never pushing beyond the opponents' last outfield line; the other team (and both teams when the ball is loose) defends,
/// with its nearest outfield player sprinting to close the ball (to a jockeying spot goal-side of a carrier). The ball carrier follows their carry,
/// if any, and otherwise holds position. Whoever a pass in flight will reach sprints to meet it.
/// </summary>
internal sealed class TeamShapeSystem
{
    // How strongly the block follows the ball (fraction of ball offset from the centre spot).
    private const double AttackShiftX = 0.5;
    private const double AttackShiftY = 0.25;
    private const double DefendShiftX = 0.55;
    private const double DefendShiftY = 0.35;

    private const double CentreBackCoverBehindBall = 6;
    private const double DefensiveLineGoalSideOfBall = 4;
    private const double MinDefensiveLineX = 6;
    private const double MidfieldGapAheadOfLine = 8;
    private const double MidfieldCentralCompression = 0.8;
    private const double ForwardMaxDepthFromGoalLine = 6;
    private const double TouchlineMargin = 2;
    private const double GoalLineMargin = 3;

    /// <summary>
    /// Moves each team's <see cref="MatchState.ShapeFocus"/> toward the ball — by <paramref name="attackingSmoothing"/>
    /// per tick for the attacking team and <paramref name="defendingSmoothing"/> for the other (1 = snap to the ball,
    /// as tests use) — then assigns targets around it.
    /// </summary>
    public void Apply(MatchState state, double attackingSmoothing = 1.0, double defendingSmoothing = 1.0)
    {
        foreach (var side in Enum.GetValues<TeamSide>())
        {
            var smoothing = Math.Clamp(state.AttackingTeam == side ? attackingSmoothing : defendingSmoothing, 0, 1);
            var focus = state.ShapeFocus(side);
            state.SetShapeFocus(side, focus + (state.Ball.Position - focus) * smoothing);
        }

        foreach (var side in Enum.GetValues<TeamSide>())
            AssignTeam(state, side);

        if (state.Ball.Flight is { Pass: { FinalReceiver: { } receiverId } pass })
        {
            var receiver = state.Player(receiverId);
            receiver.MovementTarget = pass.TargetPosition;
            receiver.MovementIntensity = MovementIntensity.Sprint;
        }
    }

    /// <summary>
    /// Shape targets while play is stopped for a restart at <paramref name="ball"/>: both shapes are built around the
    /// restart spot, <paramref name="awardedTeam"/> in its attacking shape and the other in its defending shape.
    /// Nobody presses a dead ball.
    /// </summary>
    public void ApplyForRestart(MatchState state, TeamSide awardedTeam, Vec2 ball)
    {
        foreach (var side in Enum.GetValues<TeamSide>())
        {
            state.SetShapeFocus(side, ball);
            AssignTeam(state, side, side == awardedTeam, press: false);
        }
    }

    private static void AssignTeam(MatchState state, TeamSide side) =>
        AssignTeam(state, side, state.AttackingTeam == side, press: true);

    private static void AssignTeam(MatchState state, TeamSide side, bool inPossession, bool press)
    {
        var pitch = state.Pitch;
        var ballLocal = TeamFrame.Convert(pitch, side, state.ShapeFocus(side));
        var defensiveLineX = DefensiveLineX(pitch, ballLocal);
        var opponentLastLineX = OpponentLastLineX(state, side);

        foreach (var player in state.TeamPlayers(side))
        {
            if (state.HasBall(player.PlayerId))
            {
                if (player.ActiveCarry is { } carry)
                {
                    player.MovementTarget = carry.TargetPosition;
                    player.MovementIntensity = MovementIntensity.Carry;
                }
                else
                {
                    player.MovementTarget = player.Position;
                    player.MovementIntensity = MovementIntensity.Jog;
                }

                continue;
            }

            var slot = player.Definition.Position;
            var local = slot.IsGoalkeeper()
                ? GoalkeeperPositioning.Target(pitch, ballLocal, player.Definition.Stats.Awareness, inPossession)
                : inPossession
                    ? AttackingTarget(pitch, slot, ballLocal, opponentLastLineX)
                    : DefendingTarget(pitch, slot, ballLocal, defensiveLineX);

            player.MovementTarget = TeamFrame.Convert(pitch, side, local);
            player.MovementIntensity = inPossession ? MovementIntensity.Easy : MovementIntensity.Jog;
        }

        if (!inPossession && press)
            AssignPresser(state, side);
    }

    private static Vec2 AttackingTarget(PitchGeometry pitch, PlayingPosition slot, Vec2 ballLocal, double opponentLastLineX)
    {
        var anchor = Formation433.InPossession(pitch, slot);
        var x = anchor.X + AttackShiftX * (ballLocal.X - pitch.HalfwayX);
        var y = anchor.Y + AttackShiftY * (ballLocal.Y - pitch.Width / 2);

        if (slot is PlayingPosition.LCB or PlayingPosition.RCB)
            x = Math.Min(x, ballLocal.X - CentreBackCoverBehindBall);
        if (slot.Group() == PositionGroup.Forward)
            x = Math.Min(x, pitch.Length - ForwardMaxDepthFromGoalLine);

        // Positional discipline (not an offside rule): stay level with the opponents' last outfield line.
        x = Math.Min(x, Math.Max(opponentLastLineX, ballLocal.X));

        return ClampOutfield(pitch, new Vec2(x, y));
    }

    private static Vec2 DefendingTarget(PitchGeometry pitch, PlayingPosition slot, Vec2 ballLocal, double defensiveLineX)
    {
        var anchor = Formation433.OutOfPossession(pitch, slot);
        var dx = DefendShiftX * (ballLocal.X - pitch.HalfwayX);
        var dy = DefendShiftY * (ballLocal.Y - pitch.Width / 2);
        var centreY = pitch.Width / 2;

        var target = slot.Group() switch
        {
            PositionGroup.Defender => new Vec2(defensiveLineX, anchor.Y + dy),
            PositionGroup.Midfielder => new Vec2(
                Math.Max(anchor.X + dx, defensiveLineX + MidfieldGapAheadOfLine),
                centreY + (anchor.Y - centreY) * MidfieldCentralCompression + dy),
            _ => new Vec2(
                Math.Max(anchor.X + dx, defensiveLineX + MidfieldGapAheadOfLine * 2),
                anchor.Y + dy),
        };

        return ClampOutfield(pitch, target);
    }

    /// <summary>The opponents' deepest outfield player, in <paramref name="side"/>'s team frame (their largest X).</summary>
    internal static double OpponentLastLineX(MatchState state, TeamSide side) =>
        state.TeamPlayers(side.Opponent())
            .Where(p => !p.Definition.Position.IsGoalkeeper())
            .Max(p => TeamFrame.Convert(state.Pitch, side, p.Position).X);

    /// <summary>One shared X for the back four: follows the ball but stays goal-side of it.</summary>
    private static double DefensiveLineX(PitchGeometry pitch, Vec2 ballLocal)
    {
        var anchorLine = Formation433.OutOfPossession(pitch, PlayingPosition.LCB).X;
        var followed = anchorLine + DefendShiftX * (ballLocal.X - pitch.HalfwayX);
        var goalSideLimit = Math.Max(MinDefensiveLineX, ballLocal.X - DefensiveLineGoalSideOfBall);
        return Math.Clamp(followed, MinDefensiveLineX, goalSideLimit);
    }

    /// <summary>How far goal-side of a ball carrier the presser stands (inside the 1.8 m challenge range).</summary>
    public const double JockeyDistance = 1.5;

    /// <summary>Tired players are treated as further from the ball when choosing who presses (fresh → ×1, empty → ×2).</summary>
    public const double TiredPressDistancePenalty = 1.0;

    /// <summary>
    /// The outfield player with the lowest fatigue-weighted distance closes the ball at full intensity;
    /// ties go to the earlier player.
    /// </summary>
    private static void AssignPresser(MatchState state, TeamSide side)
    {
        var ball = state.Ball.Position;
        var presser = state.TeamPlayers(side)
            .Where(p => !p.Definition.Position.IsGoalkeeper())
            .MinBy(p => p.Position.DistanceTo(ball) * (1 + TiredPressDistancePenalty * FatigueModel.Tiredness(p.CurrentStamina)));

        if (presser is null)
            return;

        // Against a carrier, close down to a jockeying spot goal-side of them (inside tackle range);
        // a loose ball or a ball in flight is chased directly.
        var target = ball;
        if (state.PossessionPlayerId is { } carrierId)
        {
            var carrier = state.Player(carrierId);
            var ownGoal = state.Pitch.OwnGoalCentre(side);
            target = state.Pitch.Clamp(carrier.Position + (ownGoal - carrier.Position).Normalized() * JockeyDistance);
        }

        presser.MovementTarget = target;
        presser.MovementIntensity = MovementIntensity.Sprint;
    }

    private static Vec2 ClampOutfield(PitchGeometry pitch, Vec2 point) =>
        new(
            Math.Clamp(point.X, GoalLineMargin, pitch.Length - GoalLineMargin),
            Math.Clamp(point.Y, TouchlineMargin, pitch.Width - TouchlineMargin));
}
