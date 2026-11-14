using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using ShapePath = System.Windows.Shapes.Path;

namespace HunchesAndHerrings.Rendering;

/// <summary>What to draw: one floor of the house at a chosen hour, plus click handlers.</summary>
internal sealed record MapView(GameSession Game, Floor Floor, int HourIndex, Action<string> OnRoomClicked, Action<string> OnPersonClicked);

/// <summary>
/// Draws one floor of the manor as an architect's blueprint: walls, doors, windows and furniture in
/// white line on blue, with the crime scene, your position and everyone's whereabouts overlaid.
/// </summary>
internal static class MapRenderer
{
    public const double PlanWidth = 640;
    public const double PlanHeight = 432;
    private const double PlanMin = 20, PlanMax = 620, PlanTop = 20, PlanBottom = 380;

    public static readonly Color BlueprintColor = Color.FromRgb(0x1D, 0x4E, 0x89);
    private static readonly Brush Background = Frozen(BlueprintColor);
    private static readonly Brush Line = Frozen(Color.FromRgb(0xE8, 0xF1, 0xFB));
    private static readonly Brush LineFaint = Frozen(Color.FromArgb(0x80, 0xE8, 0xF1, 0xFB));
    private static readonly Brush RoomFill = Frozen(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
    private static readonly Brush HoverFill = Frozen(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
    private static readonly Brush CurrentFill = Frozen(Color.FromArgb(0x30, 0xFF, 0xE9, 0xA0));
    private static readonly Brush SceneFill = Frozen(Color.FromArgb(0x50, 0xD3, 0x3A, 0x2C));
    private static readonly Brush Red = Frozen(Color.FromRgb(0xFF, 0x5C, 0x4D));
    private static readonly Brush Amber = Frozen(Color.FromRgb(0xFF, 0xC9, 0x40));
    private static readonly Brush MarkerText = Frozen(BlueprintColor);

    public static string FloorTitle(Floor floor) => floor switch
    {
        Floor.Upper => "UPPER FLOOR",
        Floor.Cellar => "CELLAR · BELOW STAIRS",
        _ => "GROUND FLOOR",
    };

    public static void Render(Canvas canvas, MapView view)
    {
        canvas.Children.Clear();
        canvas.Width = PlanWidth;
        canvas.Height = PlanHeight;

        GameSession game = view.Game;
        Manor manor = game.Case.Manor;
        var rooms = manor.OnFloor(view.Floor).ToList();

        DrawPaper(canvas);
        foreach (Room room in rooms)
        {
            DrawRoom(canvas, game, room, view.OnRoomClicked);
        }

        foreach (Connection door in manor.Connections.Where(c => !c.IsStairs && manor[c.A].Floor == view.Floor))
        {
            DrawDoor(canvas, manor[door.A], manor[door.B]);
        }

        foreach (Room room in rooms)
        {
            DrawWindows(canvas, room);
            canvas.Children.Add(new ShapePath
            {
                Data = Furniture.For(room.Id, ToRect(room.Bounds)),
                Stroke = LineFaint,
                StrokeThickness = 0.8,
                IsHitTestVisible = false,
            });
        }

        Room? scene = rooms.FirstOrDefault(r => r.Id == game.Case.CrimeSceneId);
        if (scene is not null)
        {
            DrawCrimeScene(canvas, scene);
        }

        DrawTrail(canvas, game, view.Floor);
        foreach (Room room in rooms)
        {
            DrawLabels(canvas, game, room);
        }

        DrawSergeant(canvas, game, view.Floor);

        DrawWhereabouts(canvas, view, rooms);
        DrawTitleBlock(canvas, view, scene is not null);
    }

    // ------------------------------------------------------------------ paper and rooms

    private static void DrawPaper(Canvas canvas)
    {
        canvas.Children.Add(new Rectangle { Width = PlanWidth, Height = PlanHeight, Fill = Background, IsHitTestVisible = false });

        var minor = new GeometryGroup();
        var major = new GeometryGroup();
        for (double x = 0; x <= PlanWidth; x += 20)
        {
            (x % 100 == 0 ? major : minor).Children.Add(new LineGeometry(new Point(x, 0), new Point(x, PlanHeight)));
        }

        for (double y = 0; y <= PlanHeight; y += 20)
        {
            (y % 100 == 0 ? major : minor).Children.Add(new LineGeometry(new Point(0, y), new Point(PlanWidth, y)));
        }

        canvas.Children.Add(new ShapePath { Data = minor, Stroke = Line, StrokeThickness = 0.4, Opacity = 0.09, IsHitTestVisible = false });
        canvas.Children.Add(new ShapePath { Data = major, Stroke = Line, StrokeThickness = 0.6, Opacity = 0.18, IsHitTestVisible = false });
    }

    private static void DrawRoom(Canvas canvas, GameSession game, Room room, Action<string> onRoomClicked)
    {
        Rect rect = ToRect(room.Bounds);
        bool current = room.Id == game.RoomId;
        Brush fill = current ? CurrentFill : RoomFill;

        var body = new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Fill = fill,
            Stroke = Line,
            StrokeThickness = room.IsOutdoors ? 1.2 : 2.6,
            StrokeDashArray = room.IsOutdoors ? new DoubleCollection { 4, 3 } : null,
            Cursor = Cursors.Hand,
        };
        Route? route = game.Case.Manor.FindRoute(game.RoomId, room.Id);
        body.ToolTip = current ? $"{room.Name} (you are here)" : $"{room.Name}: {route?.Minutes ?? 0} minutes' walk";
        body.MouseEnter += (_, _) => body.Fill = HoverFill;
        body.MouseLeave += (_, _) => body.Fill = fill;
        body.MouseLeftButtonUp += (_, _) => onRoomClicked(room.Id);
        Place(canvas, body, rect.X, rect.Y);

        if (!room.IsOutdoors)
        {
            // Inner face of the wall.
            var inner = new Rectangle { Width = rect.Width - 6, Height = rect.Height - 6, Stroke = LineFaint, StrokeThickness = 0.8, IsHitTestVisible = false };
            Place(canvas, inner, rect.X + 3, rect.Y + 3);
        }
    }

    /// <summary>Where the doorway between two rooms sits, and how its leaf swings.</summary>
    private sealed record Doorway(Rect Gap, Point Hinge, bool InHorizontalWall)
    {
        public Point Center => new(Gap.X + Gap.Width / 2, Gap.Y + Gap.Height / 2);
    }

    private const double DoorLeaf = 13;

    /// <summary>Computes the doorway for a connection (always in the connection's own A/B order, so it's stable).</summary>
    private static Doorway? ComputeDoor(Room roomA, Room roomB)
    {
        RoomRect a = roomA.Bounds, b = roomB.Bounds;
        var rng = new Random(StableHash.Of(roomA.Id + roomB.Id));
        double ox1 = Math.Max(a.X, b.X), ox2 = Math.Min(a.Right, b.Right);
        double oy1 = Math.Max(a.Y, b.Y), oy2 = Math.Min(a.Bottom, b.Bottom);

        if (ox2 - ox1 > 16)
        {
            double x = (ox1 + ox2) / 2 + (rng.NextDouble() - 0.5) * 24;
            double top = Math.Min(a.Bottom, b.Bottom);
            double bottom = Math.Max(a.Y, b.Y);
            return new Doorway(new Rect(x - DoorLeaf / 2 - 1, top - 3, DoorLeaf + 2, Math.Max(0, bottom - top) + 6), new Point(x - DoorLeaf / 2, bottom + 2), true);
        }

        if (oy2 - oy1 > 16)
        {
            double y = (oy1 + oy2) / 2 + (rng.NextDouble() - 0.5) * 18;
            double left = Math.Min(a.Right, b.Right);
            double right = Math.Max(a.X, b.X);
            return new Doorway(new Rect(left - 3, y - DoorLeaf / 2 - 1, Math.Max(0, right - left) + 6, DoorLeaf + 2), new Point(right + 2, y - DoorLeaf / 2), false);
        }

        return null;
    }

    private static void DrawDoor(Canvas canvas, Room roomA, Room roomB)
    {
        Doorway? door = ComputeDoor(roomA, roomB);
        if (door is null)
        {
            return;
        }

        Rect gap = door.Gap;
        Point hinge = door.Hinge;
        var swing = new StreamGeometry();
        using (StreamGeometryContext ctx = swing.Open())
        {
            ctx.BeginFigure(hinge, false, false);
            if (door.InHorizontalWall)
            {
                ctx.LineTo(new Point(hinge.X, hinge.Y + DoorLeaf), true, false);
                ctx.ArcTo(new Point(hinge.X + DoorLeaf, hinge.Y), new Size(DoorLeaf, DoorLeaf), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            else
            {
                ctx.LineTo(new Point(hinge.X + DoorLeaf, hinge.Y), true, false);
                ctx.ArcTo(new Point(hinge.X, hinge.Y + DoorLeaf), new Size(DoorLeaf, DoorLeaf), 0, false, SweepDirection.Clockwise, true, false);
            }
        }

        var opening = new Rectangle { Width = gap.Width, Height = gap.Height, Fill = Background, IsHitTestVisible = false };
        Place(canvas, opening, gap.X, gap.Y);

        var jambs = new GeometryGroup();
        if (door.InHorizontalWall)
        {
            jambs.Children.Add(new LineGeometry(gap.TopLeft, gap.BottomLeft));
            jambs.Children.Add(new LineGeometry(gap.TopRight, gap.BottomRight));
        }
        else
        {
            jambs.Children.Add(new LineGeometry(gap.TopLeft, gap.TopRight));
            jambs.Children.Add(new LineGeometry(gap.BottomLeft, gap.BottomRight));
        }

        canvas.Children.Add(new ShapePath { Data = jambs, Stroke = Line, StrokeThickness = 1.2, IsHitTestVisible = false });
        canvas.Children.Add(new ShapePath { Data = swing, Stroke = LineFaint, StrokeThickness = 0.8, IsHitTestVisible = false });
    }

    // ------------------------------------------------------------------ the inspector's trail

    /// <summary>
    /// Faint footprints along the routes walked so far (newer steps darker), and numbered stops
    /// in the rooms where the inspector stopped.
    /// </summary>
    private static void DrawTrail(Canvas canvas, GameSession game, Floor floor)
    {
        Manor manor = game.Case.Manor;
        IReadOnlyList<TrailStep> trail = game.Trail;
        const int recent = 40;
        int firstShown = Math.Max(1, trail.Count - recent);

        for (int i = firstShown; i < trail.Count; i++)
        {
            Room from = manor[trail[i - 1].RoomId];
            Room to = manor[trail[i].RoomId];
            if (from.Floor != floor || to.Floor != floor || from.Id == to.Id)
            {
                continue;
            }

            Connection? link = manor.Connections.FirstOrDefault(c => !c.IsStairs && c.Touches(from.Id) && c.Touches(to.Id));
            Doorway? door = link is null ? null : ComputeDoor(manor[link.A], manor[link.B]);
            var a = new Point(from.Bounds.CenterX, from.Bounds.CenterY + 8);
            var b = new Point(to.Bounds.CenterX, to.Bounds.CenterY + 8);
            double age = (trail.Count - i) / (double)recent;
            double opacity = 0.75 - age * 0.55;
            int seed = i * 31;

            if (door is null)
            {
                DrawFootprints(canvas, a, b, opacity, seed);
            }
            else
            {
                DrawFootprints(canvas, a, door.Center, opacity, seed);
                DrawFootprints(canvas, door.Center, b, opacity, seed + 7);
            }
        }

        // Numbered stops (most recent dozen), grouped by room.
        var stops = trail
            .Select((step, index) => (step, index))
            .Where(x => x.step.IsStop)
            .ToList();
        var numbered = stops
            .Select((x, n) => (x.step, Number: n + 1))
            .TakeLast(12)
            .Where(x => manor[x.step.RoomId].Floor == floor)
            .GroupBy(x => x.step.RoomId);

        foreach (var group in numbered)
        {
            RoomRect r = manor[group.Key].Bounds;
            string label = string.Join(",", group.Select(x => x.Number));
            var stamp = new Border
            {
                BorderBrush = LineFaint,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5, 0, 5, 0),
                Child = new TextBlock { Text = $"👣 {label}", FontFamily = Ui.Hand, FontSize = 9.5, Foreground = LineFaint },
                IsHitTestVisible = false,
                ToolTip = "Your stops, in order",
            };
            Place(canvas, stamp, r.Right - 14 - (label.Length * 6 + 26), r.Bottom - 20);
        }
    }

    private static void DrawFootprints(Canvas canvas, Point from, Point to, double opacity, int seed)
    {
        Vector v = to - from;
        double length = v.Length;
        if (length < 8)
        {
            return;
        }

        v /= length;
        var normal = new Vector(-v.Y, v.X);
        double angle = Math.Atan2(v.Y, v.X) * 180 / Math.PI + 90;
        var prints = new GeometryGroup();
        var rng = new Random(seed);
        int k = 0;
        for (double d = 6; d < length - 4; d += 11, k++)
        {
            Point centre = from + v * d + normal * ((k % 2 == 0 ? 3.2 : -3.2) + (rng.NextDouble() - 0.5));
            var sole = new EllipseGeometry(centre, 1.7, 3.0) { Transform = new RotateTransform(angle, centre.X, centre.Y) };
            prints.Children.Add(sole);
        }

        canvas.Children.Add(new ShapePath { Data = prints, Fill = Amber, Opacity = Math.Clamp(opacity, 0.15, 0.8), IsHitTestVisible = false });
    }

    // ------------------------------------------------------------------ the sergeant

    private static void DrawSergeant(Canvas canvas, GameSession game, Floor floor)
    {
        Room room = game.Case.Manor[game.SergeantRoomId];
        if (room.Floor != floor)
        {
            return;
        }

        bool withYou = game.Sergeant is null;
        var badge = new Border
        {
            Background = Frozen(Color.FromRgb(0x23, 0x39, 0x5B)),
            BorderBrush = Amber,
            BorderThickness = new Thickness(1.6),
            CornerRadius = new CornerRadius(4, 4, 10, 10),
            Padding = new Thickness(4, 0, 4, 1),
            Child = new TextBlock { Text = "★ SGT", FontFamily = Ui.Hand, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Amber },
            ToolTip = $"{game.Case.SergeantName}: {game.SergeantStatus}",
        };
        RoomRect r = room.Bounds;
        Place(canvas, badge, withYou ? r.Right - 150 : r.Right - 52, withYou ? r.Y - 9 : r.Y + 21);
    }

    private static void DrawWindows(Canvas canvas, Room room)
    {
        if (room.IsOutdoors)
        {
            return;
        }

        RoomRect r = room.Bounds;
        var windows = new GeometryGroup();
        void Horizontal(double y)
        {
            foreach (double f in new[] { 0.3, 0.7 })
            {
                double x = r.X + r.Width * f;
                var w = new Rect(x - 11, y - 3, 22, 6);
                windows.Children.Add(new RectangleGeometry(w));
                windows.Children.Add(new LineGeometry(new Point(w.Left, y), new Point(w.Right, y)));
            }
        }

        void Vertical(double x)
        {
            foreach (double f in new[] { 0.35, 0.7 })
            {
                double y = r.Y + r.Height * f;
                var w = new Rect(x - 3, y - 11, 6, 22);
                windows.Children.Add(new RectangleGeometry(w));
                windows.Children.Add(new LineGeometry(new Point(x, w.Top), new Point(x, w.Bottom)));
            }
        }

        if (Math.Abs(r.Y - PlanTop) < 1) Horizontal(r.Y);
        if (Math.Abs(r.Bottom - PlanBottom) < 1) Horizontal(r.Bottom);
        if (Math.Abs(r.X - PlanMin) < 1) Vertical(r.X);
        if (Math.Abs(r.Right - PlanMax) < 1) Vertical(r.Right);

        canvas.Children.Add(new ShapePath { Data = windows, Stroke = Line, StrokeThickness = 1, Fill = Background, IsHitTestVisible = false });
    }

    // ------------------------------------------------------------------ the crime scene

    private static void DrawCrimeScene(Canvas canvas, Room scene)
    {
        Rect rect = ToRect(scene.Bounds);
        var rng = new Random(StableHash.Of(scene.Id));

        canvas.Children.Add(new ShapePath
        {
            Data = new RectangleGeometry(rect),
            Fill = SceneFill,
            Stroke = Red,
            StrokeThickness = 3,
            IsHitTestVisible = false,
        });
        canvas.Children.Add(new ShapePath
        {
            Data = InkGeometry.Hatch(rect, 10, rng),
            Stroke = Red,
            StrokeThickness = 0.7,
            Opacity = 0.5,
            Clip = new RectangleGeometry(rect),
            IsHitTestVisible = false,
        });

        // Chalk outline of the body.
        double s = Math.Min(rect.Width, rect.Height) / 110;
        var body = Geometry.Parse("M 0,-28 m -6,0 a 6,6 0 1 0 12,0 a 6,6 0 1 0 -12,0 M -7,-20 L 7,-20 L 9,6 L 16,26 L 11,28 L 2,8 L -2,8 L -10,28 L -15,26 L -9,6 Z M -7,-18 L -22,-6 L -19,-3 L -6,-12 M 7,-18 L 20,-30 L 23,-27 L 8,-12").Clone();
        body.Transform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(s * 1.1, s * 1.1),
                new RotateTransform(rng.Next(-60, 60)),
                new TranslateTransform(rect.X + rect.Width * 0.55, rect.Y + rect.Height * 0.58),
            },
        };
        canvas.Children.Add(new ShapePath { Data = body, Stroke = Line, StrokeThickness = 1.6, StrokeDashArray = new DoubleCollection { 2, 1.5 }, IsHitTestVisible = false });

        var stamp = new Border
        {
            BorderBrush = Red,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(4, 0, 4, 0),
            Child = new TextBlock { Text = "SCENE OF THE CRIME", FontFamily = Ui.Hand, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Red },
            RenderTransform = new RotateTransform(-7),
            IsHitTestVisible = false,
        };
        Place(canvas, stamp, rect.X + 6, rect.Bottom - 24);
    }

