using System.Windows;
using System.Windows.Media;
using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Rendering;

/// <summary>Geometry helpers that give shapes a slightly wobbly, hand-inked line.</summary>
internal static class InkGeometry
{
    /// <summary>A rectangle traced by hand: each edge is broken into short jittered segments.</summary>
    public static Geometry SketchRect(RoomRect r, Random rng, double jitter)
    {
        var corners = new[]
        {
            new Point(r.X, r.Y),
            new Point(r.Right, r.Y),
            new Point(r.Right, r.Bottom),
            new Point(r.X, r.Bottom),
        };

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(Jitter(corners[0], rng, jitter), isFilled: true, isClosed: true);
            for (int i = 0; i < corners.Length; i++)
            {
                Point a = corners[i];
                Point b = corners[(i + 1) % corners.Length];
                Vector edge = b - a;
                int steps = Math.Max(2, (int)(edge.Length / 22));
                for (int k = 1; k <= steps; k++)
                {
                    Point p = a + edge * (k / (double)steps);
                    ctx.LineTo(Jitter(p, rng, jitter), isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>A hand-drawn straight line.</summary>
    public static Geometry SketchLine(Point a, Point b, Random rng, double jitter)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(Jitter(a, rng, jitter), isFilled: false, isClosed: false);
            Vector edge = b - a;
            int steps = Math.Max(2, (int)(edge.Length / 18));
            for (int k = 1; k <= steps; k++)
            {
                ctx.LineTo(Jitter(a + edge * (k / (double)steps), rng, jitter), isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Diagonal hatching lines covering a rectangle (clip it to the shape you want).</summary>
    public static Geometry Hatch(Rect bounds, double spacing, Random rng)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (double d = -bounds.Height; d < bounds.Width; d += spacing)
            {
                double wobble = (rng.NextDouble() - 0.5) * 1.5;
                ctx.BeginFigure(new Point(bounds.X + d + wobble, bounds.Bottom), isFilled: false, isClosed: false);
                ctx.LineTo(new Point(bounds.X + d + bounds.Height - wobble, bounds.Y), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point Jitter(Point p, Random rng, double amount) =>
        new(p.X + (rng.NextDouble() - 0.5) * 2 * amount, p.Y + (rng.NextDouble() - 0.5) * 2 * amount);
}
