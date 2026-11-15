using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HunchesAndHerrings.Audio;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Rendering;
using Rectangle = System.Windows.Shapes.Rectangle;
using ShapePath = System.Windows.Shapes.Path;

namespace HunchesAndHerrings.Controls;

/// <summary>One entry as the journal displays it.</summary>
/// <param name="Key">Journal key, used for circling and actions.</param>
/// <param name="Category">Filter category: Scene, Papers, Statements, Clashes or Sergeant.</param>
/// <param name="ActionLabel">Optional clickable link such as "scrutinise (−2)".</param>
internal sealed record JournalEntry(
    string Key,
    string Category,
    string Stamp,
    string Title,
    string Source,
    string Body,
    Brush Ink,
    IReadOnlyList<string> Badges,
    bool Circled,
    string? ActionLabel,
    bool ActionEnabled,
    IReadOnlyList<WordSpan>? Highlights = null);

/// <summary>Text for the journal's first page.</summary>
internal sealed record JournalTitle(string Title, string Subtitle, string Footer);

/// <summary>
/// The inspector's journal: a leather-bound book with ruled pages. New entries are written in by a quill,
/// line by line; pages turn with a flip. Click an entry's title to circle it in red.
/// </summary>
internal sealed class JournalView : Grid
{
    private const double BookWidth = 1200;
    private const double BookHeight = 760;
    private const double PageWidth = 550;
    private const double PageHeight = 680;
    private const double PageTop = 40;
    private const double LeftPageX = 45;
    private const double RightPageX = 605;
    private const double FirstRule = 74;
    private const double LineHeight = 26;
    private const double MarginX = 64;
    private const double TextWidth = PageWidth - MarginX - 28;
    private const int LinesPerPage = 21;
    private const double WriteSpeed = 330; // pixels per second

    private static readonly string[] Filters = { "All", "Circled", "Scene", "Papers", "Statements", "Clashes" };
    private static readonly Brush RuleBrush = Frozen(Color.FromArgb(0x55, 0x6A, 0x8E, 0xB8));
    private static readonly Brush MarginBrush = Frozen(Color.FromArgb(0x70, 0xB8, 0x3A, 0x3A));
    private static readonly Brush Gold = Frozen(Color.FromRgb(0xD9, 0xB8, 0x72));
    private static readonly Brush CircleInk = Frozen(Color.FromRgb(0xA3, 0x1F, 0x1F));

