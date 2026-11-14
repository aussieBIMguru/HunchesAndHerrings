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
        if (_game is null)
        {
            return;
        }

        CaseFile c = _game.Case;
        var discovered = _game.DiscoveredClues.ToList();
        var sceneKnown = discovered.SelectMany(x => x.Facts.OfType<SceneFact>()).GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.First().Value);
        bool todKnown = _game.Settings.RevealTimeOfDeath || discovered.Any(x => x.Facts.OfType<TimeOfDeathFact>().Any());
        var categories = Enum.GetValues<TraitCategory>();

        SuspectsPanel.Children.Add(Ui.Text(
            $"◯ circles a suspect to watch (up to {GameSession.MaxFocus}): they're ringed on the plan wherever they go. Click a name to strike them off. Everything here comes from what you've found, and some of it may be lies.",
            12, Ui.InkFaded, italic: true));

        var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        int columns = 4 + categories.Length;
        for (int i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCell(grid, 0, 0, Header("◯", null));
        AddCell(grid, 0, 1, Header("Suspect", null));
        AddCell(grid, 0, 2, Header(todKnown ? $"At {NightSlots.Short(c.MurderSlot)}" : "At time of death", todKnown ? null : "time unknown"));
        for (int i = 0; i < categories.Length; i++)
        {
            TraitCategory cat = categories[i];
            string? scene = sceneKnown.TryGetValue(cat, out string? v) ? TraitRules.SceneLabel(cat, v) : null;
            AddCell(grid, 0, 3 + i, Header(TraitRules.CategoryLabel(cat), scene));
        }

        AddCell(grid, 0, columns - 1, Header("Motive", null));

        int row = 1;
        foreach (Character s in c.Suspects)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bool struck = _game.Struck.Contains(s.Id);
            double opacity = struck ? 0.4 : 1;

            // Name cell (click to strike)
            var nameCell = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, Opacity = opacity, ToolTip = "Click to strike off / restore" };
            nameCell.Children.Add(PortraitRenderer.Create(s, 30));
            nameCell.Children.Add(new TextBlock
            {
                Text = s.ShortName,
                FontFamily = Ui.Hand,
                FontSize = 14,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Ui.Ink,
                TextDecorations = struck ? TextDecorations.Strikethrough : null,
            });
            string id = s.Id;
            nameCell.MouseLeftButtonUp += (_, _) =>
            {
                _game?.ToggleStrike(id);
                _sound.Play(Sfx.Quill, 0.3);
                RefreshSuspects();
                RefreshRoom();
            };

            bool watched = _game.Focused.Contains(s.Id);
            var focus = new TextBlock
            {
                Text = watched ? "◉" : "◯",
                FontSize = 18,
                Foreground = watched ? Brushes.DarkGoldenrod : Ui.InkFaded,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = watched ? "Stop watching" : "Keep an eye on this suspect",
            };
            focus.MouseLeftButtonUp += (_, _) => ToggleFocus(id);
            AddCell(grid, row, 0, focus);
            AddCell(grid, row, 1, nameCell);

            // Whereabouts at the time of death
            string where = "?";
            if (todKnown)
            {
                var places = discovered
                    .SelectMany(x => x.Facts.OfType<LocationFact>().Select(f => (Clue: x, Fact: f)))
                    .Where(p => p.Fact.CharacterId == s.Id && p.Fact.Slot == c.MurderSlot)
                    .GroupBy(p => p.Fact.RoomId)
                    .Select(g => $"{c.Manor.NameOf(g.Key)} ({g.Select(p => p.Clue.Id).Distinct().Count()})")
                    .ToList();
                where = places.Count > 0 ? string.Join(" / ", places) : "?";
            }

            AddCell(grid, row, 2, MatrixText(where, Ui.Ink, opacity, where.Contains('/')));

            // Traits
            for (int i = 0; i < categories.Length; i++)
            {
                TraitCategory cat = categories[i];
                var values = discovered
                    .SelectMany(x => x.Facts.OfType<TraitFact>())
                    .Where(f => f.CharacterId == s.Id && f.Category == cat)
                    .Select(f => f.Value)
                    .Distinct()
                    .ToList();

                string text = values.Count == 0 ? "·" : string.Join(" / ", values.Select(v => ShortTrait(cat, v)));
                bool excluded = _game.Settings.HighlightExclusions
                    && values.Count == 1
                    && sceneKnown.TryGetValue(cat, out string? sv)
                    && TraitRules.Excludes(cat, sv, values[0]);
                AddCell(grid, row, 3 + i, MatrixText(excluded ? $"✗ {text}" : text, excluded ? Ui.RedInk : Ui.Ink, opacity, values.Count > 1));
            }

            // Motive
            var motives = discovered
                .SelectMany(x => x.Facts.OfType<MotiveFact>())
                .Where(f => f.CharacterId == s.Id)
                .Select(f => TextBank.MotiveNote(f.Motive))
                .Distinct()
                .ToList();
            AddCell(grid, row, columns - 1, MatrixText(motives.Count > 0 ? string.Join("; ", motives) : "·", Ui.Ink, opacity, false));
            row++;
        }

        SuspectsPanel.Children.Add(grid);
        SuspectsPanel.Children.Add(Ui.Text(
            "Numbers in brackets count separate sources. A slash means accounts disagree: someone is lying, or mistaken.",
            11, Ui.InkFaded, italic: true));
    }

    private static string ShortTrait(TraitCategory cat, string value) => cat switch
    {
        TraitCategory.Smoking => value == nameof(SmokeKind.None) ? "none" : value.ToLowerInvariant(),
        TraitCategory.Handedness => value.ToLowerInvariant(),
        TraitCategory.ShoeSize => value.ToLowerInvariant(),
        TraitCategory.Allergy => value == nameof(Allergen.None) ? "none" : value.ToLowerInvariant(),
        TraitCategory.Strength => value == TraitRules.Frail ? "frail" : "sound",
        _ => value,
    };

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
