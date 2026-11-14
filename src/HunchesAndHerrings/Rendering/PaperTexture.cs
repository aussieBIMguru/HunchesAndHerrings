using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HunchesAndHerrings.Rendering;

/// <summary>Generates a tileable aged-paper texture: mottled tone, faint fibres and the odd foxing spot.</summary>
internal static class PaperTexture
{
    private const int Size = 256;
    private static ImageBrush? _brush;

    public static ImageBrush Brush => _brush ??= Create();

    private static ImageBrush Create()
    {
        var rng = new Random(1887);
        var pixels = new byte[Size * Size * 4];

        // Low-frequency mottling from a small grid of random values, bilinearly interpolated (wrapping).
        const int grid = 8;
        var lattice = new double[grid, grid];
        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                lattice[x, y] = rng.NextDouble();
            }
        }

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double gx = x / (double)Size * grid;
                double gy = y / (double)Size * grid;
                int x0 = (int)gx % grid;
                int y0 = (int)gy % grid;
                int x1 = (x0 + 1) % grid;
                int y1 = (y0 + 1) % grid;
                double fx = gx - Math.Floor(gx);
                double fy = gy - Math.Floor(gy);
                double mottle = Lerp(Lerp(lattice[x0, y0], lattice[x1, y0], fx), Lerp(lattice[x0, y1], lattice[x1, y1], fx), fy);
                double grain = rng.NextDouble();
                double tone = 0.86 + mottle * 0.08 + grain * 0.04;

                int i = (y * Size + x) * 4;
                pixels[i + 0] = Clamp(211 * tone); // B
                pixels[i + 1] = Clamp(234 * tone); // G
                pixels[i + 2] = Clamp(248 * tone); // R
                pixels[i + 3] = 255;
            }
        }

        // Fibres: short faint darker strokes.
        for (int f = 0; f < 140; f++)
        {
            double x = rng.NextDouble() * Size;
            double y = rng.NextDouble() * Size;
            double angle = rng.NextDouble() * Math.PI;
            int length = rng.Next(4, 14);
            for (int s = 0; s < length; s++)
            {
                int px = ((int)(x + Math.Cos(angle) * s) % Size + Size) % Size;
                int py = ((int)(y + Math.Sin(angle) * s) % Size + Size) % Size;
                Darken(pixels, px, py, 0.95);
            }
        }

        // Foxing: a few soft brown spots.
        for (int spot = 0; spot < 5; spot++)
        {
            int cx = rng.Next(Size);
            int cy = rng.Next(Size);
            int radius = rng.Next(3, 9);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    double d = Math.Sqrt(dx * dx + dy * dy) / radius;
                    if (d <= 1)
                    {
                        Darken(pixels, ((cx + dx) % Size + Size) % Size, ((cy + dy) % Size + Size) % Size, 0.94 + d * 0.06);
                    }
                }
            }
        }

        var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, pixels, Size * 4);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, Size, Size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static byte Clamp(double v) => (byte)Math.Clamp((int)v, 0, 255);

    private static void Darken(byte[] pixels, int x, int y, double factor)
    {
        int i = (y * Size + x) * 4;
        pixels[i + 0] = Clamp(pixels[i + 0] * factor);
        pixels[i + 1] = Clamp(pixels[i + 1] * factor * 0.995);
        pixels[i + 2] = Clamp(pixels[i + 2] * factor);
    }
}
