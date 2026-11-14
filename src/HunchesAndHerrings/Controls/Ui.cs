using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HunchesAndHerrings.Controls;

/// <summary>Small factory helpers for building the ink-and-paper UI in code.</summary>
internal static class Ui
{
    public static Brush Ink => Res<Brush>("InkBrush", Brushes.Black);
    public static Brush InkFaded => Res<Brush>("InkFadedBrush", Brushes.Gray);
    public static Brush RedInk => Res<Brush>("RedInkBrush", Brushes.DarkRed);
    public static Brush BlueInk => Res<Brush>("BlueInkBrush", Brushes.DarkBlue);
    public static Brush Paper => Res<Brush>("PaperBrush", Brushes.Beige);
    public static Brush PaperDark => Res<Brush>("PaperDarkBrush", Brushes.Tan);
    public static FontFamily Hand => Res<FontFamily>("HandFont", new FontFamily("Segoe Print"));
    public static FontFamily Body => Res<FontFamily>("BodyFont", new FontFamily("Georgia"));
    public static FontFamily TitleFont => Res<FontFamily>("TitleFont", new FontFamily("Georgia"));

    private static T Res<T>(string key, T fallback) where T : class =>
        Application.Current?.TryFindResource(key) as T ?? fallback;

    /// <summary>A wrapped paragraph in the body (typeset) face.</summary>
    public static TextBlock Text(string text, double size = 14, Brush? brush = null, FontWeight? weight = null, bool italic = false)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = Body,
            FontSize = size,
            Foreground = brush ?? Ink,
            FontWeight = weight ?? FontWeights.Normal,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    /// <summary>A line in the handwriting face.</summary>
    public static TextBlock Handwriting(string text, double size = 16, Brush? brush = null, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = Hand,
            FontSize = size,
            Foreground = brush ?? Ink,
            FontWeight = weight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    public static TextBlock Title(string text, double size = 40)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = TitleFont,
            FontSize = size,
            Foreground = Ink,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    public static System.Windows.Controls.Button Button(string label, Action onClick, string? tooltip = null, bool enabled = true)
    {
        var button = new System.Windows.Controls.Button { Content = label, ToolTip = tooltip, IsEnabled = enabled };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A bordered note-card, like a slip of paper pinned in the notebook.</summary>
    public static Border Card(UIElement child, Brush? border = null, Thickness? margin = null)
    {
        return new Border
        {
            Child = child,
            BorderBrush = border ?? InkFaded,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = margin ?? new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(2),
        };
    }

    /// <summary>A thin horizontal ink rule.</summary>
    public static Border Rule(double opacity = 0.5) => new()
    {
        Height = 1,
        Background = Ink,
        Opacity = opacity,
        Margin = new Thickness(0, 6, 0, 6),
    };

    public static StackPanel Stack(Orientation orientation = Orientation.Vertical, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = orientation };
        foreach (UIElement child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }
}
