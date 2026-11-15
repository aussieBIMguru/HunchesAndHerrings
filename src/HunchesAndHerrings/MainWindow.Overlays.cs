using System.IO;
using System.Windows;
using System.Windows.Controls;
using HunchesAndHerrings.Audio;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Generation;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Rendering;
using HunchesAndHerrings.Settings;
using Microsoft.Win32;

namespace HunchesAndHerrings;

/// <summary>Full-screen paper cards: title, briefing, accusation, verdict, messages; plus save/load.</summary>
public partial class MainWindow
{
    private static readonly string SavesFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Hunches & Herrings");

    private void ShowOverlay(UIElement content)
    {
        OverlayContent.Content = content;
        OverlayHost.Visibility = Visibility.Visible;
    }

    private void HideOverlay()
    {
        OverlayHost.Visibility = Visibility.Collapsed;
        OverlayContent.Content = null;
    }

    // ================================================================== title

    private void ShowTitle()
    {
        if (JournalHost.Visibility == Visibility.Visible)
        {
            _journal.Skip();
            JournalHost.Visibility = Visibility.Collapsed;
        }

        var root = new StackPanel { Width = 900 };

        // Logo and name
        var logoRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        logoRow.Children.Add(new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/HunchesAndHerrings.png")),
            Width = 120,
            Height = 120,
            Margin = new Thickness(0, 0, 18, 0),
        });
        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock { Text = "Hunches & Herrings", FontFamily = Ui.TitleFont, FontSize = 56, Foreground = Ui.Ink });
        nameStack.Children.Add(new TextBlock { Text = "A Victorian mystery of deduction", FontFamily = Ui.Hand, FontSize = 20, Foreground = Ui.InkFaded });
        logoRow.Children.Add(nameStack);
        root.Children.Add(logoRow);
        root.Children.Add(Ui.Rule());

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(columns);

        // ---------------- left: a new case
        var left = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
        Grid.SetColumn(left, 0);
        columns.Children.Add(left);

        left.Children.Add(Ui.Handwriting("A new case", 22, weight: FontWeights.Bold));

        // The detective
        Detective remembered = _game?.Detective ?? PlayerProfile.Load();
        left.Children.Add(Ui.Handwriting("Your detective", 16, weight: FontWeights.Bold));
        var nameRow = new DockPanel();
        var nameLabel = Ui.Handwriting("Name ", 15);
        nameLabel.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(nameLabel, Dock.Left);
        nameRow.Children.Add(nameLabel);
        var genderPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0) };
        var male = new RadioButton { GroupName = "Gender", Content = "Male", IsChecked = remembered.Gender == Gender.Male, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var female = new RadioButton { GroupName = "Gender", Content = "Female", IsChecked = remembered.Gender == Gender.Female, VerticalAlignment = VerticalAlignment.Center };
        genderPanel.Children.Add(male);
        genderPanel.Children.Add(female);
        DockPanel.SetDock(genderPanel, Dock.Right);
        nameRow.Children.Add(genderPanel);
        var nameBox = new TextBox { Text = remembered.Name, MaxLength = Detective.MaxNameLength, VerticalContentAlignment = VerticalAlignment.Center };
        nameRow.Children.Add(nameBox);
        left.Children.Add(nameRow);
        var addressText = Ui.Text(string.Empty, 12, Ui.InkFaded, italic: true);
        left.Children.Add(addressText);

        Detective CurrentDetective() => Detective.Create(nameBox.Text, female.IsChecked == true ? Gender.Female : Gender.Male);
        void UpdateAddress()
        {
            Detective d = CurrentDetective();
            addressText.Text = $"The household will call you \"{d.Title}\", or \"{d.Sir}\".";
        }

        nameBox.TextChanged += (_, _) => UpdateAddress();
        male.Checked += (_, _) => UpdateAddress();
        female.Checked += (_, _) => UpdateAddress();
        UpdateAddress();

        left.Children.Add(Ui.Handwriting("Your rank", 16, weight: FontWeights.Bold));
        var radios = new List<(RadioButton Radio, Difficulty Difficulty)>();
        var presetText = Ui.Text(string.Empty, 12, Ui.InkFaded, italic: true);
        foreach (Difficulty d in Enum.GetValues<Difficulty>())
        {
            var radio = new RadioButton
            {
                GroupName = "Difficulty",
                IsChecked = d == Difficulty.Inspector,
                Content = new StackPanel
                {
                    Children =
                    {
                        Ui.Handwriting(d.ToString(), 16, weight: FontWeights.Bold),
                        Ui.Text(DifficultySettings.For(d).Blurb, 12.5, Ui.InkFaded),
                    },
                },
            };
            radios.Add((radio, d));
            left.Children.Add(radio);
        }

        Difficulty SelectedDifficulty() => radios.First(r => r.Radio.IsChecked == true).Difficulty;

        // Tuning
        left.Children.Add(Ui.Handwriting("Tune the case", 16, weight: FontWeights.Bold));
        left.Children.Add(presetText);
        var tuning = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        tuning.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tuning.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        ComboBox Option(int row, string label, IEnumerable<string> items)
        {
            tuning.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var text = Ui.Handwriting(label, 14);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetRow(text, row);
            tuning.Children.Add(text);
            var combo = new ComboBox { ItemsSource = items.ToList(), SelectedIndex = 0 };
            Grid.SetRow(combo, row);
            Grid.SetColumn(combo, 1);
            tuning.Children.Add(combo);
            return combo;
        }

        var suspects = Option(0, "Suspects", new[] { "As the rank" }.Concat(Enumerable.Range(CaseOptions.MinSuspects, CaseOptions.MaxSuspects - CaseOptions.MinSuspects + 1).Select(n => $"{n} suspects")));
        var endHour = Option(1, "Name the murderer by", new[] { "As the rank" }.Concat(Enumerable.Range(CaseOptions.EarliestEndHour, CaseOptions.LatestEndHour - CaseOptions.EarliestEndHour + 1).Select(Clock.Hour)));
        var rescues = Option(2, "Sergeant's rescues", new[] { "As the rank", "None", "One", "Two", "Three" });
        var herrings = Option(3, "Red herrings", new[] { "As the rank", "On: lies, frames and secrets", "Off: only the murderer lies" });
        var kinds = new (string Label, CaseModel? Model)[]
        {
            ("A surprise (any kind)", null),
            ("A lone killer", CaseModel.LoneKiller),
            ("Two conspirators", CaseModel.Conspiracy),
            ("The children, together", CaseModel.FamilyPact),
            ("A house full of secrets", CaseModel.HiddenSecrets),
            ("A secret heir", CaseModel.SecretHeir),
            ("Perhaps no murder at all", CaseModel.Accident),
            ("The doctor's poison", CaseModel.DoctorPoison),
            ("The doctor's blunder", CaseModel.DoctorNegligence),
            ("An affair", CaseModel.Affair),
            ("The wrong victim", CaseModel.MistakenIdentity),
            ("An impostor", CaseModel.Impostor),
        };
        var kind = Option(4, "Kind of case", kinds.Select(k => k.Label));
        var reliable = new CheckBox { Content = "The sergeant never mishears", Margin = new Thickness(0, 6, 0, 0) };
        var rivalBox = new CheckBox { Content = "A rival detective on the case (a nuisance, with the odd tip)", Margin = new Thickness(0, 4, 0, 0) };
        left.Children.Add(tuning);
        left.Children.Add(reliable);
        left.Children.Add(rivalBox);
        left.Children.Add(Ui.Text("Picking a kind of case gives the game away; leave it on \"a surprise\" for the real thing.", 11, Ui.InkFaded, italic: true));

        void UpdatePreset()
        {
            DifficultySettings s = DifficultySettings.For(SelectedDifficulty());
            presetText.Text = $"{SelectedDifficulty()} gives {s.SuspectCount} suspects, until {Clock.Hour(s.EndHour)}, {s.MaxRescues} rescue(s), red herrings {(s.FrameLies ? "on" : "off")}.";
        }

        foreach (var (radio, _) in radios)
        {
            radio.Checked += (_, _) => UpdatePreset();
        }

        UpdatePreset();

        // Seed
        var seedRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var dice = Ui.Button("🎲", () => { }, "Roll a random case number");
        DockPanel.SetDock(dice, Dock.Right);
        seedRow.Children.Add(dice);
        var seedLabel = Ui.Handwriting("Case number ", 15);
        seedLabel.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(seedLabel, Dock.Left);
        seedRow.Children.Add(seedLabel);
        var seedBox = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Leave blank for a fresh mystery, or enter a number to replay or share a case." };
        seedRow.Children.Add(seedBox);
        dice.Click += (_, _) => seedBox.Text = Random.Shared.Next(10000, 99999).ToString();
        left.Children.Add(seedRow);
        left.Children.Add(Ui.Text("Same number + same settings = the same mystery. Share it with a friend.", 11.5, Ui.InkFaded, italic: true));

        var errorText = Ui.Text(string.Empty, 13, Ui.RedInk);
        left.Children.Add(errorText);

        var begin = Ui.Button("Begin the investigation", () =>
        {
            int seed;
            string text = seedBox.Text.Trim();
            if (text.Length == 0)
            {
                seed = Random.Shared.Next(10000, 99999);
            }
            else if (!int.TryParse(text, out seed) || seed < 0)
            {
                errorText.Text = "A case number is a whole number, e.g. 48213.";
                return;
            }

            var options = new CaseOptions(SelectedDifficulty())
            {
                SuspectCount = suspects.SelectedIndex > 0 ? CaseOptions.MinSuspects + suspects.SelectedIndex - 1 : null,
                EndHour = endHour.SelectedIndex > 0 ? CaseOptions.EarliestEndHour + endHour.SelectedIndex - 1 : null,
                MaxRescues = rescues.SelectedIndex > 0 ? rescues.SelectedIndex - 1 : null,
                RedHerrings = herrings.SelectedIndex switch { 1 => true, 2 => false, _ => null },
                ReliableSergeant = reliable.IsChecked == true,
                Rival = rivalBox.IsChecked == true,
                Model = kinds[Math.Max(0, kind.SelectedIndex)].Model,
            };
            Detective detective = CurrentDetective();
            PlayerProfile.Save(detective);
            StartNewCase(seed, options, detective);
        });
        begin.FontSize = 19;
        begin.FontWeight = FontWeights.Bold;
        begin.Margin = new Thickness(0, 8, 0, 0);
        begin.HorizontalAlignment = HorizontalAlignment.Left;
        left.Children.Add(begin);

        // ---------------- right: continue / load
        var right = new StackPanel();
        Grid.SetColumn(right, 1);
        columns.Children.Add(right);

        if (_game is { IsOver: false })
        {
            right.Children.Add(Ui.Handwriting("The case in hand", 22, weight: FontWeights.Bold));
            right.Children.Add(Ui.Text($"{_game.Case.Title}: {Clock.Format(_game.Minute)}, {_game.Journal.Count} journal entries.", 13));
            right.Children.Add(Ui.Button("Return to the case", HideOverlay));
            right.Children.Add(Ui.Rule());
        }

        right.Children.Add(Ui.Handwriting("Resume a saved case", 22, weight: FontWeights.Bold));
        right.Children.Add(Ui.Button("Open a saved case…", () => LoadGame()));

        var recent = RecentSaves().Take(6).ToList();
        if (recent.Count > 0)
        {
            right.Children.Add(Ui.Text("Recent:", 12, Ui.InkFaded, italic: true));
            foreach (FileInfo file in recent)
            {
                string path = file.FullName;
                var link = new TextBlock
                {
                    Text = $"• {Path.GetFileNameWithoutExtension(file.Name)}  ({file.LastWriteTime:d MMM, HH:mm})",
                    FontFamily = Ui.Hand,
                    FontSize = 14,
                    Foreground = Ui.BlueInk,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    TextDecorations = TextDecorations.Underline,
                    Margin = new Thickness(0, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap,
                };
                link.MouseLeftButtonUp += (_, _) => LoadGameFrom(path);
                right.Children.Add(link);
            }
        }

        right.Children.Add(Ui.Rule());
        right.Children.Add(Ui.Handwriting("The game", 18, weight: FontWeights.Bold));
        right.Children.Add(Ui.Text(
            "A body in the manor and a houseful of suspects, every one with something to hide. " +
            "Walk the house, question the household, search the rooms, catch the lies, and name the murderer, " +
            "their motive and your proof before the Chief Constable arrives.", 13));

        ShowOverlay(root);
    }

    private IEnumerable<FileInfo> RecentSaves()
    {
        try
        {
            return Directory.Exists(SavesFolder)
                ? new DirectoryInfo(SavesFolder).GetFiles("*.hhcase").OrderByDescending(f => f.LastWriteTimeUtc).ToList()
                : Enumerable.Empty<FileInfo>();
        }
        catch (IOException)
        {
            return Enumerable.Empty<FileInfo>();
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<FileInfo>();
        }
    }

    private void StartNewCase(int seed, CaseOptions options, Detective detective)
    {
        _game = new GameSession(CaseGenerator.Generate(seed, options), detective);
        ResetViewState();
        SetFloor(Floor.Ground);
        RefreshAll();
        ShowBriefing();
    }

    /// <summary>Clears per-case UI state when a case is started or loaded.</summary>
    private void ResetViewState()
    {
        _selectedPersonId = null;
        _lastSpeech = string.Empty;
        _transientWarning = null;
        _viewHour = null;
        _journalSeen = _game?.Journal.Count ?? 0;
        _journal.Skip();
        JournalHost.Visibility = Visibility.Collapsed;
    }

    // ================================================================== briefing

    private void ShowBriefing()
    {
        if (_game is null)
        {
            return;
        }

        CaseFile c = _game.Case;
        var panel = new StackPanel { Width = 820 };
        panel.Children.Add(Ui.Title(c.Title, 40));
        panel.Children.Add(new TextBlock
        {
            Text = $"Case No. {c.Seed}  ·  {c.Manor.Name}  ·  {c.Difficulty}",
            FontFamily = Ui.Hand,
            FontSize = 15,
            Foreground = Ui.InkFaded,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
        });

        var victimRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var portrait = PortraitRenderer.Create(c.Victim, 90);
        portrait.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(portrait, Dock.Left);
        victimRow.Children.Add(portrait);
        victimRow.Children.Add(Ui.Text($"{_game.Detective.Title}, you've been sent for. {c.Briefing}", 15));
        panel.Children.Add(victimRow);

        panel.Children.Add(Ui.Rule());
        panel.Children.Add(Ui.Handwriting("The household", 18, weight: FontWeights.Bold));
        var cast = new WrapPanel();
        foreach (Character s in c.Suspects)
        {
            var card = new StackPanel { Width = 110, Margin = new Thickness(0, 0, 8, 8) };
            card.Children.Add(PortraitRenderer.Create(s, 70));
            card.Children.Add(new TextBlock { Text = s.ShortName, FontFamily = Ui.Hand, FontSize = 14, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Ink });
            card.Children.Add(new TextBlock { Text = s.RoleLabel, FontFamily = Ui.Body, FontSize = 11, FontStyle = FontStyles.Italic, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Ui.InkFaded });
            cast.Children.Add(card);
        }

        foreach (Character s in c.Children.Concat(c.Rival is { } rv ? new[] { rv.Detective } : Array.Empty<Character>()))
        {
            var card = new StackPanel { Width = 110, Margin = new Thickness(0, 0, 8, 8), Opacity = 0.8 };
            card.Children.Add(PortraitRenderer.Create(s, 70));
            card.Children.Add(new TextBlock { Text = s.ShortName, FontFamily = Ui.Hand, FontSize = 14, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Ink });
            card.Children.Add(new TextBlock { Text = $"{s.RoleLabel} (not a suspect)", FontFamily = Ui.Body, FontSize = 11, FontStyle = FontStyles.Italic, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Ui.InkFaded });
            cast.Children.Add(card);
        }

        panel.Children.Add(cast);
        panel.Children.Add(Ui.Rule());
        panel.Children.Add(Ui.Handwriting("How to investigate", 18, weight: FontWeights.Bold));
        string[] tips =
        {
            "Click a room on the blueprint to walk there. Every step, question and search costs time. Your footprints mark where you've been.",
            "Search the scene first: the body and what lies around it tell you when, and something about who.",
            "People move about the house through the day. Ask who'll be where, and plan your route.",
            "Each innocent can be ruled out: by an alibi others confirm, or by a trait that doesn't fit the scene.",
            "The murderer lies about where they were. When accounts clash, the notebook flags it and you earn Deduction.",
            $"Spend Deduction on a Hunch, or to Scrutinise a statement in the journal. Talk to {c.SergeantName} (bottom right): \"search the library\", \"question the cook\", \"where is the colonel?\", \"what next?\"",
            "Everything you learn is written into your journal. Circle the entries that matter, and circle up to three suspects to keep an eye on them on the plan.",
            "People with secrets lie about them. Find the proof among their things, then confront them with it: they'll come clean, about that at least.",
            "Callers from outside (marked ⌂) aren't always at the house. Send for them, or catch them when they call.",
            "Not every case is one killer. Sometimes two act together, sometimes a whole family, and sometimes nobody killed anyone at all.",
            "When you're sure, Accuse: name the guilty (one or more, or nobody), the motive, and the evidence that proves it.",
        };
        foreach (string tip in tips)
        {
            panel.Children.Add(Ui.Text($"•  {tip}", 13));
        }

        var begin = Ui.Button("To work", () =>
        {
            _sound.Play(Sfx.Step);
            HideOverlay();
        });
        begin.HorizontalAlignment = HorizontalAlignment.Center;
        begin.Margin = new Thickness(0, 14, 0, 0);
        panel.Children.Add(begin);
        ShowOverlay(panel);
    }

    // ================================================================== accusation

    private void ShowAccuse(bool forced)
    {
        if (_game is null || _game.IsOver)
        {
            return;
        }

        CaseFile c = _game.Case;
        var panel = new StackPanel { Width = 900 };
        panel.Children.Add(Ui.Title(forced ? "Time is up" : "J'accuse", 40));
        panel.Children.Add(Ui.Text(forced
            ? "The Chief Constable's carriage is at the door. You must name the murderer now."
            : "Name the guilty, why they did it, and the evidence that proves it. Tick more than one if you believe they acted together, or none at all if you believe there was no murder. This closes the case.", 14));
        panel.Children.Add(Ui.Rule());

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        // Suspects
        var who = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        who.Children.Add(Ui.Handwriting("The guilty", 17, weight: FontWeights.Bold));
        var suspectRadios = new List<(CheckBox Radio, string Id)>();
        var noMurder = new CheckBox { Content = "Nobody: it was no murder", FontFamily = Ui.Body, FontSize = 14, Foreground = Ui.Ink, Margin = new Thickness(0, 6, 0, 0) };
        foreach (Character s in c.Suspects)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(PortraitRenderer.Create(s, 32));
            content.Children.Add(new TextBlock
            {
                Text = $"{s.ShortName}{(_game.Struck.Contains(s.Id) ? "  (struck off)" : string.Empty)}",
                FontFamily = Ui.Body,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = Ui.Ink,
            });
            var radio = new CheckBox { Content = content, Margin = new Thickness(0, 1, 0, 1) };
            radio.Checked += (_, _) => noMurder.IsChecked = false;
            suspectRadios.Add((radio, s.Id));
            who.Children.Add(radio);
        }

        noMurder.Checked += (_, _) =>
        {
            foreach (var (box, _) in suspectRadios)
            {
                box.IsChecked = false;
            }
        };
        who.Children.Add(noMurder);

        Grid.SetColumn(who, 0);
        columns.Children.Add(who);

        // Motive
        var why = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        why.Children.Add(Ui.Handwriting("The motive", 17, weight: FontWeights.Bold));
        var motiveRadios = new List<(RadioButton Radio, MotiveKind Motive)>();
        foreach (MotiveKind m in Enum.GetValues<MotiveKind>().Where(k => k != MotiveKind.None))
        {
            var radio = new RadioButton { GroupName = "Motive", Content = RoleCatalog.MotiveName(m) };
            motiveRadios.Add((radio, m));
            why.Children.Add(radio);
        }

        Grid.SetColumn(why, 1);
        columns.Children.Add(why);

        // Evidence
        var proof = new StackPanel();
        proof.Children.Add(Ui.Handwriting("The key evidence", 17, weight: FontWeights.Bold));
        var clues = _game.DiscoveredClues
            .OrderByDescending(x => _game.Circled.Contains(JournalItem.CluePrefix + x.Id))
            .ToList();
        var evidence = new ListBox
        {
            Height = 300,
            FontFamily = Ui.Body,
            FontSize = 13,
            Background = Ui.Paper,
            Foreground = Ui.Ink,
            BorderBrush = Ui.InkFaded,
        };
        foreach (Clue clue in clues)
        {
            evidence.Items.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = _game.Circled.Contains(JournalItem.CluePrefix + clue.Id) ? $"◯ {clue.Title}" : clue.Title,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300,
                    FontWeight = _game.Circled.Contains(JournalItem.CluePrefix + clue.Id) ? FontWeights.Bold : FontWeights.Normal,
                },
                ToolTip = clue.Text,
                Tag = clue.Id,
            });
        }

        proof.Children.Add(evidence);
        proof.Children.Add(Ui.Text("Pick the single clue that best proves your case (optional, but it counts). Your circled entries are listed first.", 11, Ui.InkFaded, italic: true));
        Grid.SetColumn(proof, 2);
        columns.Children.Add(proof);
        panel.Children.Add(columns);

        var error = Ui.Text(string.Empty, 13, Ui.RedInk);
        panel.Children.Add(error);

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        var confirm = Ui.Button("Make the accusation", () =>
        {
            var accused = suspectRadios.Where(r => r.Radio.IsChecked == true).Select(r => r.Id).ToList();
            var motive = motiveRadios.FirstOrDefault(r => r.Radio.IsChecked == true);
            if ((accused.Count == 0 && noMurder.IsChecked != true) || motive.Radio is null)
            {
                error.Text = "You must name at least one suspect (or declare it no murder) and a motive.";
                return;
            }

            string? evidenceId = (evidence.SelectedItem as ListBoxItem)?.Tag as string;
            Verdict verdict = _game.Accuse(accused, motive.Motive, evidenceId);
            _sound.Play(verdict.CorrectMurderer ? Sfx.Win : Sfx.Lose, 0.8);
            RefreshAll();
            ShowResult();
        });
        confirm.Foreground = Ui.RedInk;
        confirm.FontWeight = FontWeights.Bold;
        buttons.Children.Add(confirm);
        if (!forced)
        {
            buttons.Children.Add(Ui.Button("Not yet", HideOverlay));
        }

        panel.Children.Add(buttons);
        ShowOverlay(panel);
    }

    // ================================================================== verdict

    private void ShowResult()
    {
        if (_game?.Verdict is not { } verdict)
        {
            return;
        }

        CaseFile c = _game.Case;
        Character murderer = c.Culprits.FirstOrDefault() ?? c.Victim;
        var panel = new StackPanel { Width = 860 };

        panel.Children.Add(Ui.Title(verdict.CorrectMurderer ? "Case closed" : verdict.Partial ? "Half the truth" : "The wrong answer", 44));
        panel.Children.Add(new TextBlock
        {
            Text = $"Grade {verdict.Grade}: {verdict.Headline}",
            FontFamily = Ui.Hand,
            FontSize = 20,
            Foreground = verdict.CorrectMurderer ? Ui.Ink : Ui.RedInk,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        var truthRow = new DockPanel();
        var portrait = PortraitRenderer.Create(murderer, 96);
        portrait.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(portrait, Dock.Left);
        truthRow.Children.Add(portrait);
        var truth = new StackPanel();
        truth.Children.Add(Ui.Handwriting("What really happened", 17, weight: FontWeights.Bold));
        truth.Children.Add(Ui.Text(c.Truth, 14));
        truthRow.Children.Add(truth);
        panel.Children.Add(truthRow);

        panel.Children.Add(Ui.Rule());
        panel.Children.Add(Ui.Handwriting("Your accusation", 17, weight: FontWeights.Bold));
        string named = verdict.DeclaredAccident ? "nobody (no murder)" : TextBank.JoinNames(verdict.AccusedIds.Select(c.NameOf).ToList());
        string truly = c.IsAccident ? "nobody: it was an accident" : TextBank.JoinNames(c.Culprits.Select(x => x.ShortName).ToList());
        panel.Children.Add(Mark(verdict.CorrectMurderer, $"You named: {named} (truly: {truly})"));
        panel.Children.Add(Mark(verdict.CorrectMotive, $"Motive: {RoleCatalog.MotiveName(verdict.Motive)} (truly: {RoleCatalog.MotiveName(c.CaseMotive)})"));
        string evidenceTitle = verdict.EvidenceId is not null ? _game.GetClue(verdict.EvidenceId)?.Title ?? "?" : "none chosen";
        panel.Children.Add(Mark(verdict.StrongEvidence, $"Key evidence: {evidenceTitle}"));
        var strongest = c.KeyEvidenceIds.Select(id => _game.GetClue(id)).OfType<Clue>().Select(x => x.Title).ToList();
        if (!verdict.StrongEvidence && strongest.Count > 0)
        {
            panel.Children.Add(Ui.Text($"Evidence that would have convicted: {string.Join("; ", strongest)}.", 12, Ui.InkFaded, italic: true));
        }

        if (c.Rival is { } rival)
        {
            string theirs = rival.AccusesAccident ? "that there was no murder at all" : TextBank.JoinNames(rival.AccusesIds.Select(c.NameOf).ToList());
            bool rivalRight = rival.AccusesAccident ? c.IsAccident : rival.AccusesIds.ToHashSet().SetEquals(c.CulpritIds) && !c.IsAccident;
            string verdictLine = (rivalRight, verdict.CorrectMurderer) switch
            {
                (true, true) => "You both got there. The newspapers credit them, naturally.",
                (true, false) => "They were right, and you weren't. They'll be insufferable.",
                (false, true) => "They were wrong. You permit yourself a small smile.",
                _ => "You were both wrong. Small comfort.",
            };
            panel.Children.Add(Ui.Text($"Your rival, {rival.Detective.ShortName}, named {theirs}. {verdictLine}", 13, Ui.BlueInk, italic: true));
        }

        panel.Children.Add(Ui.Rule());
        panel.Children.Add(Ui.Handwriting(c.IsAccident ? "How each person could have been cleared" : "How each innocent could have been cleared", 17, weight: FontWeights.Bold));
        foreach ((string id, Elimination elimination) in c.Eliminations)
        {
            int found = elimination.ClueIds.Count(_game.IsDiscovered);
            bool enough = elimination.ClueIds.Count > 0 && found > 0;
            panel.Children.Add(Mark(enough, $"{elimination.Reason} ({found} of {elimination.ClueIds.Count} supporting clues found)"));
        }

        var missed = c.Clues.Where(x => x.IsKey && !_game.IsDiscovered(x.Id)).ToList();
        if (missed.Count > 0)
        {
            panel.Children.Add(Ui.Rule());
            panel.Children.Add(Ui.Handwriting("What you missed", 17, weight: FontWeights.Bold));
            foreach (Clue clue in missed.Take(14))
            {
                string where = clue.RoomId is not null
                    ? $"in the {c.Manor.NameOf(clue.RoomId)}"
                    : clue.SpeakerId is not null ? $"from {c.NameOf(clue.SpeakerId)}" : string.Empty;
                panel.Children.Add(Ui.Text($"•  {clue.Title}, {where}: {clue.Text}", 12));
            }
        }

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(Ui.Button("A new mystery", ShowTitle));
        buttons.Children.Add(Ui.Button("Replay this case", () => StartNewCase(c.Seed, c.Options, _game.Detective)));
        buttons.Children.Add(Ui.Button("Review the notebook", HideOverlay));
        panel.Children.Add(buttons);
        ShowOverlay(panel);
    }

    private static UIElement Mark(bool good, string text)
    {
        var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        var tick = new TextBlock
        {
            Text = good ? "✓" : "✗",
            FontFamily = Ui.Hand,
            FontSize = 16,
            Foreground = good ? Ui.Ink : Ui.RedInk,
            Width = 22,
        };
        DockPanel.SetDock(tick, Dock.Left);
        row.Children.Add(tick);
        row.Children.Add(Ui.Text(text, 13));
        return row;
    }

    // ================================================================== messages

    private void ShowMessage(string title, string text)
    {
        var panel = new StackPanel { Width = 520 };
        panel.Children.Add(Ui.Title(title, 30));
        panel.Children.Add(Ui.Text(text, 14));
        var ok = Ui.Button("Very well", () =>
        {
            if (_game is null)
            {
                ShowTitle();
            }
            else
            {
                HideOverlay();
            }
        });
        ok.HorizontalAlignment = HorizontalAlignment.Center;
        ok.Margin = new Thickness(0, 12, 0, 0);
        panel.Children.Add(ok);
        ShowOverlay(panel);
    }

    // ================================================================== save / load

    private void SaveGame()
    {
        if (_game is null || _game.IsOver)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(SavesFolder);
            var dialog = new SaveFileDialog
            {
                Title = "Save case",
                Filter = "Hunches & Herrings case (*.hhcase)|*.hhcase",
                FileName = $"Case {_game.Case.Seed} at {Clock.Format(_game.Minute).Replace(':', '.')}.hhcase",
                InitialDirectory = SavesFolder,
            };

            if (dialog.ShowDialog(this) == true)
            {
                File.WriteAllText(dialog.FileName, SaveSerializer.Serialize(_game.ToSaveData()));
                _transientWarning = null;
                RefreshRoom();
            }
        }
        catch (IOException ex)
        {
            ShowMessage("Could not save", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowMessage("Could not save", ex.Message);
        }
    }

    private void LoadGame()
    {
        try
        {
            Directory.CreateDirectory(SavesFolder);
            var dialog = new OpenFileDialog
            {
                Title = "Open a saved case",
                Filter = "Hunches & Herrings case (*.hhcase)|*.hhcase|All files (*.*)|*.*",
                InitialDirectory = SavesFolder,
            };

            if (dialog.ShowDialog(this) == true)
            {
                LoadGameFrom(dialog.FileName);
            }
        }
        catch (IOException ex)
        {
            ShowMessage("Could not open the case", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowMessage("Could not open the case", ex.Message);
        }
    }

    private void LoadGameFrom(string path)
    {
        try
        {
            SaveData? data = SaveSerializer.TryDeserialize(File.ReadAllText(path), out string? error);
            if (data is null)
            {
                ShowMessage("Could not open the case", error ?? "Unknown problem.");
                return;
            }

            _game = GameSession.FromSaveData(data);
            ResetViewState();
            SetFloor(_game.CurrentRoom.Floor);
            RefreshAll();
            HideOverlay();
            CheckTimeUp();
        }
        catch (IOException ex)
        {
            ShowMessage("Could not open the case", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowMessage("Could not open the case", ex.Message);
        }
    }
}
