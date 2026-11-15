using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Rendering;

namespace HunchesAndHerrings.Controls;

/// <summary>
/// A breakout window with a card for everyone in the case: where they've been placed last night, every trait and motive
/// you've heard of, any secret, and how many clashes involve them. It stays open beside the game and refreshes as you go.
/// </summary>
internal sealed class DossierWindow : Window
{
    private readonly WrapPanel _cards = new() { Margin = new Thickness(12) };
    private readonly WrapPanel _evidence = new() { Margin = new Thickness(12, 8, 12, 0) };
    private readonly CheckBox _hideStruck = new() { Content = "Hide struck-off suspects", Margin = new Thickness(12, 6, 0, 0) };
    private GameSession? _game;

    public DossierWindow()
    {
        Title = "The dossier";
        Width = 980;
        Height = 760;
        MinWidth = 420;
        MinHeight = 300;
        Background = Ui.Paper;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.WorkArea.Right - Width - 20;
        Top = SystemParameters.WorkArea.Top + 40;

        _hideStruck.Checked += (_, _) => Refresh(_game);
        _hideStruck.Unchecked += (_, _) => Refresh(_game);

        var root = new DockPanel();
        var header = new StackPanel();
        header.Children.Add(Ui.Title("The dossier", 30));
        header.Children.Add(_evidence);
        header.Children.Add(_hideStruck);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    public void Refresh(GameSession? game)
    {
        _game = game;
        _cards.Children.Clear();
        _evidence.Children.Clear();
        if (game is null)
        {
            return;
        }

        CaseFile c = game.Case;
        _evidence.Children.Add(Ui.Handwriting($"{c.Title}  ·  ", 15, weight: FontWeights.Bold));
        var indicators = SuspectSummary.Indicators(game);
        if (indicators.Count == 0)
        {
            _evidence.Children.Add(Ui.Text("No evidence from the scene yet.", 12, Ui.InkFaded, italic: true));
        }

        foreach (KnownIndicator i in indicators)
        {
            var text = Ui.Text(i.ExplainedBy is null ? $"• {i.Label}  " : $"• {i.Label} (explained: {i.ExplainedBy})  ", 12.5, i.ExplainedBy is null ? Ui.RedInk : Ui.InkFaded);
            if (i.ExplainedBy is not null)
            {
                text.TextDecorations = TextDecorations.Strikethrough;
            }

            _evidence.Children.Add(text);
        }

        foreach (Character s in c.Suspects.Where(s => !(_hideStruck.IsChecked == true && game.Struck.Contains(s.Id))))
        {
            _cards.Children.Add(Card(game, SuspectSummary.For(game, s)));
        }

        var others = c.Children.Concat(c.Rival is { } r ? new[] { r.Detective } : Array.Empty<Character>()).ToList();
        if (others.Count > 0)
        {
            var label = Ui.Handwriting("Also in the house (not suspects)", 16, weight: FontWeights.Bold);
            label.Width = 900;
            label.Margin = new Thickness(0, 8, 0, 4);
            _cards.Children.Add(label);
            foreach (Character o in others)
            {
                _cards.Children.Add(Card(game, SuspectSummary.For(game, o)));
            }
        }
    }

    private static UIElement Card(GameSession game, SuspectSummary sum)
    {
        CaseFile c = game.Case;
        Character s = sum.Person;
        bool struck = game.Struck.Contains(s.Id);
        bool highlight = game.Settings.HighlightExclusions;

        var body = new StackPanel { Width = 280 };
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var portrait = PortraitRenderer.Create(s, 64);
        portrait.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(portrait, Dock.Left);
        head.Children.Add(portrait);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = Ui.Handwriting(s.ShortName, 17, weight: FontWeights.Bold);
        if (struck)
        {
            name.TextDecorations = TextDecorations.Strikethrough;
        }

        names.Children.Add(name);
        names.Children.Add(Ui.Text(s.RoleLabel, 11.5, Ui.InkFaded, italic: true));
        var tags = new List<string>();
        if (s.IsVisitor)
        {
            tags.Add("⌂ calls at the house");
        }

        if (game.Focused.Contains(s.Id))
        {
            tags.Add("◉ watched");
        }

        if (struck)
        {
            tags.Add("struck off");
        }

        if (tags.Count > 0)
        {
            names.Children.Add(Ui.Text(string.Join("  ·  ", tags), 11, Ui.BlueInk));
        }

        head.Children.Add(names);
        body.Children.Add(head);

        if (!s.IsRival && !s.IsYoungChild)
        {
            body.Children.Add(Section("Last night"));
            for (int slot = 0; slot < NightSlots.Count; slot++)
            {
                var places = sum.Night[slot];
                string where = places.Count == 0 ? "—" : string.Join(" / ", places.Select(p => $"{c.Manor.NameOf(p.RoomId)} ({p.Sources})"));
                bool tod = slot == c.MurderSlot && (game.Settings.RevealTimeOfDeath || game.DiscoveredClues.Any(x => x.Facts.OfType<TimeOfDeathFact>().Any()));
                var line = Ui.Text($"{NightSlots.Short(slot)}: {where}{(tod ? "   ← time of death" : string.Empty)}", 12, places.Count > 1 ? Ui.RedInk : Ui.Ink, tod ? FontWeights.Bold : null);
                body.Children.Add(line);
            }
        }

        if (sum.Traits.Count > 0)
        {
            body.Children.Add(Section("Known details"));
            foreach (KnownTrait t in sum.Traits)
            {
                bool cross = highlight && t.Excludes;
                string mark = cross ? "✗ " : t.AtScene ? "• " : "  ";
                body.Children.Add(Ui.Text($"{mark}{t.Label}: {t.ValueText}", 12,
                    cross ? Ui.RedInk : t.AtScene ? Ui.Ink : Ui.InkFaded, t.AtScene ? FontWeights.SemiBold : null, italic: t.Values.Count > 1));
            }
        }

        if (sum.Motives.Count > 0)
        {
            body.Children.Add(Section("Motive"));
            foreach (string m in sum.Motives)
            {
                body.Children.Add(Ui.Text(m, 12, Ui.BlueInk));
            }
        }

        if (sum.Secrets.Count > 0)
        {
            body.Children.Add(Section("Secrets"));
            foreach (string secret in sum.Secrets)
            {
                body.Children.Add(Ui.Text(secret, 12, Ui.BlueInk));
            }
        }

        string seen = game.LastSeen.TryGetValue(s.Id, out Sighting? sighting)
            ? $"Last seen: {c.Manor.NameOf(sighting.RoomId)}, {Clock.Format(sighting.Minute)}"
            : "Not yet seen today";
        body.Children.Add(Ui.Text($"{seen}  ·  {sum.Statements} statements  ·  {sum.Clashes} clashes", 11, Ui.InkFaded, italic: true));

        var card = Ui.Card(body, sum.Clashes > 0 ? Ui.RedInk : Ui.InkFaded, new Thickness(0, 0, 10, 10));
        card.Opacity = struck ? 0.55 : 1;
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private static TextBlock Section(string title)
    {
        var text = Ui.Handwriting(title, 14, weight: FontWeights.Bold);
        text.Margin = new Thickness(0, 6, 0, 0);
        return text;
    }
}