    private readonly Canvas _book = new() { Width = BookWidth, Height = BookHeight };
    private readonly Canvas _leftPage = NewPage(new Point(1, 0.5));
    private readonly Canvas _rightPage = NewPage(new Point(0, 0.5));
    private readonly ShapePath _pen;
    private readonly TextBlock _pageInfo;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal };
    private readonly Action<Sfx, double> _playSound;

    private readonly List<Layout> _layouts = new();
    private readonly Dictionary<(int Entry, int Stroke), Stroke> _strokes = new();
    private readonly Dictionary<int, List<UIElement>> _extras = new();
    private readonly HashSet<int> _pending = new();
    private readonly HashSet<(int Entry, int Stroke)> _done = new();
    private readonly Queue<Step> _queue = new();
    private readonly Stopwatch _clock = new();

    private IReadOnlyList<JournalEntry> _all = Array.Empty<JournalEntry>();
    private List<JournalEntry> _entries = new();
    private JournalTitle _title = new(string.Empty, string.Empty, string.Empty);
    private string _filter = "All";
    private int _spread;
    private int _pageCount = 2;
    private Step? _current;
    private int _strokeIndex;
    private double _progress;
    private double _pauseLeft;
    private bool _turning;
    private bool _hooked;
    private TimeSpan _last;
    private (int Entry, int Word)? _dragStart;
    private bool _eraser;
    private readonly StackPanel _tools = new() { Orientation = Orientation.Horizontal };

    public JournalView(Action<Sfx, double> playSound)
    {
        _playSound = playSound;
        Background = Frozen(Color.FromArgb(0xB0, 0x14, 0x0E, 0x08));
        MouseLeftButtonUp += OnBackdropClick;

        _pen = new ShapePath
        {
            Data = Geometry.Parse("M 0,0 L 5,-9 C 18,-40 48,-82 92,-112 C 72,-70 42,-34 11,-5 Z M 3,-5 L 86,-106"),
            Fill = Frozen(Color.FromRgb(0xF4, 0xEE, 0xE0)),
            Stroke = Frozen(Color.FromRgb(0x2B, 0x21, 0x18)),
            StrokeThickness = 1.3,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        _pageInfo = new TextBlock { FontFamily = Ui.Hand, FontSize = 15, Foreground = Gold, Width = 300, TextAlignment = TextAlignment.Center };
        BuildChrome();

        var viewbox = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(24), Child = _book };
        Children.Add(viewbox);
    }

    /// <summary>Raised when the reader closes the journal.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when an entry's title is clicked (to circle or uncircle it).</summary>
    public event Action<string>? CircleToggled;

    /// <summary>Raised when an entry's action link is clicked.</summary>
    public event Action<string>? ActionInvoked;

    /// <summary>
    /// Raised when words in an entry are ringed or rubbed out: entry key, first and last word index, and true to erase.
    /// </summary>
    public event Action<string, int, int, bool>? HighlightRequested;

    public bool IsAnimating => _current is not null || _queue.Count > 0 || _turning;

    /// <summary>Opens the journal. With <paramref name="writeKeys"/>, those entries are written in with the pen.</summary>
    public void Show(JournalTitle title, IReadOnlyList<JournalEntry> entries, IReadOnlyList<string>? writeKeys)
    {
        _title = title;
        _all = entries;
        StopAnimation();
        _pending.Clear();
        _done.Clear();

        if (writeKeys is { Count: > 0 })
        {
            _filter = "All";
            Relayout();
            var indices = writeKeys.Select(k => _entries.FindIndex(e => e.Key == k)).Where(i => i >= 0).Distinct().OrderBy(i => i).ToList();
            foreach (int i in indices)
            {
                _pending.Add(i);
            }

            if (indices.Count > 0)
            {
                _spread = SpreadOf(indices[0]);
                foreach (int i in indices)
                {
                    _queue.Enqueue(new TurnStep(SpreadOf(i)));
                    _queue.Enqueue(new WriteStep(i));
                    _queue.Enqueue(new PauseStep(0.25));
                }

                Render();
                StartAnimation();
                return;
            }
        }
        else
        {
            Relayout();
            _spread = Math.Max(0, SpreadCount - 1);
        }

        Render();
    }

    /// <summary>Updates the entries (e.g. after circling) without moving the page, unless writing is underway.</summary>
    public void Refresh(IReadOnlyList<JournalEntry> entries)
    {
        if (IsAnimating)
        {
            return;
        }

        _all = entries;
        Relayout();
        _spread = Math.Clamp(_spread, 0, Math.Max(0, SpreadCount - 1));
        Render();
    }

    /// <summary>Finishes any writing immediately.</summary>
    public void Skip()
    {
        if (!IsAnimating)
        {
            return;
        }

        int last = _pending.Count > 0 ? _pending.Max() : -1;
        _queue.Clear();
        _current = null;
        _pending.Clear();
        _done.Clear();
        StopAnimation();
        if (last >= 0)
        {
            _spread = SpreadOf(last);
        }

        ResetScales();
        Render();
    }

    // ================================================================== layout

    private int SpreadCount => Math.Max(1, (_pageCount + 1) / 2);

    private int SpreadOf(int entryIndex) =>
        entryIndex >= 0 && entryIndex < _layouts.Count ? _layouts[entryIndex].Page / 2 : 0;

    private void Relayout()
    {
        _entries = _all.Where(MatchesFilter).ToList();
        _layouts.Clear();
        double dip = PixelsPerDip();

        // Page 0 is the title page; entries start on page 1.
        int page = 1;
        int line = 0;
        foreach (JournalEntry entry in _entries)
        {
            var layout = new Layout(entry)
            {
                TitleWidth = Measure(entry.Title, Ui.Hand, 19, FontWeights.Bold, FontStyles.Normal, dip),
                SourceWidth = Measure(entry.Source, Ui.Body, 11.5, FontWeights.Normal, FontStyles.Italic, dip),
            };
            int word = 0;
            foreach (string text in Wrap(entry.Body, Ui.Hand, 16, TextWidth, dip))
            {
                layout.Body.Add((text, Measure(text, Ui.Hand, 16, FontWeights.Normal, FontStyles.Normal, dip)));
                layout.FirstWord.Add(word);
                word += text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            }

            layout.HasActionRow = entry.ActionLabel is not null || entry.Badges.Count > 0;
            if (line > 0 && line + layout.LineCount > LinesPerPage)
            {
                page++;
                line = 0;
            }

            layout.Page = page;
            layout.FirstLine = line;
            line += layout.LineCount;
            _layouts.Add(layout);
        }

        _pageCount = Math.Max(2, page + 1);
    }

    private bool MatchesFilter(JournalEntry e) => _filter switch
    {
        "All" => e.Category != "Points",
        "Circled" => e.Circled || (e.Highlights?.Count ?? 0) > 0,
        _ => e.Category == _filter,
    };

    private double PixelsPerDip()
    {
        try
        {
            return VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }
        catch (InvalidOperationException)
        {
            return 1.0;
        }
    }

    private static double Measure(string text, FontFamily family, double size, FontWeight weight, FontStyle style, double dip)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(family, style, weight, FontStretches.Normal), size, Brushes.Black, dip);
        return formatted.WidthIncludingTrailingWhitespace;
    }

    private static List<string> Wrap(string text, FontFamily family, double size, double width, double dip)
    {
        var lines = new List<string>();
        var current = string.Empty;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && Measure(candidate, family, size, FontWeights.Normal, FontStyles.Normal, dip) > width)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    // ================================================================== rendering

    private void Render()
    {
        _strokes.Clear();
        _extras.Clear();
        DrawPage(_leftPage, _spread * 2, isLeft: true);
        DrawPage(_rightPage, _spread * 2 + 1, isLeft: false);
        _pageInfo.Text = $"pages {_spread * 2 + 1}–{_spread * 2 + 2} of {_pageCount}  ·  {_entries.Count} entr{(_entries.Count == 1 ? "y" : "ies")}";
        RefreshTabs();
    }

    private void DrawPage(Canvas page, int pageIndex, bool isLeft)
    {
        page.Children.Clear();

        var paper = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        Color light = Color.FromRgb(0xF6, 0xEE, 0xD8), shade = Color.FromRgb(0xD8, 0xC8, 0xA4);
        paper.GradientStops.Add(new GradientStop(isLeft ? light : shade, 0));
        paper.GradientStops.Add(new GradientStop(light, isLeft ? 0.85 : 0.12));
        paper.GradientStops.Add(new GradientStop(isLeft ? shade : light, 1));
        page.Children.Add(new Rectangle { Width = PageWidth, Height = PageHeight, Fill = paper });
        page.Children.Add(new Rectangle { Width = PageWidth, Height = PageHeight, Fill = PaperTexture.Brush, Opacity = 0.35 });

        var rules = new GeometryGroup();
        for (int i = 0; i < LinesPerPage; i++)
        {
            double y = FirstRule + i * LineHeight;
            rules.Children.Add(new LineGeometry(new Point(18, y), new Point(PageWidth - 18, y)));
        }

        page.Children.Add(new ShapePath { Data = rules, Stroke = RuleBrush, StrokeThickness = 0.8 });
        page.Children.Add(new ShapePath { Data = new LineGeometry(new Point(MarginX - 10, 20), new Point(MarginX - 10, PageHeight - 20)), Stroke = MarginBrush, StrokeThickness = 1 });
        Place(page, new TextBlock { Text = $"— {pageIndex + 1} —", FontFamily = Ui.Body, FontSize = 12, Foreground = Ui.InkFaded, Width = PageWidth, TextAlignment = TextAlignment.Center }, 0, PageHeight - 32);

        if (pageIndex == 0)
        {
            DrawTitlePage(page);
            return;
        }

        double pageX = isLeft ? LeftPageX : RightPageX;
        for (int i = 0; i < _layouts.Count; i++)
        {
            if (_layouts[i].Page == pageIndex)
            {
                DrawEntry(page, i, pageX);
            }
        }

        if (_layouts.Count == 0 && pageIndex == 1)
        {
            Place(page, Hand(_filter == "All" ? "Nothing written yet." : $"No entries under \"{_filter}\".", 18, Ui.InkFaded), MarginX, FirstRule - 24);
        }
    }

    private void DrawTitlePage(Canvas page)
    {
        var panel = new StackPanel { Width = PageWidth - 100 };
        panel.Children.Add(new TextBlock { Text = "Journal of the Investigation", FontFamily = Ui.TitleFont, FontSize = 30, Foreground = Ui.Ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Border { Height = 1, Background = Ui.Ink, Margin = new Thickness(40, 14, 40, 14) });
        panel.Children.Add(new TextBlock { Text = _title.Title, FontFamily = Ui.Hand, FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Ui.BlueInk, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = _title.Subtitle, FontFamily = Ui.Hand, FontSize = 17, Foreground = Ui.BlueInk, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(new TextBlock { Text = _title.Footer, FontFamily = Ui.Body, FontSize = 13, FontStyle = FontStyles.Italic, Foreground = Ui.InkFaded, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 40, 0, 0) });
        panel.Children.Add(new TextBlock
        {
            Text = "Click an entry's title to circle it, or click a word (or drag across words) to ring just that. Rings that touch join up.\nRight-click, or pick the eraser, to rub rings out. Everything circled is gathered under the \"Circled\" tab.",
            FontFamily = Ui.Hand, FontSize = 14, Foreground = Ui.InkFaded, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 60, 0, 0),
        });
        Place(page, panel, 50, 120);
    }

    private void DrawEntry(Canvas page, int index, double pageX)
    {
        Layout l = _layouts[index];
        JournalEntry e = l.Entry;
        bool pending = _pending.Contains(index);
        double Rule(int line) => FirstRule + (l.FirstLine + line) * LineHeight;
        var extras = new List<UIElement>();

        // Margin time-stamp
        var stamp = new TextBlock { Text = e.Stamp, FontFamily = Ui.Body, FontSize = 10, Foreground = Ui.InkFaded, Width = MarginX - 16, TextAlignment = TextAlignment.Right };
        Place(page, stamp, 2, Rule(0) - 15);

        // Title (click to circle)
        var title = new TextBlock { Text = e.Title, FontFamily = Ui.Hand, FontSize = 19, FontWeight = FontWeights.Bold, Foreground = e.Ink, Cursor = Cursors.Hand, ToolTip = e.Circled ? "Click to remove the circle" : "Click to circle this entry" };
        title.MouseLeftButtonUp += (_, args) =>
        {
            if (!IsAnimating)
            {
                CircleToggled?.Invoke(e.Key);
                args.Handled = true;
            }
        };
        AddStroke(page, index, 0, title, MarginX, Rule(0) - 26, l.TitleWidth, pageX, Rule(0) - 3, pending);

        // Source line
        var source = new TextBlock { Text = e.Source, FontFamily = Ui.Body, FontSize = 11.5, FontStyle = FontStyles.Italic, Foreground = Ui.InkFaded };
        AddStroke(page, index, 1, source, MarginX, Rule(1) - 17, l.SourceWidth, pageX, Rule(1) - 3, pending);

        // Body: click a word, or drag across words (even onto the next line), to ring them.
        // With the eraser, or with a right-click, the same gestures rub rings out.
        double dip = PixelsPerDip();
        for (int k = 0; k < l.Body.Count; k++)
        {
            string text = l.Body[k].Text;
            string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int firstWord = l.FirstWord[k];
            var line = new TextBlock
            {
                Text = text,
                FontFamily = Ui.Hand,
                FontSize = 16,
                Foreground = e.Ink,
                Background = Brushes.Transparent,
                Cursor = _eraser ? Cursors.Cross : Cursors.IBeam,
                ToolTip = _eraser ? "Click or drag to rub out rings" : "Click a word, or drag across words, to ring them. Right-click to rub out.",
            };
            int WordUnder(MouseEventArgs args) => firstWord + WordAt(text, args.GetPosition(line).X, dip);
            line.MouseLeftButtonDown += (_, args) =>
            {
                if (!IsAnimating)
                {
                    _dragStart = (index, WordUnder(args));
                    args.Handled = true;
                }
            };
            line.MouseLeftButtonUp += (_, args) =>
            {
                if (IsAnimating || _dragStart is not { } start || start.Entry != index)
                {
                    _dragStart = null;
                    return;
                }

                _dragStart = null;
                HighlightRequested?.Invoke(e.Key, start.Word, WordUnder(args), _eraser);
                args.Handled = true;
            };
            line.MouseRightButtonUp += (_, args) =>
            {
                if (!IsAnimating)
                {
                    int w = WordUnder(args);
                    HighlightRequested?.Invoke(e.Key, w, w, true);
                    args.Handled = true;
                }
            };
            AddStroke(page, index, 2 + k, line, MarginX, Rule(2 + k) - 22, l.Body[k].Width, pageX, Rule(2 + k) - 4, pending);

            // Red rings round the highlighted part of this line.
            int lastWord = firstWord + words.Length - 1;
            foreach (WordSpan span in e.Highlights ?? Array.Empty<WordSpan>())
            {
                int from = Math.Max(span.First, firstWord);
                int to = Math.Min(span.Last, lastWord);
                if (from > to)
                {
                    continue;
                }

                string before = string.Join(' ', words.Take(from - firstWord));
                string ringed = string.Join(' ', words[(from - firstWord)..(to - firstWord + 1)]);
                double x = before.Length == 0 ? 0 : Measure(before + " ", Ui.Hand, 16, FontWeights.Normal, FontStyles.Normal, dip);
                double w = Measure(ringed, Ui.Hand, 16, FontWeights.Normal, FontStyles.Normal, dip);
                var loop = new ShapePath
                {
                    Data = InkLoop(new Rect(MarginX + x - 5, Rule(2 + k) - 25, w + 10, 26), new Random(HashCode.Combine(e.Key, span.First, k))),
                    Stroke = CircleInk,
                    StrokeThickness = 1.8,
                    IsHitTestVisible = false,
                };
                page.Children.Add(loop);
                extras.Add(loop);
            }
        }

        // Badges and action link
        if (l.HasActionRow)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (string badge in e.Badges)
            {
                row.Children.Add(new TextBlock { Text = $"[{badge}]  ", FontFamily = Ui.Hand, FontSize = 13, Foreground = badge.Contains("false", StringComparison.OrdinalIgnoreCase) || badge.Contains("clash", StringComparison.OrdinalIgnoreCase) ? CircleInk : Ui.InkFaded });
            }

            if (e.ActionLabel is not null)
            {
                var link = new TextBlock
                {
                    Text = e.ActionLabel,
                    FontFamily = Ui.Hand,
                    FontSize = 13,
                    Foreground = e.ActionEnabled ? Ui.BlueInk : Ui.InkFaded,
                    TextDecorations = e.ActionEnabled ? TextDecorations.Underline : null,
                    Cursor = e.ActionEnabled ? Cursors.Hand : Cursors.Arrow,
                };
                if (e.ActionEnabled)
                {
                    link.MouseLeftButtonUp += (_, args) =>
                    {
                        if (!IsAnimating)
                        {
                            ActionInvoked?.Invoke(e.Key);
                            args.Handled = true;
                        }
                    };
                }

                row.Children.Add(link);
            }

            Place(page, row, MarginX, Rule(2 + l.Body.Count) - 20);
            extras.Add(row);
        }

        // The red circle round a circled entry
        if (e.Circled)
        {
            var circle = new ShapePath
            {
                Data = InkLoop(new Rect(MarginX - 12, Rule(0) - 30, l.TitleWidth + 24, 34), new Random(e.Key.GetHashCode())),
                Stroke = CircleInk,
                StrokeThickness = 2.2,
                IsHitTestVisible = false,
            };
            page.Children.Add(circle);
            extras.Add(circle);
        }

        extras.Add(stamp);
        _extras[index] = extras;
        if (pending)
        {
            foreach (UIElement x in extras)
            {
                x.Visibility = Visibility.Hidden;
            }
        }
    }

    private void AddStroke(Canvas page, int entry, int strokeIndex, TextBlock block, double x, double y, double width, double pageX, double baseline, bool pending)
    {
        Place(page, block, x, y);
        if (!pending || _done.Contains((entry, strokeIndex)))
        {
            return;
        }

        var clip = new RectangleGeometry(new Rect(0, -10, 0, 80));
        block.Clip = clip;
        _strokes[(entry, strokeIndex)] = new Stroke(block, clip, new Point(pageX + x, PageTop + baseline), Math.Max(4, width));
    }

    /// <summary>The index (within the line) of the word under a horizontal offset into a line of journal text.</summary>
    private static int WordAt(string text, double x, double dip)
    {
        int ch = Math.Max(0, text.Length - 1);
        for (int i = 1; i <= text.Length; i++)
        {
            if (Measure(text[..i], Ui.Hand, 16, FontWeights.Normal, FontStyles.Normal, dip) > x)
            {
                ch = i - 1;
                break;
            }
        }

        // Count the gaps before the character; a click on a space belongs to the word after it.
        return text[..ch].Count(c => c == ' ');
    }

    /// <summary>A loose hand-drawn loop around a rectangle, overshooting where the pen started.</summary>
    private static Geometry InkLoop(Rect r, Random rng)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            double start = -2.4 + rng.NextDouble() * 0.4;
            bool first = true;
            for (double t = 0; t <= Math.PI * 2.25; t += 0.12)
            {
                double wobble = 1 + (rng.NextDouble() - 0.5) * 0.05;
                double grow = 1 + t * 0.012;
                var p = new Point(cx + Math.Cos(start + t) * r.Width / 2 * wobble * grow, cy + Math.Sin(start + t) * r.Height / 2 * wobble * grow);
                if (first)
                {
                    ctx.BeginFigure(p, false, false);
                    first = false;
                }
                else
                {
                    ctx.LineTo(p, true, true);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    // ================================================================== chrome

    private void BuildChrome()
    {
        // Leather cover with stitching
        var leather = new LinearGradientBrush(Color.FromRgb(0x6A, 0x3E, 0x24), Color.FromRgb(0x3A, 0x21, 0x13), 45);
        Place(_book, new Border { Width = BookWidth, Height = BookHeight, CornerRadius = new CornerRadius(20), Background = leather }, 0, 0);
        Place(_book, new Rectangle { Width = BookWidth - 28, Height = BookHeight - 28, RadiusX = 12, RadiusY = 12, Stroke = Gold, StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 3 }, Opacity = 0.6 }, 14, 14);

        // Page block edges for depth
        for (int i = 3; i >= 1; i--)
        {
            var edge = Frozen(Color.FromRgb((byte)(0xE0 - i * 12), (byte)(0xD2 - i * 12), (byte)(0xB0 - i * 12)));
            Place(_book, new Rectangle { Width = PageWidth, Height = PageHeight, Fill = edge }, LeftPageX - i * 3, PageTop + i * 2);
            Place(_book, new Rectangle { Width = PageWidth, Height = PageHeight, Fill = edge }, RightPageX + i * 3, PageTop + i * 2);
        }

        Place(_book, _leftPage, LeftPageX, PageTop);
        Place(_book, _rightPage, RightPageX, PageTop);

        // Spine shadow
        var spine = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        spine.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 0));
        spine.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 0x20, 0x10, 0x05), 0.5));
        spine.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1));
        Place(_book, new Rectangle { Width = 50, Height = PageHeight, Fill = spine, IsHitTestVisible = false }, (LeftPageX + PageWidth + RightPageX) / 2 - 25, PageTop);

        // Ribbon bookmark
        Place(_book, new ShapePath { Data = Geometry.Parse("M 0,0 L 18,0 L 18,120 L 9,108 L 0,120 Z"), Fill = Frozen(Color.FromRgb(0x8B, 0x1E, 0x1E)), IsHitTestVisible = false, Opacity = 0.9 }, 1040, PageTop - 6);

        // Pen or eraser, filter tabs and close
        Place(_book, _tools, LeftPageX + 10, 6);
        RefreshTools();
        Place(_book, _tabs, RightPageX + 10, 6);
        var close = Tab("✕ close", true, () =>
        {
            Skip();
            CloseRequested?.Invoke();
        });
        Place(_book, close, BookWidth - 120, 6);

        // Navigation
        Place(_book, Link("◀ previous", () => Turn(-1)), LeftPageX + 10, BookHeight - 34);
        Place(_book, Link("next ▶", () => Turn(1)), RightPageX + PageWidth - 70, BookHeight - 34);
        Place(_book, _pageInfo, BookWidth / 2 - 150, BookHeight - 34);

        _book.MouseLeftButtonDown += (_, e) =>
        {
            if (IsAnimating)
            {
                Skip();
                e.Handled = true;
            }
        };

        Place(_book, _pen, 0, 0);
    }

    private void RefreshTools()
    {
        _tools.Children.Clear();
        _tools.Children.Add(Tab("◯ ring", !_eraser, () => SetEraser(false)));
        _tools.Children.Add(Tab("⌫ eraser", _eraser, () => SetEraser(true)));
    }

    private void SetEraser(bool on)
    {
        if (_eraser == on || IsAnimating)
        {
            return;
        }

        _eraser = on;
        _playSound(Sfx.Quill, 0.25);
        RefreshTools();
        Render();
    }

    private void RefreshTabs()
    {
        _tabs.Children.Clear();
        foreach (string filter in Filters)
        {
            string f = filter;
            _tabs.Children.Add(Tab(filter, filter == _filter, () =>
            {
                if (IsAnimating || _filter == f)
                {
                    return;
                }

                _filter = f;
                Relayout();
                _spread = 0;
                _playSound(Sfx.Page, 0.4);
                Render();
            }));
        }
    }

    private static Border Tab(string text, bool selected, Action onClick)
    {
        var tab = new Border
        {
            Background = selected ? Frozen(Color.FromRgb(0xF6, 0xEE, 0xD8)) : Frozen(Color.FromRgb(0xC9, 0xB4, 0x88)),
            BorderBrush = Ui.Ink,
            BorderThickness = new Thickness(1, 1, 1, 0),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(10, 2, 10, 4),
            Margin = new Thickness(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = text, FontFamily = Ui.Hand, FontSize = 15, Foreground = Ui.Ink, FontWeight = selected ? FontWeights.Bold : FontWeights.Normal },
        };
        tab.MouseLeftButtonUp += (_, e) =>
        {
            onClick();
            e.Handled = true;
        };
        return tab;
    }

    private static TextBlock Link(string text, Action onClick)
    {
        var link = new TextBlock { Text = text, FontFamily = Ui.Hand, FontSize = 17, Foreground = Gold, Cursor = Cursors.Hand };
        link.MouseLeftButtonUp += (_, e) =>
        {
            onClick();
            e.Handled = true;
        };
        return link;
    }

    private void OnBackdropClick(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, this))
        {
            return;
        }

        if (IsAnimating)
        {
            Skip();
        }
        else
        {
            CloseRequested?.Invoke();
        }
    }

    // ================================================================== page turning

    private void Turn(int delta)
    {
        if (IsAnimating)
        {
            return;
        }

        int target = Math.Clamp(_spread + delta, 0, SpreadCount - 1);
        if (target != _spread)
        {
            TurnTo(target, () => { });
        }
    }

    private void TurnTo(int target, Action done)
    {
        _turning = true;
        bool forward = target > _spread;
        Canvas outgoing = forward ? _rightPage : _leftPage;
        Canvas incoming = forward ? _leftPage : _rightPage;
        var outScale = (ScaleTransform)outgoing.RenderTransform;
        var inScale = (ScaleTransform)incoming.RenderTransform;
        _playSound(Sfx.Page, 0.45);

        Animate(outScale, 1, 0, () =>
        {
            outScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            outScale.ScaleX = 1;
            _spread = target;
            Render();
            Animate(inScale, 0, 1, () =>
            {
                inScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                inScale.ScaleX = 1;
                _turning = false;
                done();
            });
        });
    }

    private static void Animate(ScaleTransform transform, double from, double to, Action done)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(170))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        transform.ScaleX = to;
        animation.Completed += (_, _) => done();
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
    }

    private void ResetScales()
    {
        foreach (Canvas page in new[] { _leftPage, _rightPage })
        {
            var scale = (ScaleTransform)page.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.ScaleX = 1;
        }

        _turning = false;
    }

    // ================================================================== writing animation

    private void StartAnimation()
    {
        if (!_hooked)
        {
            CompositionTarget.Rendering += OnFrame;
            _hooked = true;
        }

        _clock.Restart();
        _last = TimeSpan.Zero;
    }

    private void StopAnimation()
    {
        if (_hooked)
        {
            CompositionTarget.Rendering -= OnFrame;
            _hooked = false;
        }

        _queue.Clear();
        _current = null;
        _pen.Visibility = Visibility.Collapsed;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        TimeSpan now = _clock.Elapsed;
        double dt = Math.Min(0.05, (now - _last).TotalSeconds);
        _last = now;

        if (_turning)
        {
            return;
        }

        if (_current is null)
        {
            if (!_queue.TryDequeue(out Step? next))
            {
                StopAnimation();
                Render();
                return;
            }

            Begin(next);
            return;
        }

        switch (_current)
        {
            case WriteStep write:
                if (!_strokes.TryGetValue((write.Entry, _strokeIndex), out Stroke? stroke))
                {
                    CompleteEntry(write.Entry);
                    _current = null;
                    return;
                }

                int waiting = _queue.Count(s => s is WriteStep);
                double speed = WriteSpeed * (waiting > 2 ? 2.4 : 1) * (_strokeIndex == 1 ? 3 : 1);
                _progress += speed * dt;
                double revealed = Math.Min(_progress, stroke.Width);
                stroke.Clip.Rect = new Rect(0, -10, revealed, 80);
                double bob = Math.Sin(now.TotalSeconds * 40) * 1.5;
                Canvas.SetLeft(_pen, stroke.PenOrigin.X + revealed);
                Canvas.SetTop(_pen, stroke.PenOrigin.Y + bob);

                if (_progress >= stroke.Width)
                {
                    stroke.Block.Clip = null;
                    _done.Add((write.Entry, _strokeIndex));
                    _strokeIndex++;
                    _progress = 0;
                }

                break;

            case PauseStep:
                _pauseLeft -= dt;
                if (_pauseLeft <= 0)
                {
                    _current = null;
                }

                break;
        }
    }

    private void Begin(Step step)
    {
        switch (step)
        {
            case TurnStep turn when turn.Spread != _spread:
                _pen.Visibility = Visibility.Collapsed;
                TurnTo(turn.Spread, () => { });
                break;
            case WriteStep write:
                _current = write;
                _strokeIndex = 0;
                _progress = 0;
                _pen.Visibility = Visibility.Visible;
                _playSound(Sfx.Quill, 0.45);
                if (_extras.TryGetValue(write.Entry, out List<UIElement>? extras))
                {
                    // Show the margin stamp as soon as the pen starts.
                    extras[^1].Visibility = Visibility.Visible;
                }

                break;
            case PauseStep pause:
                _current = pause;
                _pauseLeft = pause.Seconds;
                break;
        }
    }

    private void CompleteEntry(int entry)
    {
        _pending.Remove(entry);
        if (_extras.TryGetValue(entry, out List<UIElement>? extras))
        {
            foreach (UIElement x in extras)
            {
                x.Visibility = Visibility.Visible;
            }
        }
    }

    // ================================================================== helpers

    private static Canvas NewPage(Point origin) => new()
    {
        Width = PageWidth,
        Height = PageHeight,
        ClipToBounds = true,
        RenderTransform = new ScaleTransform(1, 1),
        RenderTransformOrigin = origin,
    };

    private static TextBlock Hand(string text, double size, Brush brush) =>
        new() { Text = text, FontFamily = Ui.Hand, FontSize = size, Foreground = brush };

    private static void Place(Canvas canvas, UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed class Layout
    {
        public Layout(JournalEntry entry) => Entry = entry;

        public JournalEntry Entry { get; }
        public int Page { get; set; }
        public int FirstLine { get; set; }
        public double TitleWidth { get; set; }
        public double SourceWidth { get; set; }
        public List<(string Text, double Width)> Body { get; } = new();

        /// <summary>Index (into the entry's words) of the first word on each body line.</summary>
        public List<int> FirstWord { get; } = new();
        public bool HasActionRow { get; set; }
        public int LineCount => 2 + Body.Count + (HasActionRow ? 1 : 0) + 1;
    }

    private sealed record Stroke(TextBlock Block, RectangleGeometry Clip, Point PenOrigin, double Width);

    private abstract record Step;

    private sealed record TurnStep(int Spread) : Step;

    private sealed record WriteStep(int Entry) : Step;

    private sealed record PauseStep(double Seconds) : Step;
}
