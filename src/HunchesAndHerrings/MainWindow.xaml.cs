using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using HunchesAndHerrings.Audio;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Rendering;

namespace HunchesAndHerrings;

/// <summary>
/// The main game window. This file holds wiring, the action pipeline, the header, the blueprint and the log.
/// Partial files hold the room panel, notebook, journal, sergeant chat and overlay screens.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SoundManager _sound = new();
    private readonly JournalView _journal;
    private GameSession? _game;
    private Floor _viewFloor = Floor.Ground;
    private int? _viewHour;
    private string? _selectedPersonId;
    private string _lastSpeech = string.Empty;
    private string? _transientWarning;
    private bool _suppressNotes;

    public MainWindow()
    {
        InitializeComponent();
        Background = PaperTexture.Brush;
        OverlayCard.Background = PaperTexture.Brush;
        MapBorder.Background = new SolidColorBrush(MapRenderer.BlueprintColor);

        _journal = new JournalView((sfx, volume) => _sound.Play(sfx, volume));
        _journal.CloseRequested += CloseJournal;
        _journal.CircleToggled += OnJournalCircle;
        _journal.ActionInvoked += OnJournalAction;
        JournalHost.Children.Add(_journal);
        JournalIconHost.Content = BookIcon();

        FloorUpper.Checked += (_, _) => SetFloor(Floor.Upper);
        FloorGround.Checked += (_, _) => SetFloor(Floor.Ground);
        FloorCellar.Checked += (_, _) => SetFloor(Floor.Cellar);
        HourPrev.Click += (_, _) => ShiftHour(-1);
        HourNext.Click += (_, _) => ShiftHour(1);
        HourNow.Click += (_, _) =>
        {
            _viewHour = null;
            RefreshMap();
        };

        SearchButton.Click += (_, _) => Act(g => g.Search(), Sfx.Search);
        WaitButton.Click += (_, _) => Act(g => g.Wait(), null);
        HunchButton.Click += (_, _) => Act(g => g.Hunch(), Sfx.Hint);
        AccuseButton.Click += (_, _) => ShowAccuse(forced: false);
        SaveButton.Click += (_, _) => SaveGame();
        LoadButton.Click += (_, _) => LoadGame();
        MenuButton.Click += (_, _) => ShowTitle();
        OpenJournalButton.Click += (_, e) =>
        {
            OpenJournal(null);
            e.Handled = true;
        };
        JournalLauncher.MouseLeftButtonUp += (_, _) => OpenJournal(null);

        MusicCheck.Checked += (_, _) => _sound.MusicEnabled = true;
        MusicCheck.Unchecked += (_, _) => _sound.MusicEnabled = false;
        SfxCheck.Checked += (_, _) => _sound.SfxEnabled = true;
        SfxCheck.Unchecked += (_, _) => _sound.SfxEnabled = false;

        NotesBox.TextChanged += (_, _) =>
        {
            if (_game is not null && !_suppressNotes)
            {
                _game.Notes = NotesBox.Text;
            }
        };

        WireSergeantChat();
        KeyDown += OnKeyDown;
        Loaded += OnLoaded;
        Closed += (_, _) => _sound.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShowTitle();
        await _sound.InitializeAsync();
        _sound.StartMusic();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (JournalHost.Visibility == Visibility.Visible)
        {
            if (_journal.IsAnimating)
            {
                _journal.Skip();
            }
            else
            {
                CloseJournal();
            }

            e.Handled = true;
        }
    }

    // ================================================================== action pipeline

    /// <summary>Runs a game action, plays sounds, refreshes the UI, opens the journal and handles time-up.</summary>
    private void Act(Func<GameSession, IReadOnlyList<GameMessage>> action, Sfx? sound)
    {
        if (_game is null)
        {
            return;
        }

        IReadOnlyList<GameMessage> msgs = action(_game);
        if (sound is { } sfx && msgs.Any(m => m.Kind != MessageKind.Warning))
        {
            _sound.Play(sfx);
        }

        React(msgs);
        RefreshAll();
        AfterAction();
    }

    private void AfterAction()
    {
        OpenJournalForNewEntries();
        CheckTimeUp();
    }

    private void React(IReadOnlyList<GameMessage> msgs)
    {
        _transientWarning = msgs.LastOrDefault(m => m.Kind == MessageKind.Warning)?.Text;

        if (msgs.Any(m => m.Kind == MessageKind.Contradiction))
        {
            _sound.Play(Sfx.Contradiction, 0.6);
        }

        if (msgs.Any(m => m.Kind == MessageKind.Sergeant))
        {
            _sound.Play(Sfx.Report, 0.4);
            FlashSergeant();
        }

        if (msgs.Any(m => m.Kind == MessageKind.Time))
        {
            _sound.Play(Sfx.Chime, 0.35);
        }
    }

    private void CheckTimeUp()
    {
        if (_game is { IsTimeUp: true, IsOver: false })
        {
            ShowAccuse(forced: true);
        }
    }

    private void OnRoomClicked(string roomId)
    {
        if (_game is null || roomId == _game.RoomId)
        {
            return;
        }

        _selectedPersonId = null;
        _lastSpeech = string.Empty;
        Act(g => g.MoveTo(roomId), Sfx.Step);
        SetFloor(_game.CurrentRoom.Floor);
    }

    private void OnMapPersonClicked(string personId)
    {
        _selectedPersonId = personId;
        _lastSpeech = string.Empty;
        RefreshRoom();
    }

    private void SetFloor(Floor floor)
    {
        _viewFloor = floor;
        RadioButton tab = floor switch
        {
            Floor.Upper => FloorUpper,
            Floor.Cellar => FloorCellar,
            _ => FloorGround,
        };

        if (tab.IsChecked != true)
        {
            tab.IsChecked = true; // re-enters SetFloor via Checked; harmless
        }

        RefreshMap();
    }

    private int ViewHour => _game is null ? 0 : Math.Clamp(_viewHour ?? _game.HourIndex, 0, GameRules.DayHours - 1);

    private void ShiftHour(int delta)
    {
        if (_game is null)
        {
            return;
        }

        int next = Math.Clamp(ViewHour + delta, 0, GameRules.DayHours - 1);
        _viewHour = next == _game.HourIndex ? null : next;
        _sound.Play(Sfx.Page, 0.25);
        RefreshMap();
    }

    // ================================================================== refresh

    private void RefreshAll()
    {
        if (_game is null)
        {
            return;
        }

        RefreshHeader();
        RefreshMap();
        RefreshRoom();
        RefreshNotebook();
        RefreshJournalLauncher();
        RefreshChat();
        RefreshLog();
        if (JournalHost.Visibility == Visibility.Visible)
        {
            _journal.Refresh(BuildJournalEntries());
        }
    }

    private void RefreshHeader()
    {
        if (_game is null)
        {
            return;
        }

        CaseFile c = _game.Case;
        Room scene = c.CrimeScene;
        CaseTitleText.Text = c.Title;
        CaseSubtitleText.Text = $"{_game.Detective.Title}  ·  Case No. {c.Seed}  ·  {c.Manor.Name}  ·  {c.Difficulty}{(c.Options.IsCustom ? " (custom)" : string.Empty)}";
        CrimeText.Text = $"✕ {c.Victim.ShortName} found dead in the {scene.Name}, {FloorName(scene.Floor).ToLowerInvariant()}";
        ClockText.Text = Clock.Format(_game.Minute);
        TimeFill.Width = Math.Max(0, 356 * _game.TimeUsed);
        int left = _game.MinutesLeft;
        TimeLeftText.Text = _game.IsOver
            ? "The case is closed."
            : left > 0
                ? $"{left / 60}h {left % 60:00}m until the Chief Constable arrives at {Clock.Hour(_game.Settings.EndHour)}"
                : "Time is up!";

        DeductionText.Text = $"✒ Deduction {_game.Deduction}";
        HunchButton.Content = $"Hunch (−{_game.Settings.HunchCost})";
        HunchButton.IsEnabled = !_game.IsOver && _game.Deduction >= _game.Settings.HunchCost;
        AccuseButton.IsEnabled = !_game.IsOver;
        SaveButton.IsEnabled = !_game.IsOver;
    }

    private static string FloorName(Floor floor) => floor switch
    {
        Floor.Upper => "Upper Floor",
        Floor.Cellar => "Cellar",
        _ => "Ground Floor",
    };

    private void RefreshMap()
    {
        MapInfoPanel.Children.Clear();
        if (_game is null)
        {
            MapCanvas.Children.Clear();
            return;
        }

        RefreshFloorTabs();
        int hour = ViewHour;
        bool now = hour == _game.HourIndex;
        HourText.Text = Clock.Hour(GameRules.DayStartHour + hour) + (now ? " (now)" : string.Empty);
        HourText.Foreground = now ? Ui.Ink : Ui.RedInk;

        MapRenderer.Render(MapCanvas, new MapView(_game, _viewFloor, hour, OnRoomClicked, OnMapPersonClicked));
        RefreshMapInfo(hour);
    }

    /// <summary>Floor tabs carry glyphs: ◉ you, ★ the sergeant, ✕ the murder, ◯ watched suspects (at the shown hour).</summary>
    private void RefreshFloorTabs()
    {
        if (_game is null)
        {
            return;
        }

        Manor manor = _game.Case.Manor;
        var watched = _game.WhereaboutsAt(ViewHour)
            .Where(w => _game.Focused.Contains(w.Person.Id) && manor.Contains(w.RoomId))
            .ToList();

        foreach ((RadioButton tab, Floor floor) in new[] { (FloorUpper, Floor.Upper), (FloorGround, Floor.Ground), (FloorCellar, Floor.Cellar) })
        {
            var text = new TextBlock();
            text.Inlines.Add(new Run(FloorName(floor)));
            var tips = new List<string>();

            if (manor[_game.RoomId].Floor == floor)
            {
                text.Inlines.Add(new Run("  ◉") { Foreground = Ui.RedInk });
                tips.Add("◉ you are here");
            }

            if (manor[_game.SergeantRoomId].Floor == floor)
            {
                text.Inlines.Add(new Run(" ★") { Foreground = Ui.BlueInk });
                tips.Add("★ the sergeant is here");
            }

            if (_game.Case.CrimeScene.Floor == floor)
            {
                text.Inlines.Add(new Run(" ✕") { Foreground = Ui.RedInk, FontWeight = FontWeights.Bold });
                tips.Add($"✕ the murder: {_game.Case.CrimeScene.Name}");
            }

            int focusHere = watched.Count(w => manor[w.RoomId].Floor == floor && (w.Certainty != Certainty.Rough || w.Precise));
            if (focusHere > 0)
            {
                text.Inlines.Add(new Run($" ◯{focusHere}") { Foreground = Brushes.DarkGoldenrod, FontWeight = FontWeights.Bold });
                tips.Add($"◯ {focusHere} of your watched suspects");
            }

            tab.Content = text;
            tab.ToolTip = tips.Count > 0 ? string.Join("\n", tips) : null;
        }
    }

    /// <summary>Below the plan: rough whereabouts by floor, and where your watched suspects are.</summary>
    private void RefreshMapInfo(int hour)
    {
        if (_game is null)
        {
            return;
        }

        Manor manor = _game.Case.Manor;
        var all = _game.WhereaboutsAt(hour);

        foreach (Whereabout w in all.Where(w => _game.Focused.Contains(w.Person.Id)))
        {
            bool inHouse = manor.Contains(w.RoomId);
            string where = !inHouse ? "away in the village"
                : w.Certainty != Certainty.Rough || w.Precise ? $"{manor[w.RoomId].Name}, {FloorName(manor[w.RoomId].Floor).ToLowerInvariant()}"
                : $"somewhere on the {FloorName(manor[w.RoomId].Floor).ToLowerInvariant()}";
            string how = w.Certainty switch
            {
                Certainty.Seen => "seen",
                Certainty.Told => "told",
                _ => "probably",
            };

            var line = new TextBlock
            {
                FontFamily = Ui.Hand,
                FontSize = 14,
                Cursor = inHouse ? Cursors.Hand : Cursors.Arrow,
                ToolTip = inHouse ? "Show that floor" : null,
                TextWrapping = TextWrapping.Wrap,
            };
            line.Inlines.Add(new Run("◯ ") { Foreground = Brushes.DarkGoldenrod, FontWeight = FontWeights.Bold });
            line.Inlines.Add(new Run(w.Person.ShortName) { FontWeight = FontWeights.Bold, Foreground = Ui.Ink });
            line.Inlines.Add(new Run($": {where} ({how})") { Foreground = Ui.Ink });
            if (inHouse)
            {
                Floor floor = manor[w.RoomId].Floor;
                line.MouseLeftButtonUp += (_, _) => SetFloor(floor);
            }

            MapInfoPanel.Children.Add(line);
        }

        if (_game.Focused.Count == 0)
        {
            MapInfoPanel.Children.Add(Ui.Text("Circle up to three suspects (◯ in the Suspects tab) to keep a close eye on where they are.", 12, Ui.InkFaded, italic: true));
        }

        var rough = all
            .Where(w => w.Certainty == Certainty.Rough && !w.Precise)
            .GroupBy(w => manor.Contains(w.RoomId) ? FloorName(manor[w.RoomId].Floor) : "In the village")
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(w => w.Person.ShortName))}")
            .ToList();
        if (rough.Count > 0)
        {
            MapInfoPanel.Children.Add(Ui.Text($"Roughly, at {Clock.Hour(GameRules.DayStartHour + hour)}: {string.Join("  ·  ", rough)}", 12, Ui.InkFaded));
        }
    }

    private void RefreshLog()
    {
        LogPanel.Children.Clear();
        if (_game is null)
        {
            return;
        }

        foreach (LogEntry entry in _game.Log.TakeLast(120))
        {
            Brush brush = entry.Kind switch
            {
                MessageKind.Contradiction or MessageKind.Warning => Ui.RedInk,
                MessageKind.Sergeant or MessageKind.Hint => Ui.BlueInk,
                MessageKind.Time => Ui.InkFaded,
                _ => Ui.Ink,
            };

            var line = new TextBlock
            {
                FontFamily = Ui.Body,
                FontSize = 13,
                Foreground = brush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 3),
                FontStyle = entry.Kind == MessageKind.Hint ? FontStyles.Italic : FontStyles.Normal,
            };
            line.Inlines.Add(new Run($"{Clock.Format(entry.Minute)}  ") { Foreground = Ui.InkFaded });
            line.Inlines.Add(new Run(entry.Text)
            {
                FontWeight = entry.Kind is MessageKind.Clue or MessageKind.Contradiction ? FontWeights.SemiBold : FontWeights.Normal,
            });
            LogPanel.Children.Add(line);
        }

        LogScroll.ScrollToEnd();
    }
}
