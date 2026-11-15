using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HunchesAndHerrings.Audio;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Model;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace HunchesAndHerrings;

/// <summary>The journal: opening it, writing new entries in, circling, and the launcher on the right.</summary>
public partial class MainWindow
{
    /// <summary>How many journal entries the player has already been shown being written.</summary>
    private int _journalSeen;

    private void OpenJournalForNewEntries()
    {
        if (_game is null)
        {
            return;
        }

        int count = _game.Journal.Count;
        if (count <= _journalSeen)
        {
            return;
        }

        if (AutoJournalCheck.IsChecked == true && OverlayHost.Visibility != Visibility.Visible)
        {
            var fresh = _game.Journal.Skip(_journalSeen).Select(j => j.Key).ToList();
            _journalSeen = count;
            OpenJournal(fresh);
        }

        RefreshJournalLauncher();
    }

    private void OpenJournal(IReadOnlyList<string>? writeKeys)
    {
        if (_game is null)
        {
            return;
        }

        if (writeKeys is null)
        {
            _journalSeen = _game.Journal.Count;
        }

        CaseFile c = _game.Case;
        var title = new JournalTitle(
            c.Title,
            $"{c.Manor.Name}\nthe death of {c.Victim.FullName}",
            $"Case No. {c.Seed}. Kept by {_game.Detective.Title} ({_game.Detective.Name}), with {c.SergeantName}.");

        JournalHost.Visibility = Visibility.Visible;
        _sound.Play(Sfx.Page, 0.5);
        _journal.Show(title, BuildJournalEntries(), writeKeys);
        RefreshJournalLauncher();
    }

    private void CloseJournal()
    {
        _journal.Skip();
        JournalHost.Visibility = Visibility.Collapsed;
        _sound.Play(Sfx.Page, 0.35);
        RefreshJournalLauncher();
        CheckTimeUp();
    }

    private void OnJournalCircle(string key)
    {
        if (_game is null)
        {
            return;
        }

        if (key.StartsWith("p:", StringComparison.Ordinal))
        {
            return;
        }

        _game.ToggleCircle(key);
        _sound.Play(Sfx.Quill, 0.4);
        _journal.Refresh(BuildJournalEntries());
        RefreshJournalLauncher();
    }

    private void OnJournalHighlight(string key, int first, int last, bool erase)
    {
        if (_game is null || key.StartsWith("p:", StringComparison.Ordinal))
        {
            return;
        }

        if (erase)
        {
            _game.EraseHighlight(key, first, last);
        }
        else
        {
            _game.AddHighlight(key, first, last);
        }

        _sound.Play(Sfx.Quill, erase ? 0.25 : 0.4);
        _journal.Refresh(BuildJournalEntries());
        RefreshJournalLauncher();
    }

    private void OnJournalAction(string key)
    {
        if (_game is null || !key.StartsWith(JournalItem.CluePrefix, StringComparison.Ordinal))
        {
            return;
        }

        string clueId = key[JournalItem.CluePrefix.Length..];
        Act(g => g.Scrutinise(clueId), Sfx.Hint);
    }

