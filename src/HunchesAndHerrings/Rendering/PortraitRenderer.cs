using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Rendering;

/// <summary>
/// Draws a small ink portrait for a character in a 100 x 120 oval frame. Role decides costume
/// (collars, caps, hats); the portrait seed decides hair, whiskers, spectacles and expression.
/// </summary>
internal static class PortraitRenderer
{
    private static readonly Dictionary<string, DrawingImage> Cache = new();

    private static readonly Color InkColor = Color.FromRgb(0x2B, 0x21, 0x18);
    private static readonly Brush InkBrush = Frozen(InkColor);
    private static readonly Brush FaceBrush = Frozen(Color.FromRgb(0xF6, 0xEC, 0xD8));
    private static readonly Brush WhiteBrush = Frozen(Color.FromRgb(0xFB, 0xF7, 0xEE));
    private static readonly Brush WashBrush = Frozen(Color.FromArgb(0x38, 0xC8, 0xB4, 0x8C));
    private static readonly Brush RedInkBrush = Frozen(Color.FromRgb(0x7A, 0x2A, 0x22));
    private static readonly Brush BrassBrush = Frozen(Color.FromRgb(0xC9, 0xA2, 0x4A));
    private static readonly Pen InkPen = FrozenPen(1.4);
    private static readonly Pen ThinPen = FrozenPen(0.8);

    public static Image Create(Character character, double width) => new()
    {
        Source = Get(character),
        Width = width,
        Height = width * 1.2,
        Stretch = Stretch.Uniform,
    };

    /// <summary>The inspector's sergeant: tunic, brass buttons and a custodian helmet.</summary>
    public static Image CreateSergeant(double width, int seed) => new()
    {
        Source = Get(new Character { Id = "sergeant", FirstName = "S", Surname = "S", Gender = Gender.Male, Role = RoleKind.Valet, Station = Station.Servant, PortraitSeed = seed }, police: true),
        Width = width,
        Height = width * 1.2,
        Stretch = Stretch.Uniform,
    };

    public static DrawingImage Get(Character character, bool police = false)
    {
        string key = $"{character.Id}:{character.PortraitSeed}:{police}";
        if (!Cache.TryGetValue(key, out DrawingImage? image))
        {
            image = new DrawingImage(Build(character, police));
            image.Freeze();
            Cache[key] = image;
        }

        return image;
    }

    private static Drawing Build(Character c, bool police)
    {
        var rng = new Random(c.PortraitSeed);
        var root = new DrawingGroup();
        var frame = new EllipseGeometry(new Point(50, 60), 47, 57);

        root.Children.Add(new GeometryDrawing(WashBrush, null, frame));

        var inner = new DrawingGroup { ClipGeometry = frame };
        Brush hair = HairBrush(c, rng);

        DrawCoat(inner, c, rng);
        Add(inner, FaceBrush, ThinPen, "M 44,70 L 44,90 L 56,90 L 56,70 Z");
        if (police)
        {
            Add(inner, InkBrush, ThinPen, "M 42,86 L 58,86 L 58,93 L 42,93 Z");
            foreach (double y in new[] { 98.0, 106.0, 114.0 })
            {
                inner.Children.Add(new GeometryDrawing(BrassBrush, ThinPen, new EllipseGeometry(new Point(50, y), 2, 2)));
            }
        }
        else
        {
            DrawCollar(inner, c);
        }

        if (c.IsMale)
        {
            inner.Children.Add(new GeometryDrawing(FaceBrush, ThinPen, new EllipseGeometry(new Point(34, 55), 3, 5)));
            inner.Children.Add(new GeometryDrawing(FaceBrush, ThinPen, new EllipseGeometry(new Point(66, 55), 3, 5)));
        }

        inner.Children.Add(new GeometryDrawing(FaceBrush, InkPen, new EllipseGeometry(new Point(50, 53), 16, 20)));
        DrawHair(inner, c, hair, rng);
        DrawFace(inner, c, hair, rng, police);
        if (police)
        {
            // Custodian helmet with a brass star.
            Add(inner, InkBrush, InkPen, "M 31,40 Q 30,4 50,3 Q 70,4 69,40 Z");
            Add(inner, InkBrush, InkPen, "M 27,41 Q 50,46 73,41 L 73,44 Q 50,49 27,44 Z");
            Add(inner, BrassBrush, ThinPen, "M 50,15 L 52,20 L 57,20 L 53,23 L 55,28 L 50,25 L 45,28 L 47,23 L 43,20 L 48,20 Z");
            Add(inner, null, ThinPen, "M 38,44 Q 50,72 62,44");
        }
        else
        {
            DrawHeadwear(inner, c, rng);
        }

        root.Children.Add(inner);
        root.Children.Add(new GeometryDrawing(null, InkPen, frame));
        root.Children.Add(new GeometryDrawing(null, ThinPen, new EllipseGeometry(new Point(50.7, 60.5), 45, 55)));
        root.Freeze();
        return root;
    }

