using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HunchesAndHerrings.Audio;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Generation;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Rendering;

namespace HunchesAndHerrings;

/// <summary>The notebook: clues, the suspect matrix, contradictions, schedules and notes.</summary>
public partial class MainWindow
{
    private DossierWindow? _dossier;

    private void RefreshNotebook()
    {
        RefreshSuspects();
        RefreshContradictions();
        RefreshSchedule();

        if (_game is not null && NotesBox.Text != _game.Notes)
        {
            _suppressNotes = true;
            NotesBox.Text = _game.Notes;
            _suppressNotes = false;
        }
    }

    // ================================================================== suspect matrix

    private void RefreshSuspects()
    {
        SuspectsPanel.Children.Clear();
        _dossier?.Refresh(_game);
        if (_game is null)
        {
            return;
        }

        CaseFile c = _game.Case;
        bool todKnown = _game.Settings.RevealTimeOfDeath || _game.DiscoveredClues.Any(x => x.Facts.OfType<TimeOfDeathFact>().Any());
        bool highlight = _game.Settings.HighlightExclusions;

        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var open = Ui.Button("Open the dossier ↗", OpenDossier, "A separate window with everything known about everyone. Keep it open beside the game.");
        DockPanel.SetDock(open, Dock.Right);
        top.Children.Add(open);
        top.Children.Add(Ui.Text(
            $"◯ watches a suspect (up to {GameSession.MaxFocus}); click a name to strike it off. All of this comes from what you've found, and some of it may be lies.",
            12, Ui.InkFaded, italic: true));
        SuspectsPanel.Children.Add(top);

        // The scene indicators found so far.
        var indicators = SuspectSummary.Indicators(_game);
        var evidence = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        evidence.Children.Add(Ui.Handwriting("Evidence: ", 14, weight: FontWeights.Bold));
        if (indicators.Count == 0)
        {
            evidence.Children.Add(Ui.Text("nothing from the scene yet.", 12, Ui.InkFaded, italic: true));
        }

        foreach (KnownIndicator i in indicators)
        {
            evidence.Children.Add(Chip(i.Label, i.ExplainedBy is null ? Ui.RedInk : Ui.InkFaded, i.ExplainedBy is not null,
                i.ExplainedBy is null ? "Found at or near the scene." : $"Explained away: {i.ExplainedBy}."));
        }

        if (todKnown)
        {
            evidence.Children.Add(Chip($"Died about {NightSlots.Short(c.MurderSlot)}", Ui.Ink, false, "The time of death."));
        }

        SuspectsPanel.Children.Add(evidence);

        foreach (Character s in c.Suspects)
        {
            SuspectsPanel.Children.Add(SuspectRow(SuspectSummary.For(_game, s), todKnown, highlight));
        }

        SuspectsPanel.Children.Add(Ui.Text(
            "Numbers in brackets count separate sources. A slash means accounts disagree: someone is lying, or mistaken. ✗ marks a trait that doesn't fit the evidence.",
            11, Ui.InkFaded, italic: true));
    }

