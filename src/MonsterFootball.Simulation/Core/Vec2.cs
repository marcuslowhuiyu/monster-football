using System.Globalization;

namespace MonsterFootball.Simulation.Core;

/// <summary>A point or direction in continuous pitch coordinates (metres).</summary>
public readonly record struct Vec2(double X, double Y)
{
    private const double Epsilon = 1e-9;

    public static Vec2 Zero { get; } = new(0, 0);

    public double Length => Math.Sqrt(LengthSquared);
    public double LengthSquared => X * X + Y * Y;

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 v, double s) => new(v.X * s, v.Y * s);
    public static Vec2 operator /(Vec2 v, double s) => new(v.X / s, v.Y / s);

    public double DistanceTo(Vec2 other) => (other - this).Length;

    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>
    /// Closest point to <paramref name="point"/> on segment <paramref name="start"/>–<paramref name="end"/>;
    /// <paramref name="t"/> is its fraction along the segment (0 at start, 1 at end).
    /// </summary>
    public static Vec2 ClosestPointOnSegment(Vec2 start, Vec2 end, Vec2 point, out double t)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared;
        t = lengthSquared < Epsilon ? 0 : Math.Clamp(Dot(point - start, segment) / lengthSquared, 0, 1);
        return start + segment * t;
    }

    public Vec2 Normalized()
    {
        var length = Length;
        return length < Epsilon ? Zero : this / length;
    }

    /// <summary>Moves toward <paramref name="target"/> by at most <paramref name="maxDistance"/>, without overshooting.</summary>
    public Vec2 MoveTowards(Vec2 target, double maxDistance)
    {
        var delta = target - this;
        var distance = delta.Length;
        if (distance <= maxDistance || distance < Epsilon)
            return target;
        return this + delta / distance * maxDistance;
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X:0.##}, {Y:0.##})");
}