    /// <summary>Turns the session's journal into display entries, in the order they were written.</summary>
    private List<JournalEntry> BuildJournalEntries()
    {
        var result = new List<JournalEntry>();
        if (_game is null)
        {
            return result;
        }

        GameSession g = _game;
        var clashing = g.Contradictions.SelectMany(x => new[] { x.ClueA, x.ClueB }).ToHashSet();

        // All the small points circled inside entries, gathered on one page (shown under "Circled").
        var points = new List<string>();
        foreach (JournalItem item in g.Journal)
        {
            var phrases = g.HighlightedPhrases(item.Key);
            if (phrases.Count > 0)
            {
                string from = item.IsClue ? g.GetClue(item.RefId)?.Title ?? "?" : "a contradiction";
                points.AddRange(phrases.Select(p => $"\u201C{p}\u201D ({from})"));
            }
        }

        if (points.Count > 0)
        {
            result.Add(new JournalEntry("p:summary", "Points", string.Empty, "Points circled", $"{points.Count} small points, gathered together",
                string.Join("  ·  ", points), Ui.RedInk, Array.Empty<string>(), true, null, false));
        }

        foreach (JournalItem item in g.Journal)
        {
            string stamp = Clock.Format(item.Minute);
            bool circled = g.Circled.Contains(item.Key);
            IReadOnlyList<WordSpan>? spans = g.Highlights.TryGetValue(item.Key, out List<WordSpan>? found) ? found : null;

            if (!item.IsClue)
            {
                Contradiction? clash = g.GetContradiction(item.RefId);
                if (clash is not null)
                {
                    result.Add(new JournalEntry(item.Key, "Clashes", stamp, "Contradiction!", "Two things in this journal can't both be true",
                        clash.Description, Ui.RedInk, Array.Empty<string>(), circled, null, false, spans));
                }

                continue;
            }

            Clue? clue = g.GetClue(item.RefId);
            if (clue is null)
            {
                continue;
            }

            bool viaSergeant = g.SergeantFound.Contains(clue.Id);
            var badges = new List<string>();
            if (viaSergeant)
            {
                badges.Add("via the sergeant");
            }

            if (clashing.Contains(clue.Id))
            {
                badges.Add("clash");
            }

            if (g.Scrutiny.TryGetValue(clue.Id, out bool ringsTrue))
            {
                badges.Add(ringsTrue ? "rings true" : "rings false");
            }

            string? action = null;
            bool actionEnabled = false;
            if (clue.IsStatement && !g.Scrutiny.ContainsKey(clue.Id) && !g.IsOver)
            {
                action = $"scrutinise (−{g.Settings.ScrutinyCost})";
                actionEnabled = g.Deduction >= g.Settings.ScrutinyCost;
            }

            string category = clue.Kind switch
            {
                ClueKind.Scene => "Scene",
                ClueKind.Document or ClueKind.Observation => "Papers",
                _ => "Statements",
            };

            Brush ink = clue.Kind == ClueKind.Report || viaSergeant ? Ui.BlueInk : Ui.Ink;
            result.Add(new JournalEntry(item.Key, category, stamp, clue.Title, SourceLine(clue), clue.Text, ink, badges, circled, action, actionEnabled, spans));
        }

        return result;
    }

    private string SourceLine(Clue clue)
    {
        GameSession game = _game!;
        if (clue.SpeakerId is not null)
        {
            return clue.Kind == ClueKind.Report && clue.SpeakerId != game.Case.Rival?.Detective.Id
                ? $"as reported by {game.Case.SergeantName}"
                : $"in the words of {game.Case.NameOf(clue.SpeakerId)}";
        }

        string where = clue.RoomId is not null ? game.Case.Manor.NameOf(clue.RoomId) : "the house";
        return clue.Kind switch
        {
            ClueKind.Scene => $"at the scene, the {where}",
            ClueKind.Document => $"a document found in the {where}",
            _ => $"observed in the {where}",
        };
    }

    private void RefreshJournalLauncher()
    {
        if (_game is null)
        {
            JournalSummaryText.Text = string.Empty;
            return;
        }

        int total = _game.Journal.Count;
        int unseen = Math.Max(0, total - _journalSeen);
        int circled = _game.Circled.Count;
        JournalSummaryText.Text = $"{total} entr{(total == 1 ? "y" : "ies")}"
            + (circled > 0 ? $", {circled} circled" : string.Empty)
            + (unseen > 0 ? $"  ·  {unseen} new, unread" : string.Empty);
        JournalSummaryText.Foreground = unseen > 0 ? Ui.RedInk : Ui.InkFaded;
    }

    /// <summary>A little closed leather journal for the launcher.</summary>
    private static UIElement BookIcon()
    {
        var canvas = new Canvas { Width = 46, Height = 56 };
        var cover = new Rectangle
        {
            Width = 40,
            Height = 52,
            RadiusX = 3,
            RadiusY = 3,
            Fill = new LinearGradientBrush(Color.FromRgb(0x6A, 0x3E, 0x24), Color.FromRgb(0x3A, 0x21, 0x13), 45),
            Stroke = Ui.Ink,
            StrokeThickness = 1,
        };
        Canvas.SetLeft(cover, 4);
        Canvas.SetTop(cover, 2);
        canvas.Children.Add(cover);

        var pages = new Rectangle { Width = 4, Height = 48, Fill = new SolidColorBrush(Color.FromRgb(0xF0, 0xE6, 0xCC)) };
        Canvas.SetLeft(pages, 41);
        Canvas.SetTop(pages, 4);
        canvas.Children.Add(pages);

        var strap = new Rectangle { Width = 40, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x18, 0x0E)) };
        Canvas.SetLeft(strap, 4);
        Canvas.SetTop(strap, 30);
        canvas.Children.Add(strap);

        var label = new Border
        {
            Width = 24,
            Height = 12,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xB8, 0x72)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "HH", FontSize = 8, FontFamily = Ui.TitleFont, Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0xB8, 0x72)), HorizontalAlignment = HorizontalAlignment.Center },
        };
        Canvas.SetLeft(label, 12);
        Canvas.SetTop(label, 10);
        canvas.Children.Add(label);
        return canvas;
    }
}