    private static void DrawCoat(DrawingGroup g, Character c, Random rng)
    {
        Geometry coat = Geometry.Parse("M 6,124 C 10,99 31,89 50,89 C 69,89 90,99 94,124 Z");
        bool dark = c.Station == Station.Servant || c.Role is RoleKind.Reverend or RoleKind.Butler;
        Color tone = c.IsMale
            ? (dark ? Color.FromArgb(0xE0, 0x2E, 0x25, 0x1D) : Color.FromArgb(0xB0, 0x5A, 0x4A, 0x3A))
            : (dark ? Color.FromArgb(0xD8, 0x33, 0x2A, 0x24) : Color.FromArgb(0xA0, 0x6B, 0x4A, 0x40));
        g.Children.Add(new GeometryDrawing(Frozen(tone), InkPen, coat));

        if (!dark)
        {
            var hatch = new DrawingGroup { ClipGeometry = coat };
            hatch.Children.Add(new GeometryDrawing(null, ThinPen, InkGeometry.Hatch(new Rect(0, 86, 100, 40), 4 + rng.Next(3), rng)));
            g.Children.Add(hatch);
        }
    }

    private static void DrawCollar(DrawingGroup g, Character c)
    {
        switch (c.Role)
        {
            case RoleKind.Butler:
            case RoleKind.Valet:
                Add(g, WhiteBrush, ThinPen, "M 44,86 L 50,92 L 45,97 Z");
                Add(g, WhiteBrush, ThinPen, "M 56,86 L 50,92 L 55,97 Z");
                Add(g, InkBrush, null, "M 45,89 L 50,92 L 45,95 Z M 55,89 L 50,92 L 55,95 Z");
                break;
            case RoleKind.Reverend:
                Add(g, WhiteBrush, ThinPen, "M 44,86 L 56,86 L 56,91 L 44,91 Z");
                break;
            case RoleKind.Colonel:
                Add(g, WhiteBrush, ThinPen, "M 44,88 L 50,102 L 56,88 Z");
                Add(g, Frozen(Color.FromRgb(0xC9, 0xA2, 0x4A)), ThinPen, "M 16,102 Q 25,94 34,96 L 32,102 Q 24,101 16,106 Z");
                Add(g, Frozen(Color.FromRgb(0xC9, 0xA2, 0x4A)), ThinPen, "M 84,102 Q 75,94 66,96 L 68,102 Q 76,101 84,106 Z");
                g.Children.Add(new GeometryDrawing(RedInkBrush, ThinPen, new EllipseGeometry(new Point(38, 106), 2.4, 2.4)));
                g.Children.Add(new GeometryDrawing(RedInkBrush, ThinPen, new EllipseGeometry(new Point(43, 107), 2.4, 2.4)));
                break;
            case RoleKind.Maid:
            case RoleKind.Cook:
                Add(g, WhiteBrush, ThinPen, "M 41,97 L 59,97 L 61,124 L 39,124 Z");
                Add(g, WhiteBrush, ThinPen, "M 42,88 Q 50,96 58,88 L 58,91 Q 50,99 42,91 Z");
                break;
            case RoleKind.Housekeeper:
            case RoleKind.Governess:
                Add(g, WhiteBrush, ThinPen, "M 42,88 Q 50,96 58,88 L 58,91 Q 50,99 42,91 Z");
                if (c.Role == RoleKind.Housekeeper)
                {
                    Add(g, null, ThinPen, "M 58,100 L 62,112 M 60,100 L 66,110");
                }

                break;
            case RoleKind.Gardener:
                Add(g, WhiteBrush, ThinPen, "M 45,88 L 50,96 L 55,88 Z");
                break;
            default:
                if (c.IsMale)
                {
                    Add(g, WhiteBrush, ThinPen, "M 44,88 L 50,104 L 56,88 Z");
                    Add(g, RedInkBrush, ThinPen, "M 47,89 L 53,89 L 50,98 Z");
                }
                else
                {
                    Add(g, null, ThinPen, "M 41,86 Q 50,97 59,86");
                    g.Children.Add(new GeometryDrawing(WhiteBrush, ThinPen, new EllipseGeometry(new Point(50, 95), 2.2, 2.6)));
                }

                break;
        }
    }