    // ------------------------------------------------------------------ labels and markers

    private static void DrawLabels(Canvas canvas, GameSession game, Room room)
    {
        Rect rect = ToRect(room.Bounds);
        Manor manor = game.Case.Manor;

        Place(canvas, Label(room.Name.ToUpperInvariant(), 11.5, Line, bold: true), rect.Left + 7, rect.Top + 5);

        if (game.SearchedRooms.Contains(room.Id))
        {
            Place(canvas, Label("✓ searched", 10, LineFaint), rect.Right - 62, rect.Top + 5);
        }

        if (manor.HasStairs(room.Id))
        {
            string where = string.Join(" ", manor.Connections
                .Where(c => c.IsStairs && c.Touches(room.Id))
                .Select(c => manor[c.Other(room.Id)].Floor)
                .Distinct()
                .OrderByDescending(f => f)
                .Select(f => f > room.Floor ? $"▲ {(f == Floor.Upper ? "UPPER" : "GROUND")}" : $"▼ {(f == Floor.Cellar ? "CELLAR" : "GROUND")}"));
            Place(canvas, Label(where, 10, Amber, bold: true), rect.Left + 7, rect.Top + 19);
        }

        if (room.Id == game.RoomId)
        {
            var pin = new Grid { IsHitTestVisible = false };
            pin.Children.Add(new Border
            {
                Background = Red,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 1, 7, 1),
                Child = new TextBlock { Text = "◉ YOU ARE HERE", FontFamily = Ui.Hand, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Line },
            });
            Place(canvas, pin, rect.Right - 104, rect.Top - 9);
        }

    }

    private static void DrawWhereabouts(Canvas canvas, MapView view, IReadOnlyList<Room> rooms)
    {
        GameSession game = view.Game;
        var onFloor = rooms.Select(r => r.Id).ToHashSet();
        var shown = game.WhereaboutsAt(view.HourIndex)
            .Where(w => onFloor.Contains(w.RoomId) && (w.Certainty != Certainty.Rough || w.Precise))
            .GroupBy(w => w.RoomId);
        bool now = view.HourIndex == game.HourIndex;

        foreach (var group in shown)
        {
            Rect rect = ToRect(game.Case.Manor[group.Key].Bounds);
            double x = rect.Left + 18;
            double y = rect.Bottom - (group.Key == game.Case.CrimeSceneId ? 44 : 18);

            foreach (Whereabout w in group.OrderByDescending(w => game.Focused.Contains(w.Person.Id)))
            {
                bool focused = game.Focused.Contains(w.Person.Id);
                bool known = w.Certainty != Certainty.Rough;
                bool clickable = now && w.RoomId == game.RoomId && w.Certainty == Certainty.Seen;

                var marker = new Grid { Width = 34, Height = 34, Cursor = clickable ? Cursors.Hand : Cursors.Arrow };
                if (focused)
                {
                    marker.Children.Add(new Ellipse { Width = 32, Height = 32, Stroke = Amber, StrokeThickness = 2.6 });
                }

                marker.Children.Add(new Ellipse
                {
                    Width = 22,
                    Height = 22,
                    Fill = known ? Line : null,
                    Stroke = Line,
                    StrokeThickness = 1.4,
                    StrokeDashArray = known ? null : new DoubleCollection { 2, 2 },
                });
                marker.Children.Add(new TextBlock
                {
                    Text = known ? w.Person.Initials : $"{w.Person.Initials}?",
                    FontFamily = Ui.Hand,
                    FontSize = known ? 10 : 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = known ? MarkerText : Line,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                });

                string how = w.Certainty switch
                {
                    Certainty.Seen => "seen here",
                    Certainty.Told => "you were told they'd be here",
                    _ => "probably here, by the household's routine",
                };
                marker.ToolTip = $"{w.Person.ShortName}, {w.Person.RoleLabel}: {how}{(clickable ? ". Click to question." : ".")}";

                if (clickable)
                {
                    string id = w.Person.Id;
                    marker.MouseLeftButtonUp += (_, e) =>
                    {
                        view.OnPersonClicked(id);
                        e.Handled = true;
                    };
                }

                Place(canvas, marker, x - 17, y - 17);
                if (focused)
                {
                    Place(canvas, Label(w.Person.ShortName, 9.5, Amber, bold: true), x - 16, y + 14);
                }

                x += 36;
                if (x > rect.Right - 16)
                {
                    x = rect.Left + 18;
                    y -= 36;
                }
            }
        }
    }

    private static void DrawTitleBlock(Canvas canvas, MapView view, bool sceneOnFloor)
    {
        GameSession game = view.Game;
        var frame = new Rectangle { Width = PlanWidth - 40, Height = 36, Stroke = Line, StrokeThickness = 1.2, IsHitTestVisible = false };
        Place(canvas, frame, 20, 390);
        canvas.Children.Add(new ShapePath
        {
            Data = new GeometryGroup
            {
                Children =
                {
                    new LineGeometry(new Point(250, 390), new Point(250, 426)),
                    new LineGeometry(new Point(470, 390), new Point(470, 426)),
                },
            },
            Stroke = Line,
            StrokeThickness = 1,
            IsHitTestVisible = false,
        });

        Place(canvas, Label($"{FloorTitle(view.Floor)} PLAN", 13, Line, bold: true), 28, 393);
        Place(canvas, Label(sceneOnFloor
                ? $"✕ SCENE OF THE CRIME: {game.Case.CrimeScene.Name.ToUpperInvariant()}"
                : game.Case.Manor.Name.ToUpperInvariant(), 9.5, sceneOnFloor ? Red : LineFaint, bold: sceneOnFloor), 28, 411);

        int hour = GameRules.DayStartHour + view.HourIndex;
        bool now = view.HourIndex == game.HourIndex;
        Place(canvas, Label($"WHEREABOUTS AT {Clock.Hour(hour).ToUpperInvariant()}{(now ? " (NOW)" : string.Empty)}", 11, Amber, bold: true), 258, 393);
        Place(canvas, Label("● known   ◌ probable   ◯ watched", 9.5, LineFaint), 258, 411);
        Place(canvas, Label($"DRAWN BY DET. {game.Detective.Surname.ToUpperInvariant()}", 9.5, LineFaint), 478, 393);
        Place(canvas, Label("NOT TO SCALE", 9.5, LineFaint), 478, 411);
    }

    private static TextBlock Label(string text, double size, Brush brush, bool bold = false) => new()
    {
        Text = text,
        FontFamily = Ui.Hand,
        FontSize = size,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Foreground = brush,
        IsHitTestVisible = false,
    };

    private static void Place(Canvas canvas, UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }

    private static Rect ToRect(RoomRect r) => new(r.X, r.Y, r.Width, r.Height);

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
