namespace MonsterFootball.Simulation.Core;

/// <summary>
/// Pitch dimensions and landmarks in continuous coordinates.
/// Origin is a corner; X runs along the length (Home defends X = 0), Y across the width.
/// </summary>
public sealed record PitchGeometry
{
    public const double GoalWidth = 7.32;
    public const double PenaltyAreaDepth = 16.5;
    public const double PenaltyAreaWidth = 40.32;

    public static PitchGeometry Standard { get; } = new(105, 68);

    public PitchGeometry(double length, double width)
    {
        if (length <= PenaltyAreaDepth * 2)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Pitch is too short to fit both penalty areas.");
        if (width <= PenaltyAreaWidth)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Pitch is narrower than the penalty area.");
        Length = length;
        Width = width;
    }

    public double Length { get; }
    public double Width { get; }

    public double HalfwayX => Length / 2;
    public Vec2 Centre => new(Length / 2, Width / 2);

    public bool Contains(Vec2 point) =>
        point.X >= 0 && point.X <= Length && point.Y >= 0 && point.Y <= Width;

    public Vec2 Clamp(Vec2 point) =>
        new(Math.Clamp(point.X, 0, Length), Math.Clamp(point.Y, 0, Width));

    /// <summary>+1 if the side attacks toward increasing X, otherwise -1.</summary>
    public static int AttackDirection(TeamSide side) => side == TeamSide.Home ? 1 : -1;

    /// <summary>X coordinate of the goal line the given side defends.</summary>
    public double OwnGoalLineX(TeamSide side) => side == TeamSide.Home ? 0 : Length;

    public Vec2 OwnGoalCentre(TeamSide side) => new(OwnGoalLineX(side), Width / 2);

    public Vec2 OpponentGoalCentre(TeamSide side) => OwnGoalCentre(side.Opponent());

    public bool IsInOwnHalf(Vec2 point, TeamSide side) =>
        side == TeamSide.Home ? point.X <= HalfwayX : point.X >= HalfwayX;

    /// <summary>True if <paramref name="point"/> lies in the penalty area defended by <paramref name="defendingSide"/>.</summary>
    public bool IsInPenaltyArea(Vec2 point, TeamSide defendingSide)
    {
        if (!Contains(point))
            return false;
        var halfWidth = PenaltyAreaWidth / 2;
        if (Math.Abs(point.Y - Width / 2) > halfWidth)
            return false;
        var distanceFromGoalLine = Math.Abs(point.X - OwnGoalLineX(defendingSide));
        return distanceFromGoalLine <= PenaltyAreaDepth;
    }
}