    private UIElement SuspectRow(SuspectSummary sum, bool todKnown, bool highlight)
    {
        CaseFile c = _game!.Case;
        Character s = sum.Person;
        string id = s.Id;
        bool struck = _game.Struck.Contains(id);
        bool watched = _game.Focused.Contains(id);

        var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2), Opacity = struck ? 0.45 : 1 };

        var focus = new TextBlock
        {
            Text = watched ? "◉" : "◯",
            FontSize = 18,
            Foreground = watched ? Brushes.DarkGoldenrod : Ui.InkFaded,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = watched ? "Stop watching" : "Keep an eye on this suspect",
        };
        focus.MouseLeftButtonUp += (_, _) => ToggleFocus(id);
        DockPanel.SetDock(focus, Dock.Left);
        row.Children.Add(focus);

        var who = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, ToolTip = "Click to strike off / restore", Width = 150, VerticalAlignment = VerticalAlignment.Top };
        who.Children.Add(PortraitRenderer.Create(s, 30));
        who.Children.Add(new TextBlock
        {
            Text = s.IsVisitor ? $"{s.ShortName} ⌂" : s.ShortName,
            FontFamily = Ui.Hand,
            FontSize = 14,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ui.Ink,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 112,
            TextDecorations = struck ? TextDecorations.Strikethrough : null,
        });
        who.MouseLeftButtonUp += (_, _) =>
        {
            _game?.ToggleStrike(id);
            _sound.Play(Sfx.Quill, 0.3);
            RefreshSuspects();
            RefreshRoom();
        };
        DockPanel.SetDock(who, Dock.Left);
        row.Children.Add(who);

        var facts = new WrapPanel();
        if (todKnown)
        {
            var places = sum.Night[c.MurderSlot];
            string where = places.Count == 0 ? "?" : string.Join(" / ", places.Select(p => $"{ShortRoom(c, p.RoomId)} ({p.Sources})"));
            facts.Children.Add(Chip($"{NightSlots.Short(c.MurderSlot)}: {where}", Ui.Ink, places.Count > 1, "Where they were said to be at the time of death."));
        }

        foreach (KnownTrait t in sum.Traits.Where(t => t.AtScene))
        {
            bool cross = highlight && t.Excludes;
            facts.Children.Add(Chip($"{(cross ? "✗ " : string.Empty)}{t.Label}: {t.ValueText}", cross ? Ui.RedInk : Ui.Ink, t.Values.Count > 1, null));
        }

        foreach (string motive in sum.Motives)
        {
            facts.Children.Add(Chip($"Motive: {motive}", Ui.BlueInk, false, null));
        }

        if (sum.Secrets.Count > 0)
        {
            facts.Children.Add(Chip("Has a secret", Ui.BlueInk, false, string.Join("\n", sum.Secrets)));
        }

        int other = sum.Traits.Count(t => !t.AtScene);
        if (other > 0)
        {
            facts.Children.Add(Chip($"+{other} other details", Ui.InkFaded, false,
                string.Join("\n", sum.Traits.Where(t => !t.AtScene).Select(t => $"{t.Label}: {t.ValueText}"))));
        }

        if (facts.Children.Count == 0)
        {
            facts.Children.Add(Ui.Text("nothing known yet", 11, Ui.InkFaded, italic: true));
        }

        row.Children.Add(facts);
        return row;
    }

    /// <summary>A small inked tag for the suspect list and dossier.</summary>
    private static Border Chip(string text, Brush ink, bool disputed, string? tip) => new()
    {
        BorderBrush = ink,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(5, 0, 5, 1),
        Margin = new Thickness(0, 1, 4, 2),
        ToolTip = tip,
        Child = new TextBlock
        {
            Text = text,
            FontFamily = Ui.Body,
            FontSize = 11.5,
            Foreground = ink,
            FontStyle = disputed ? FontStyles.Italic : FontStyles.Normal,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 240,
        },
    };

    private void OpenDossier()
    {
        if (_dossier is null || !_dossier.IsLoaded)
        {
            _dossier = new DossierWindow { Owner = this };
            _dossier.Closed += (_, _) => _dossier = null;
            _dossier.Show();
        }
        else
        {
            _dossier.Activate();
        }

        _dossier?.Refresh(_game);
    }

    private static UIElement Header(string title, string? sub)
    {
        var panel = new StackPanel { Margin = new Thickness(4, 0, 10, 4) };
        panel.Children.Add(new TextBlock { Text = title, FontFamily = Ui.Hand, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Ui.Ink });
        if (sub is not null)
        {
            panel.Children.Add(new TextBlock { Text = sub, FontFamily = Ui.Body, FontSize = 11, FontStyle = FontStyles.Italic, Foreground = Ui.RedInk });
        }

        return panel;
    }

    private static UIElement MatrixText(string text, Brush brush, double opacity, bool disputed) => new TextBlock
    {
        Text = text,
        FontFamily = Ui.Body,
        FontSize = 12,
        Foreground = brush,
        Opacity = opacity,
        FontStyle = disputed ? FontStyles.Italic : FontStyles.Normal,
        Margin = new Thickness(4, 2, 10, 2),
        VerticalAlignment = VerticalAlignment.Center,
        MaxWidth = 150,
        TextWrapping = TextWrapping.Wrap,
    };

    private static void AddCell(Grid grid, int row, int column, UIElement element)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    // ================================================================== contradictions

    private void RefreshContradictions()
    {
        ContradictionsPanel.Children.Clear();
        if (_game is null)
        {
            return;
        }

        if (_game.Contradictions.Count == 0)
        {
            ContradictionsPanel.Children.Add(Ui.Text(
                "No clashes spotted yet. When two things you've noted can't both be true, they'll appear here. Each one earns you Deduction.",
                13, Ui.InkFaded, italic: true));
            return;
        }

        foreach (Contradiction c in _game.Contradictions.Reverse())
        {
            var panel = new StackPanel();
            panel.Children.Add(Ui.Handwriting("⚡ Contradiction", 15, Ui.RedInk, FontWeights.Bold));
            panel.Children.Add(Ui.Text(c.Description, 13));
            ContradictionsPanel.Children.Add(Ui.Card(panel, Ui.RedInk));
        }

        ContradictionsPanel.Children.Add(Ui.Text(
            "A clash means at least one source is wrong. The murderer lies, but so do people with secrets, and sergeants mishear.",
            11, Ui.InkFaded, italic: true));
    }

    // ================================================================== schedule

    private void RefreshSchedule()
    {
        SchedulePanel.Children.Clear();
        if (_game is null)
        {
            return;
        }

        CaseFile c = _game.Case;
        var facts = _game.DiscoveredClues.SelectMany(x => x.Facts.OfType<ScheduleFact>()).ToList();

        SchedulePanel.Children.Add(Ui.Text(
            "Where people are expected today, from what the household has told you. ◉ marks where you saw someone yourself.",
            12, Ui.InkFaded, italic: true));

        var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int h = 0; h < GameRules.DayHours; h++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int h = 0; h < GameRules.DayHours; h++)
        {
            int hour = GameRules.DayStartHour + h;
            var head = new Border
            {
                Background = h == _game.HourIndex ? Ui.PaperDark : null,
                Child = new TextBlock { Text = Clock.Hour(hour), FontFamily = Ui.Hand, FontSize = 12, Foreground = Ui.Ink, Margin = new Thickness(4, 0, 4, 2) },
            };
            AddCell(grid, 0, h + 1, head);
        }

        int row = 1;
        foreach (Character s in c.Suspects)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddCell(grid, row, 0, new TextBlock { Text = s.ShortName, FontFamily = Ui.Hand, FontSize = 13, Foreground = Ui.Ink, Margin = new Thickness(0, 2, 8, 2) });

            for (int h = 0; h < GameRules.DayHours; h++)
            {
                int hour = GameRules.DayStartHour + h;
                string text = string.Empty;
                ScheduleFact? known = facts.LastOrDefault(f => f.CharacterId == s.Id && hour >= f.FromHour && hour < f.ToHour);
                if (known is not null)
                {
                    text = ShortRoom(c, known.RoomId);
                }

                if (_game.LastSeen.TryGetValue(s.Id, out Sighting? seen) && seen.Minute / 60 == hour)
                {
                    text = $"◉ {ShortRoom(c, seen.RoomId)}";
                }

                var cell = new Border
                {
                    Background = h == _game.HourIndex ? Ui.PaperDark : null,
                    Child = new TextBlock { Text = text, FontFamily = Ui.Body, FontSize = 11, Foreground = Ui.Ink, Margin = new Thickness(4, 2, 4, 2) },
                };
                AddCell(grid, row, h + 1, cell);
            }

            row++;
        }

        SchedulePanel.Children.Add(grid);

        // Last seen list
        SchedulePanel.Children.Add(Ui.Rule());
        SchedulePanel.Children.Add(Ui.Handwriting("Last seen", 15, weight: FontWeights.Bold));
        foreach (Character s in c.Suspects)
        {
            string text = _game.LastSeen.TryGetValue(s.Id, out Sighting? seen)
                ? $"{s.ShortName}: {c.Manor.NameOf(seen.RoomId)} at {Clock.Format(seen.Minute)}"
                : $"{s.ShortName}: not yet seen";
            SchedulePanel.Children.Add(Ui.Text(text, 12));
        }
    }

    private static string ShortRoom(CaseFile c, string roomId) =>
        roomId == RoomIds.Away ? "village" : c.Manor.NameOf(roomId).Replace("Servants' ", "Svts' ").Replace(" Room", string.Empty);
}
