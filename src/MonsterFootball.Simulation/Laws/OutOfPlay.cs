using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Laws;

/// <summary>Which kind of line the ball crossed.</summary>
internal enum BoundaryLine
{
    Touchline,
    GoalLine,
}

/// <summary>
/// Ball out of play (Law 9, MILESTONE_2 §9, ADR-028). The ball is out when its path wholly crosses a touchline or
/// goal line; the crossing point comes from segment–boundary intersection and the restart from where it crossed and
/// who last touched it: over a touchline, a throw-in to the other team; over a goal line, a goal kick if the
/// attackers touched it last, otherwise a corner. Goals are decided by the shot model, never here.
/// </summary>
internal static class OutOfPlay
{
    /// <summary>
    /// Where the path from <paramref name="from"/> (on the pitch) to <paramref name="to"/> first leaves the pitch, or
    /// null if <paramref name="to"/> is still on it. A path through a corner counts as crossing the goal line.
    /// </summary>
    public static (Vec2 Point, BoundaryLine Line)? Crossing(PitchGeometry pitch, Vec2 from, Vec2 to)
    {
        if (pitch.Contains(to))
            return null;

        var delta = to - from;
        var tGoal = Exit(from.X, delta.X, pitch.Length);
        var tTouch = Exit(from.Y, delta.Y, pitch.Width);
        var goalLine = tGoal <= tTouch;
        var t = Math.Clamp(goalLine ? tGoal : tTouch, 0, 1);
        var point = pitch.Clamp(from + delta * t);
        return (point, goalLine ? BoundaryLine.GoalLine : BoundaryLine.Touchline);
    }

    /// <summary>The restart for a ball that crossed <paramref name="line"/> at <paramref name="point"/>.</summary>
    public static (RestartType Restart, TeamSide AwardedTeam, Vec2 Location) Decide(PitchGeometry pitch, Vec2 point, BoundaryLine line, BallTouch lastTouch)
    {
        if (line == BoundaryLine.Touchline)
            return (RestartType.ThrowIn, lastTouch.Team.Opponent(), new Vec2(Math.Clamp(point.X, 0, pitch.Length), point.Y < pitch.Width / 2 ? 0 : pitch.Width));

        var defending = point.X < pitch.HalfwayX ? TeamSide.Home : TeamSide.Away;
        var goalLineX = pitch.OwnGoalLineX(defending);
        var side = point.Y < pitch.Width / 2 ? -1 : 1;
        if (lastTouch.Team != defending)
        {
            var inward = PitchGeometry.AttackDirection(defending);
            return (RestartType.GoalKick, defending,
                new Vec2(goalLineX + inward * PitchGeometry.GoalAreaDepth, pitch.Width / 2 + side * PitchGeometry.GoalAreaWidth / 2));
        }

        return (RestartType.Corner, defending.Opponent(), new Vec2(goalLineX, side < 0 ? 0 : pitch.Width));
    }

    /// <summary>Puts the ball on the line where it crossed and stops play for the restart the laws give.</summary>
    public static void Stop(MatchContext context, Vec2 point, BoundaryLine line)
    {
        var state = context.State;
        var lastTouch = state.LastTouch ?? throw new InvalidOperationException("The ball left play before anyone touched it.");
        state.SetBallLoose(point);
        var (restart, awarded, location) = Decide(state.Pitch, point, line, lastTouch);
        context.StopPlay(StoppageReason.BallOutOfPlay, restart, awarded, location);
    }

    /// <summary>
    /// Moves a loose ball from where it is to <paramref name="to"/>, stopping play if that path leaves the pitch.
    /// Returns true if the ball went out.
    /// </summary>
    public static bool MoveLoose(MatchContext context, Vec2 to, Actions.LooseBallMotion? roll = null)
    {
        var state = context.State;
        if (Crossing(state.Pitch, state.Ball.Position, to) is { } crossing)
        {
            Stop(context, crossing.Point, crossing.Line);
            return true;
        }

        if (roll is null)
            state.SetBallLoose(to);
        else
            state.SetBallRolling(to, roll);
        return false;
    }

    /// <summary>Fraction of the path at which a coordinate leaves [0, limit]; +∞ if it never does.</summary>
    private static double Exit(double start, double delta, double limit)
    {
        if (delta > 0 && start + delta > limit)
            return (limit - start) / delta;
        if (delta < 0 && start + delta < 0)
            return -start / delta;
        return double.PositiveInfinity;
    }
}