    private static void DrawHair(DrawingGroup g, Character c, Brush hair, Random rng)
    {
        bool capped = c.Role is RoleKind.Maid or RoleKind.Cook && !c.IsMale;
        if (c.IsMale)
        {
            bool balding = c.Traits.Frail || rng.NextDouble() < 0.2;
            if (balding)
            {
                Add(g, hair, ThinPen, "M 34,56 C 33,47 35,42 39,40 L 40,51 Z");
                Add(g, hair, ThinPen, "M 66,56 C 67,47 65,42 61,40 L 60,51 Z");
            }
            else
            {
                Add(g, hair, ThinPen, "M 34,52 C 32,34 42,30 50,30 C 60,30 68,36 66,52 C 63,42 57,38 50,38 C 43,38 37,42 34,52 Z");
            }

            return;
        }

        Add(g, hair, ThinPen, "M 33,60 C 29,36 40,28 50,28 C 60,28 71,36 67,60 C 64,46 58,38 50,37 C 42,38 36,46 33,60 Z");
        if (capped)
        {
            return;
        }

        if (rng.NextDouble() < 0.5 || c.Station == Station.Servant)
        {
            g.Children.Add(new GeometryDrawing(hair, ThinPen, new EllipseGeometry(new Point(50, 27), 8, 6)));
        }
        else
        {
            g.Children.Add(new GeometryDrawing(hair, ThinPen, new EllipseGeometry(new Point(32, 64), 3.5, 7)));
            g.Children.Add(new GeometryDrawing(hair, ThinPen, new EllipseGeometry(new Point(68, 64), 3.5, 7)));
        }
    }

    private static void DrawFace(DrawingGroup g, Character c, Brush hair, Random rng, bool police = false)
    {
        double browTilt = (rng.NextDouble() - 0.5) * 3;
        g.Children.Add(new GeometryDrawing(InkBrush, null, new EllipseGeometry(new Point(44, 52), 1.6, 1.8)));
        g.Children.Add(new GeometryDrawing(InkBrush, null, new EllipseGeometry(new Point(56, 52), 1.6, 1.8)));
        Add(g, null, ThinPen, FormattableString.Invariant($"M 40,{47 + browTilt:0.0} L 47,{46 - browTilt:0.0} M 53,{46 - browTilt:0.0} L 60,{47 + browTilt:0.0}"));
        Add(g, null, ThinPen, "M 50,52 L 48,61 L 51,62");
        double mouth = 66 + rng.Next(-1, 3);
        Add(g, null, ThinPen, FormattableString.Invariant($"M 45,67 Q 50,{mouth:0.0} 55,67"));

        bool spectacles = c.Role switch
        {
            RoleKind.Doctor => rng.NextDouble() < 0.7,
            RoleKind.Governess or RoleKind.Solicitor => rng.NextDouble() < 0.6,
            RoleKind.Housekeeper or RoleKind.Reverend => rng.NextDouble() < 0.3,
            _ => rng.NextDouble() < 0.08,
        };

        if (c.Role == RoleKind.Colonel)
        {
            g.Children.Add(new GeometryDrawing(null, InkPen, new EllipseGeometry(new Point(56, 52), 4.6, 4.6)));
            Add(g, null, ThinPen, "M 60,55 Q 66,70 60,88");
        }
        else if (spectacles)
        {
            g.Children.Add(new GeometryDrawing(null, ThinPen, new EllipseGeometry(new Point(44, 52), 4.5, 4)));
            g.Children.Add(new GeometryDrawing(null, ThinPen, new EllipseGeometry(new Point(56, 52), 4.5, 4)));
            Add(g, null, ThinPen, "M 48.5,52 L 51.5,52");
        }

        if (!c.IsMale)
        {
            return;
        }

        bool moustache = police || c.Role is RoleKind.Colonel or RoleKind.BusinessPartner || rng.NextDouble() < 0.4;
        bool chops = rng.NextDouble() < 0.3;
        bool beard = c.Role is RoleKind.Gardener or RoleKind.Reverend ? rng.NextDouble() < 0.5 : rng.NextDouble() < 0.12;

        if (beard)
        {
            Add(g, hair, ThinPen, "M 35,58 Q 36,79 50,81 Q 64,79 65,58 Q 60,71 50,72 Q 40,71 35,58 Z");
        }
        else if (chops)
        {
            Add(g, hair, ThinPen, "M 34,50 Q 33,64 41,68 L 42,62 Q 38,58 37,50 Z");
            Add(g, hair, ThinPen, "M 66,50 Q 67,64 59,68 L 58,62 Q 62,58 63,50 Z");
        }

        if (moustache || beard)
        {
            Add(g, hair, ThinPen, c.Role == RoleKind.Colonel
                ? "M 38,62 Q 44,59 50,63 Q 56,59 62,62 Q 56,67 50,65 Q 44,67 38,62 Z"
                : "M 43,64 Q 47,61 50,63 Q 53,61 57,64 Q 53,66 50,65 Q 47,66 43,64 Z");
        }
    }

