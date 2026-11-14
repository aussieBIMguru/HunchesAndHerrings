using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Rendering;

namespace HunchesAndHerrings;

/// <summary>The sergeant's chat panel, bottom right: type orders or use the quick buttons.</summary>
public partial class MainWindow
{
    private bool _chatFolded;
    private int _chipPerson;
    private int _chipRoom = -1;

    private void WireSergeantChat()
    {
        ChatSend.Click += (_, _) => SendChat(ChatInput.Text);
        ChatInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                SendChat(ChatInput.Text);
                e.Handled = true;
            }
        };
        ChatToggle.Click += (_, _) =>
        {
            _chatFolded = !_chatFolded;
            ChatBody.Visibility = _chatFolded ? Visibility.Collapsed : Visibility.Visible;
            ChatToggle.Content = _chatFolded ? "+" : "–";
            ChatToggle.ToolTip = _chatFolded ? "Open the conversation" : "Fold the conversation away";
        };
    }

    private void SendChat(string text)
    {
        if (_game is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ChatInput.Text = string.Empty;
        Act(g => g.SergeantCommand(text), null);
        ChatInput.Focus();
    }

    private void RefreshChat()
    {
        if (_game is null)
        {
            return;
        }

        CaseFile c = _game.Case;
        SergeantPortraitHost.Content = PortraitRenderer.CreateSergeant(42, c.Seed);
        SergeantNameText.Text = $"{c.SergeantName} ({c.SergeantStyle.ToString().ToLowerInvariant()})";
        SergeantStatusText.Text = $"Currently {_game.SergeantStatus}. Ask or instruct below.";

        ChatPanel.Children.Clear();
        foreach (ChatLine line in _game.Chat.TakeLast(40))
        {
            ChatPanel.Children.Add(Bubble(line));
        }

        ChatScroll.ScrollToEnd();
        BuildChips();
    }

    private UIElement Bubble(ChatLine line)
    {
        var text = new TextBlock
        {
            Text = line.Text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = line.FromPlayer ? Ui.Body : Ui.Hand,
            FontSize = line.FromPlayer ? 13 : 14,
            FontStyle = line.FromPlayer ? FontStyles.Italic : FontStyles.Normal,
            Foreground = line.FromPlayer ? Ui.Ink : Ui.BlueInk,
        };
        var stamp = new TextBlock
        {
            Text = $"{(line.FromPlayer ? _game?.Detective.Title ?? "You" : "Sergeant")} · {Core.Model.Clock.Format(line.Minute)}",
            FontFamily = Ui.Body,
            FontSize = 9.5,
            Foreground = Ui.InkFaded,
            HorizontalAlignment = line.FromPlayer ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        var stack = new StackPanel();
        stack.Children.Add(text);
        stack.Children.Add(stamp);
        return new Border
        {
            Child = stack,
            MaxWidth = 340,
            HorizontalAlignment = line.FromPlayer ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = line.FromPlayer ? new SolidColorBrush(Color.FromArgb(0x30, 0xA0, 0x82, 0x5A)) : new SolidColorBrush(Color.FromArgb(0x22, 0x23, 0x39, 0x5B)),
            BorderBrush = line.FromPlayer ? Ui.InkFaded : Ui.BlueInk,
            BorderThickness = new Thickness(1),
            CornerRadius = line.FromPlayer ? new CornerRadius(10, 10, 2, 10) : new CornerRadius(10, 10, 10, 2),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(line.FromPlayer ? 30 : 0, 0, line.FromPlayer ? 0 : 30, 5),
        };
    }

    /// <summary>Quick orders: each one is typed into the chat as if you'd said it.</summary>
    private void BuildChips()
    {
        ChatChips.Children.Clear();
        if (_game is null)
        {
            return;
        }

        bool free = _game.Sergeant is null && _game.CanAct;
        ChatChips.Children.Add(Chip("Search here", () => SendChat($"Search the {_game.CurrentRoom.Name}"), free));
        ChatChips.Children.Add(Chip("What next?", () => SendChat("What should I do next?"), true));
        ChatChips.Children.Add(Chip("Thoughts?", () => SendChat("Any thoughts on who did it?"), true));
        ChatChips.Children.Add(Chip("Recap", () => SendChat("Give me a recap"), true));

        var people = _game.Case.Suspects.ToList();
        var who = new ComboBox
        {
            ItemsSource = people.Select(p => p.ShortName).ToList(),
            SelectedIndex = Math.Clamp(_chipPerson, 0, people.Count - 1),
            MinWidth = 120,
            FontSize = 12,
            Margin = new Thickness(0, 2, 2, 2),
        };
        who.SelectionChanged += (_, _) => _chipPerson = who.SelectedIndex;
        ChatChips.Children.Add(who);
        ChatChips.Children.Add(Chip("Question", () => SendChat($"Question {people[Math.Max(0, who.SelectedIndex)].ShortName}"), free));
        ChatChips.Children.Add(Chip("Where?", () => SendChat($"Where is {people[Math.Max(0, who.SelectedIndex)].ShortName}?"), true));

        var rooms = _game.Case.Manor.Rooms.Where(r => !r.IsPassage).OrderBy(r => r.Floor).ThenBy(r => r.Name).ToList();
        var where = new ComboBox
        {
            ItemsSource = rooms.Select(r => r.Name).ToList(),
            SelectedIndex = _chipRoom >= 0 ? Math.Min(_chipRoom, rooms.Count - 1) : Math.Max(0, rooms.FindIndex(r => r.Id == _game.Case.CrimeSceneId)),
            MinWidth = 120,
            FontSize = 12,
            Margin = new Thickness(0, 2, 2, 2),
        };
        where.SelectionChanged += (_, _) => _chipRoom = where.SelectedIndex;
        ChatChips.Children.Add(where);
        ChatChips.Children.Add(Chip("Search there", () => SendChat($"Search the {rooms[Math.Max(0, where.SelectedIndex)].Name}"), free));
    }

    private static Button Chip(string label, Action onClick, bool enabled)
    {
        Button chip = Ui.Button(label, onClick, enabled: enabled);
        chip.FontSize = 12.5;
        chip.Padding = new Thickness(7, 0, 7, 0);
        chip.Margin = new Thickness(0, 2, 4, 2);
        chip.Foreground = Ui.BlueInk;
        return chip;
    }

    /// <summary>Draws the eye to the sergeant panel when they say something.</summary>
    private void FlashSergeant()
    {
        var flash = new ColorAnimation(Color.FromArgb(0x70, 0xFF, 0xC9, 0x40), Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF), TimeSpan.FromMilliseconds(900));
        var brush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        SergeantPanel.Background = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, flash);

        if (_chatFolded)
        {
            ChatToggle.Content = "+ (new)";
        }
    }
}
