using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// Assigns every player's <see cref="PlayerMatchState.MovementTarget"/> and intensity each tick
/// from the 4-3-3 anchors, shifted with the ball. The attacking team (in possession, or whose pass is
/// in flight) uses the attacking shape; the other team (and both teams when the ball is loose) defends,
/// with its nearest outfield player sprinting to close the ball. The ball carrier is left alone
/// (carrying arrives in M1.6). Whoever a pass in flight will reach sprints to meet it.
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

    public void Apply(MatchState state)
    {
        foreach (var side in Enum.GetValues<TeamSide>())
            AssignTeam(state, side);

        if (state.Ball.Flight is { Pass.FinalReceiver: { } receiverId } flight)
        {
            var receiver = state.Player(receiverId);
            receiver.MovementTarget = flight.Pass.TargetPosition;
            receiver.MovementIntensity = MovementIntensity.Sprint;
        }
    }

    private static void AssignTeam(MatchState state, TeamSide side)
    {
        var pitch = state.Pitch;
        var inPossession = state.AttackingTeam == side;
        var ballLocal = TeamFrame.Convert(pitch, side, state.Ball.Position);
        var defensiveLineX = DefensiveLineX(pitch, ballLocal);

        foreach (var player in state.TeamPlayers(side))
        {
            if (state.HasBall(player.PlayerId))
            {
                player.MovementTarget = player.Position;
                player.MovementIntensity = MovementIntensity.Jog;
                continue;
            }

            var slot = player.Definition.Position;
            var local = slot.IsGoalkeeper()
                ? GoalkeeperPositioning.Target(pitch, ballLocal, player.Definition.Stats.Awareness, inPossession)
                : inPossession
                    ? AttackingTarget(pitch, slot, ballLocal)
                    : DefendingTarget(pitch, slot, ballLocal, defensiveLineX);

            player.MovementTarget = TeamFrame.Convert(pitch, side, local);
            player.MovementIntensity = MovementIntensity.Jog;
        }

        if (!inPossession)
            AssignPresser(state, side);
    }

    private static Vec2 AttackingTarget(PitchGeometry pitch, PlayingPosition slot, Vec2 ballLocal)
    {
        var anchor = Formation433.InPossession(pitch, slot);
        var x = anchor.X + AttackShiftX * (ballLocal.X - pitch.HalfwayX);
        var y = anchor.Y + AttackShiftY * (ballLocal.Y - pitch.Width / 2);

        if (slot is PlayingPosition.LCB or PlayingPosition.RCB)
            x = Math.Min(x, ballLocal.X - CentreBackCoverBehindBall);
        if (slot.Group() == PositionGroup.Forward)
            x = Math.Min(x, pitch.Length - ForwardMaxDepthFromGoalLine);

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

    /// <summary>One shared X for the back four: follows the ball but stays goal-side of it.</summary>
    private static double DefensiveLineX(PitchGeometry pitch, Vec2 ballLocal)
    {
        var anchorLine = Formation433.OutOfPossession(pitch, PlayingPosition.LCB).X;
        var followed = anchorLine + DefendShiftX * (ballLocal.X - pitch.HalfwayX);
        var goalSideLimit = Math.Max(MinDefensiveLineX, ballLocal.X - DefensiveLineGoalSideOfBall);
        return Math.Clamp(followed, MinDefensiveLineX, goalSideLimit);
    }

    /// <summary>The nearest outfield player closes the ball at full intensity; ties go to the earlier player.</summary>
    private static void AssignPresser(MatchState state, TeamSide side)
    {
        var ball = state.Ball.Position;
        var presser = state.TeamPlayers(side)
            .Where(p => !p.Definition.Position.IsGoalkeeper())
            .MinBy(p => p.Position.DistanceTo(ball));

        if (presser is null)
            return;

        presser.MovementTarget = ball;
        presser.MovementIntensity = MovementIntensity.Sprint;
    }

    private static Vec2 ClampOutfield(PitchGeometry pitch, Vec2 point) =>
        new(
            Math.Clamp(point.X, GoalLineMargin, pitch.Length - GoalLineMargin),
            Math.Clamp(point.Y, TouchlineMargin, pitch.Width - TouchlineMargin));
}
