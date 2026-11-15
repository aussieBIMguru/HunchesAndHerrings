using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace HunchesAndHerrings.Controls;

/// <summary>
/// Finds the usable area (excluding the taskbar) of the monitor the game is launched on, in WPF device-independent
/// units. <see cref="SystemParameters.WorkArea"/> only ever reports the primary monitor, which puts the window in
/// the wrong place on multi-monitor setups.
/// </summary>
internal static class ScreenFit
{
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>Work area of the monitor under the mouse cursor (where the user just launched from).</summary>
    /// <param name="window">A window whose handle already exists (call from SourceInitialized or later).</param>
    public static Rect WorkAreaAtCursor(Window window)
    {
        Rect fallback = SystemParameters.WorkArea;

        if (!GetCursorPos(out NativePoint cursor))
        {
            return fallback;
        }

        IntPtr monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return fallback;
        }

        // Device pixels -> DIPs using the window's DPI.
        Matrix toDip = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        Point topLeft = toDip.Transform(new Point(info.Work.Left, info.Work.Top));
        Point bottomRight = toDip.Transform(new Point(info.Work.Right, info.Work.Bottom));
        var rect = new Rect(topLeft, bottomRight);

        return rect.Width > 0 && rect.Height > 0 ? rect : fallback;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
