using System;
using Godot;
using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Presentation.MatchViewer;

/// <summary>Maps pitch metres to screen pixels (Home attacks to the right, +Y up) and draws the pitch markings.</summary>
public readonly record struct PitchTransform(PitchGeometry Pitch, Vector2 Origin, float Scale)
{
    public static PitchTransform Fit(PitchGeometry pitch, Rect2 area)
    {
        var scale = Mathf.Min(area.Size.X / (float)pitch.Length, area.Size.Y / (float)pitch.Width);
        var size = new Vector2((float)pitch.Length, (float)pitch.Width) * scale;
        return new PitchTransform(pitch, area.Position + (area.Size - size) / 2, scale);
    }

    public Vector2 ToScreen(Vec2 p) => Origin + new Vector2((float)p.X, (float)(Pitch.Width - p.Y)) * Scale;

    public float Metres(double metres) => (float)metres * Scale;
}

public static class PitchDrawing
{
    private static readonly Color Grass = new(0.18f, 0.45f, 0.22f);
    private static readonly Color Line = new(1, 1, 1, 0.85f);

    public static void Draw(CanvasItem canvas, PitchTransform t)
    {
        var pitch = t.Pitch;
        var length = pitch.Length;
        var width = pitch.Width;
        var midY = width / 2;

        canvas.DrawRect(Rect(t, 0, 0, length, width), Grass);
        canvas.DrawRect(Rect(t, 0, 0, length, width), Line, filled: false, width: 2);
        canvas.DrawLine(t.ToScreen(new Vec2(length / 2, 0)), t.ToScreen(new Vec2(length / 2, width)), Line, 2);
        canvas.DrawArc(t.ToScreen(pitch.Centre), t.Metres(9.15), 0, Mathf.Tau, 64, Line, 2);
        canvas.DrawCircle(t.ToScreen(pitch.Centre), t.Metres(0.3), Line);

        foreach (var goalLineX in new[] { 0.0, length })
        {
            var inward = goalLineX == 0 ? 1 : -1;
            Box(canvas, t, goalLineX, midY, inward * PitchGeometry.PenaltyAreaDepth, PitchGeometry.PenaltyAreaWidth);
            Box(canvas, t, goalLineX, midY, inward * 5.5, PitchGeometry.GoalWidth + 11);
            Box(canvas, t, goalLineX, midY, -inward * 1.5, PitchGeometry.GoalWidth); // the goal, behind the line
            canvas.DrawCircle(t.ToScreen(new Vec2(goalLineX + inward * 11, midY)), t.Metres(0.3), Line);
        }
    }

    private static void Box(CanvasItem canvas, PitchTransform t, double goalLineX, double midY, double depth, double boxWidth)
    {
        var x0 = Math.Min(goalLineX, goalLineX + depth);
        canvas.DrawRect(Rect(t, x0, midY - boxWidth / 2, Math.Abs(depth), boxWidth), Line, filled: false, width: 2);
    }

    private static Rect2 Rect(PitchTransform t, double x, double y, double w, double h)
    {
        var topLeft = t.ToScreen(new Vec2(x, y + h));
        return new Rect2(topLeft, t.Metres(w), t.Metres(h));
    }
}