    private static void DrawHeadwear(DrawingGroup g, Character c, Random rng)
    {
        switch (c.Role)
        {
            case RoleKind.Maid:
            case RoleKind.Cook when !c.IsMale:
                g.Children.Add(new GeometryDrawing(WhiteBrush, InkPen, new EllipseGeometry(new Point(50, 33), 20, 10)));
                Add(g, null, ThinPen, "M 31,37 Q 50,44 69,37");
                break;
            case RoleKind.Gardener:
                Add(g, Frozen(Color.FromArgb(0xD0, 0x5A, 0x4A, 0x36)), InkPen, "M 33,40 Q 34,25 50,25 Q 66,25 68,38 L 75,42 Q 60,45 33,40 Z");
                break;
            case RoleKind.BusinessPartner when c.IsMale:
                Add(g, InkBrush, InkPen, "M 34,36 Q 34,14 50,14 Q 66,14 66,36 Z");
                g.Children.Add(new GeometryDrawing(InkBrush, null, new EllipseGeometry(new Point(50, 36), 21, 3)));
                break;
            case RoleKind.Master when c.IsMale:
            case RoleKind.Heir when c.IsMale && rng.NextDouble() < 0.4:
                Add(g, InkBrush, InkPen, "M 36,36 L 38,8 L 62,8 L 64,36 Z");
                g.Children.Add(new GeometryDrawing(InkBrush, null, new EllipseGeometry(new Point(50, 36), 23, 3.5)));
                break;
            case RoleKind.Companion:
                Add(g, Frozen(Color.FromArgb(0xC8, 0x4A, 0x3A, 0x30)), InkPen, "M 30,62 Q 26,26 50,24 Q 74,26 70,62 Q 66,40 50,36 Q 34,40 30,62 Z");
                Add(g, null, ThinPen, "M 31,62 Q 40,74 48,76 M 69,62 Q 60,74 52,76");
                break;
        }
    }

    private static Brush HairBrush(Character c, Random rng)
    {
        Color[] tones =
        {
            Color.FromArgb(0xE6, 0x3A, 0x2E, 0x22),
            Color.FromArgb(0xE0, 0x6A, 0x4A, 0x2A),
            Color.FromArgb(0xD0, 0xA8, 0x8A, 0x5A),
            Color.FromArgb(0xE0, 0x1E, 0x18, 0x14),
        };
        bool grey = c.Traits.Frail || c.Role is RoleKind.Colonel or RoleKind.Reverend or RoleKind.Housekeeper && rng.NextDouble() < 0.6;
        return Frozen(grey ? Color.FromArgb(0xD0, 0x9A, 0x96, 0x8E) : tones[rng.Next(tones.Length)]);
    }

    private static void Add(DrawingGroup g, Brush? fill, Pen? pen, string data) =>
        g.Children.Add(new GeometryDrawing(fill, pen, Geometry.Parse(data)));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(double thickness)
    {
        var pen = new Pen(Frozen(InkColor), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }
}
