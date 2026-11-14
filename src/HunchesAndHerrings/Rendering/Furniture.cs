using System.Windows;
using System.Windows.Media;
using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Rendering;

/// <summary>
/// Blueprint-style furniture symbols for each room, laid out in coordinates relative to the room
/// (0..1 across the usable interior, below the room label).
/// </summary>
internal static class Furniture
{
    public static Geometry For(string roomId, Rect room)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        var b = new Sketch(group, new Rect(room.X + 8, room.Y + 22, Math.Max(10, room.Width - 16), Math.Max(10, room.Height - 30)));

        switch (roomId)
        {
            case RoomIds.WineCellar:
                for (int i = 0; i < 4; i++)
                {
                    b.Box(0.02, 0.05 + i * 0.22, 0.62, 0.1);
                    for (int k = 1; k < 6; k++)
                    {
                        b.Line(0.02 + k * 0.1, 0.05 + i * 0.22, 0.02 + k * 0.1, 0.15 + i * 0.22);
                    }
                }

                b.Circle(0.84, 0.25, 0.12);
                b.Circle(0.84, 0.7, 0.12);
                break;

            case RoomIds.Boiler:
                b.Circle(0.38, 0.55, 0.26);
                b.Circle(0.38, 0.55, 0.16);
                b.Line(0.38, 0.29, 0.38, 0.0);
                b.Box(0.72, 0.15, 0.24, 0.6);
                b.Line(0.72, 0.15, 0.96, 0.75);
                break;

            case RoomIds.ServantsHall:
                b.TableWithChairs(0.12, 0.35, 0.62, 0.22, 5);
                b.Box(0.86, 0.02, 0.12, 0.12);
                for (int i = 0; i < 3; i++)
                {
                    b.Circle(0.885 + i * 0.03, 0.08, 0.008);
                }

                b.Box(0.02, 0.82, 0.3, 0.12);
                break;

            case RoomIds.Kitchen:
                b.Box(0.02, 0.02, 0.4, 0.16);
                b.Circle(0.1, 0.1, 0.04);
                b.Circle(0.22, 0.1, 0.04);
                b.Circle(0.34, 0.1, 0.04);
                b.Box(0.3, 0.45, 0.42, 0.25);
                b.Box(0.88, 0.02, 0.1, 0.9);
                b.Line(0.88, 0.32, 0.98, 0.32);
                b.Line(0.88, 0.62, 0.98, 0.62);
                break;

            case RoomIds.Pantry:
                b.Box(0.02, 0.02, 0.16, 0.9);
                b.Line(0.02, 0.3, 0.18, 0.3);
                b.Line(0.02, 0.6, 0.18, 0.6);
                b.Box(0.42, 0.42, 0.34, 0.18);
                b.Circle(0.59, 0.75, 0.06);
                b.Box(0.8, 0.02, 0.18, 0.2);
                break;

            case RoomIds.Scullery:
                b.Box(0.05, 0.02, 0.5, 0.16);
                b.Box(0.08, 0.05, 0.2, 0.1);
                b.Box(0.32, 0.05, 0.2, 0.1);
                b.Circle(0.75, 0.6, 0.1);
                b.Line(0.75, 0.5, 0.75, 0.7);
                b.Circle(0.25, 0.68, 0.14);
                break;

            case RoomIds.Conservatory:
                b.Plant(0.2, 0.15, 0.12);
                b.Plant(0.8, 0.2, 0.14);
                b.Plant(0.25, 0.75, 0.15);
                b.Plant(0.78, 0.78, 0.12);
                b.Box(0.35, 0.45, 0.3, 0.08);
                b.Circle(0.5, 0.3, 0.05);
                break;

            case RoomIds.Library:
                b.Box(0.0, 0.0, 1.0, 0.08);
                b.Box(0.0, 0.0, 0.06, 1.0);
                b.Box(0.94, 0.0, 0.06, 1.0);
                b.Box(0.28, 0.35, 0.44, 0.45, dashed: true);
                b.Armchair(0.36, 0.55, 0.1);
                b.Armchair(0.64, 0.55, 0.1);
                b.Circle(0.5, 0.42, 0.05);
                break;

            case RoomIds.Study:
                b.Box(0.25, 0.3, 0.45, 0.22);
                b.Circle(0.47, 0.68, 0.07);
                b.Box(0.82, 0.0, 0.16, 0.42);
                b.Box(0.0, 0.3, 0.07, 0.4);
                b.Box(0.02, 0.38, 0.03, 0.24);
                break;

            case RoomIds.Billiard:
                b.Box(0.16, 0.18, 0.62, 0.62);
                b.Box(0.2, 0.24, 0.54, 0.5);
                foreach ((double x, double y) in new[] { (0.18, 0.21), (0.47, 0.19), (0.76, 0.21), (0.18, 0.77), (0.47, 0.79), (0.76, 0.77) })
                {
                    b.Circle(x, y, 0.03);
                }

                b.Box(0.88, 0.1, 0.08, 0.7);
                break;

            case RoomIds.Hall:
                b.Stairs(0.38, 0.0, 0.24, 1.0, 9, horizontalTreads: true);
                b.Box(0.02, 0.05, 0.05, 0.3);
                b.Circle(0.045, 0.14, 0.02);
                b.Box(0.1, 0.55, 0.22, 0.35, dashed: true);
                b.Box(0.7, 0.55, 0.22, 0.35, dashed: true);
                b.Circle(0.18, 0.2, 0.06);
                b.Circle(0.82, 0.2, 0.06);
                break;

            case RoomIds.BackPassage:
                b.Stairs(0.55, 0.0, 0.4, 1.0, 7, horizontalTreads: true);
                break;

            case RoomIds.Gardens:
                b.Line(0.5, 0.0, 0.5, 1.0, dashed: true);
                b.Line(0.0, 0.55, 1.0, 0.55, dashed: true);
                b.Circle(0.5, 0.55, 0.14);
                b.Circle(0.5, 0.55, 0.07);
                b.Plant(0.18, 0.18, 0.12);
                b.Plant(0.82, 0.18, 0.12);
                b.Plant(0.18, 0.88, 0.1);
                b.Plant(0.82, 0.88, 0.1);
                break;

            case RoomIds.Drawing:
                b.Sofa(0.15, 0.25, 0.35, 0.18);
                b.Armchair(0.2, 0.75, 0.09);
                b.Armchair(0.45, 0.75, 0.09);
                b.Box(0.22, 0.48, 0.2, 0.14);
                b.Piano(0.66, 0.1, 0.3, 0.55);
                b.Box(0.0, 0.3, 0.06, 0.45);
                break;

            case RoomIds.Dining:
                b.TableWithChairs(0.12, 0.38, 0.72, 0.3, 7);
                b.Box(0.3, 0.0, 0.4, 0.1);
                b.Circle(0.48, 0.53, 0.04);
                break;

            case RoomIds.Master:
                b.Bed(0.28, 0.08, 0.38, 0.6, posts: true);
                b.Box(0.82, 0.05, 0.16, 0.3);
                b.Line(0.9, 0.05, 0.9, 0.35);
                b.Box(0.02, 0.75, 0.22, 0.18);
                b.Circle(0.13, 0.66, 0.05);
                break;

            case RoomIds.Gallery:
                for (int i = 0; i < 5; i++)
                {
                    b.Box(0.06 + i * 0.19, 0.0, 0.12, 0.05);
                }

                b.Box(0.25, 0.55, 0.18, 0.15);
                b.Box(0.58, 0.55, 0.18, 0.15);
                b.Line(0.0, 0.35, 1.0, 0.35, dashed: true);
                break;

            case RoomIds.Blue:
            case RoomIds.Rose:
            case RoomIds.Green:
                b.Bed(0.08, 0.05, 0.38, 0.55, posts: false);
                b.Box(0.75, 0.05, 0.2, 0.18);
                b.Circle(0.85, 0.14, 0.05);
                b.Box(0.7, 0.7, 0.26, 0.2);
                b.Circle(0.6, 0.78, 0.05);
                break;

            case RoomIds.Landing:
                b.Stairs(0.05, 0.1, 0.3, 0.8, 7, horizontalTreads: true);
                b.Line(0.4, 0.1, 0.4, 0.9);
                b.Box(0.6, 0.3, 0.3, 0.4, dashed: true);
                break;

            case RoomIds.Quarters:
                for (int i = 0; i < 3; i++)
                {
                    b.Bed(0.06, 0.04 + i * 0.32, 0.5, 0.24, posts: false);
                }

                b.Box(0.75, 0.05, 0.2, 0.15);
                b.Stairs(0.72, 0.65, 0.26, 0.33, 4, horizontalTreads: false);
                break;
        }

        group.Freeze();
        return group;
    }

    /// <summary>Converts relative coordinates in a room interior to plan geometry.</summary>
    private sealed class Sketch
    {
        private readonly GeometryGroup _group;
        private readonly Rect _r;

        public Sketch(GeometryGroup group, Rect interior)
        {
            _group = group;
            _r = interior;
        }

        private double X(double fx) => _r.X + fx * _r.Width;
        private double Y(double fy) => _r.Y + fy * _r.Height;
        private double Min => Math.Min(_r.Width, _r.Height);

        public void Box(double x, double y, double w, double h, bool dashed = false)
        {
            if (!dashed)
            {
                _group.Children.Add(new RectangleGeometry(new Rect(X(x), Y(y), w * _r.Width, h * _r.Height)));
                return;
            }

            // A dashed outline (rugs, notional areas) built from short segments.
            Line(x, y, x + w, y, dashed: true);
            Line(x + w, y, x + w, y + h, dashed: true);
            Line(x + w, y + h, x, y + h, dashed: true);
            Line(x, y + h, x, y, dashed: true);
        }

        public void Circle(double cx, double cy, double r) =>
            _group.Children.Add(new EllipseGeometry(new Point(X(cx), Y(cy)), r * Min, r * Min));

        public void Line(double x1, double y1, double x2, double y2, bool dashed = false)
        {
            var a = new Point(X(x1), Y(y1));
            var b = new Point(X(x2), Y(y2));
            if (!dashed)
            {
                _group.Children.Add(new LineGeometry(a, b));
                return;
            }

            Vector v = b - a;
            double length = v.Length;
            if (length < 1)
            {
                return;
            }

            v /= length;
            for (double d = 0; d < length; d += 7)
            {
                _group.Children.Add(new LineGeometry(a + v * d, a + v * Math.Min(d + 4, length)));
            }
        }

        public void Plant(double cx, double cy, double r)
        {
            Circle(cx, cy, r);
            for (int i = 0; i < 6; i++)
            {
                double t = i * Math.PI / 3;
                double rr = r * Min;
                _group.Children.Add(new LineGeometry(
                    new Point(X(cx), Y(cy)),
                    new Point(X(cx) + Math.Cos(t) * rr, Y(cy) + Math.Sin(t) * rr)));
            }
        }

        public void Armchair(double cx, double cy, double r)
        {
            double s = r * Min;
            var c = new Point(X(cx), Y(cy));
            _group.Children.Add(new RectangleGeometry(new Rect(c.X - s, c.Y - s, s * 2, s * 2), s * 0.3, s * 0.3));
            _group.Children.Add(new RectangleGeometry(new Rect(c.X - s, c.Y - s, s * 2, s * 0.5)));
        }

        public void Sofa(double x, double y, double w, double h)
        {
            Box(x, y, w, h);
            Box(x, y, w, h * 0.3);
            Box(x, y, w * 0.12, h);
            Box(x + w * 0.88, y, w * 0.12, h);
        }

        public void Piano(double x, double y, double w, double h)
        {
            var figure = new PathFigure { StartPoint = new Point(X(x), Y(y)), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(X(x + w), Y(y)), true));
            figure.Segments.Add(new LineSegment(new Point(X(x + w), Y(y + h * 0.5)), true));
            figure.Segments.Add(new BezierSegment(
                new Point(X(x + w), Y(y + h)),
                new Point(X(x + w * 0.3), Y(y + h * 0.6)),
                new Point(X(x), Y(y + h)), true));
            var path = new PathGeometry();
            path.Figures.Add(figure);
            _group.Children.Add(path);
            Box(x, y, w * 0.12, h);
        }

        public void Bed(double x, double y, double w, double h, bool posts)
        {
            Box(x, y, w, h);
            Box(x + w * 0.08, y + h * 0.04, w * 0.38, h * 0.14);
            Box(x + w * 0.54, y + h * 0.04, w * 0.38, h * 0.14);
            Line(x, y + h * 0.35, x + w, y + h * 0.42);
            if (posts)
            {
                Circle(x, y, 0.025);
                Circle(x + w, y, 0.025);
                Circle(x, y + h, 0.025);
                Circle(x + w, y + h, 0.025);
            }
        }

        public void TableWithChairs(double x, double y, double w, double h, int perSide)
        {
            Box(x, y, w, h);
            double chair = 0.06;
            for (int i = 0; i < perSide; i++)
            {
                double cx = x + (i + 0.5) * w / perSide - chair / 2;
                Box(cx, y - chair - 0.03, chair, chair);
                Box(cx, y + h + 0.03, chair, chair);
            }

            Box(x - chair - 0.02, y + h / 2 - chair / 2, chair, chair);
            Box(x + w + 0.02, y + h / 2 - chair / 2, chair, chair);
        }

        public void Stairs(double x, double y, double w, double h, int treads, bool horizontalTreads)
        {
            Box(x, y, w, h);
            for (int i = 1; i < treads; i++)
            {
                if (horizontalTreads)
                {
                    double ty = y + i * h / treads;
                    Line(x, ty, x + w, ty);
                }
                else
                {
                    double tx = x + i * w / treads;
                    Line(tx, y, tx, y + h);
                }
            }

            // Direction arrow up the flight.
            if (horizontalTreads)
            {
                Line(x + w / 2, y + h * 0.9, x + w / 2, y + h * 0.1);
                Line(x + w / 2, y + h * 0.1, x + w * 0.35, y + h * 0.2);
                Line(x + w / 2, y + h * 0.1, x + w * 0.65, y + h * 0.2);
            }
            else
            {
                Line(x + w * 0.1, y + h / 2, x + w * 0.9, y + h / 2);
                Line(x + w * 0.9, y + h / 2, x + w * 0.78, y + h * 0.3);
                Line(x + w * 0.9, y + h / 2, x + w * 0.78, y + h * 0.7);
            }
        }
    }
}
