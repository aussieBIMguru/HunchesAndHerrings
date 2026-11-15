using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HunchesAndHerrings.Audio;
using HunchesAndHerrings.Controls;
using HunchesAndHerrings.Core.Game;
using HunchesAndHerrings.Core.Generation;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Rendering;

namespace HunchesAndHerrings;

/// <summary>The centre panel: the current room, who is in it, and the interview.</summary>
public partial class MainWindow
{
    private void RefreshRoom()
    {
        PeoplePanel.Children.Clear();
        InterviewPanel.Children.Clear();
        if (_game is null)
        {
            return;
        }

        Room room = _game.CurrentRoom;
        bool canAct = _game.CanAct;

        RoomTitle.Text = room.Name;
        RoomDescription.Text = room.Description;

        var status = new List<string>();
        if (room.Id == _game.Case.CrimeSceneId)
        {
            status.Add($"A constable stands guard. {_game.Case.Victim.ShortName}'s body has not been moved.");
        }

        if (_transientWarning is not null)
        {
            status.Add(_transientWarning);
        }

        RoomStatus.Text = string.Join(" ", status);
        RoomStatus.Visibility = status.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        SearchButton.Content = _game.SearchedRooms.Contains(room.Id) ? "Search again (20 min)" : "Search room (20 min)";
        SearchButton.IsEnabled = canAct;
        WaitButton.IsEnabled = canAct;

        var here = _game.PeopleHere;
        if (_selectedPersonId is not null && here.All(p => p.Id != _selectedPersonId))
        {
            _selectedPersonId = null;
            _lastSpeech = string.Empty;
        }

        if (here.Count == 0)
        {
            PeoplePanel.Children.Add(Ui.Text("Nobody is here just now. Check the Schedule tab, or wait for someone to come by.", 13, Ui.InkFaded, italic: true));
        }

        foreach (Character person in here)
        {
            PeoplePanel.Children.Add(PersonChip(person));
        }

        var callers = _game.Case.Visitors.Where(v => _game.CanSendFor(v.Id)).ToList();
        if (callers.Count > 0 && canAct)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(Ui.Text("Not at the house: ", 12, Ui.InkFaded, italic: true));
            foreach (Character v in callers)
            {
                string id = v.Id;
                Button send = Ui.Button($"Send for {v.ShortName}", () =>
                {
                    React(_game?.SendFor(id) ?? Array.Empty<GameMessage>());
                    RefreshAll();
                    AfterAction();
                }, $"A messenger fetches {v.ShortName}, {v.RoleLabel}. {v.HeCap}'ll wait in the drawing room for a couple of hours.");
                send.FontSize = 12.5;
                row.Children.Add(send);
            }

            PeoplePanel.Children.Add(row);
        }

        if (_selectedPersonId is not null && _game.Case.Person(_selectedPersonId) is { } selected)
        {
            BuildInterview(selected);
        }
        else
        {
            InterviewPanel.Children.Add(Ui.Text(
                "Click someone above to question them. Each question takes ten minutes; answers go straight into your notebook.",
                13, Ui.InkFaded, italic: true));
        }
    }

    private UIElement PersonChip(Character person)
    {
        bool selected = person.Id == _selectedPersonId;
        bool watched = _game?.Focused.Contains(person.Id) == true;
        var content = new StackPanel { Width = 92 };
        var portrait = new Grid { Width = 60, Height = 70, HorizontalAlignment = HorizontalAlignment.Center };
        portrait.Children.Add(PortraitRenderer.Create(person, 56));
        if (watched)
        {
            portrait.Children.Add(new System.Windows.Shapes.Ellipse { Stroke = System.Windows.Media.Brushes.DarkGoldenrod, StrokeThickness = 2.5 });
        }

        content.Children.Add(portrait);
        content.Children.Add(new TextBlock
        {
            Text = person.ShortName,
            FontFamily = Ui.Hand,
            FontSize = 13,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.Ink,
            TextDecorations = _game?.Struck.Contains(person.Id) == true ? TextDecorations.Strikethrough : null,
        });
        content.Children.Add(new TextBlock
        {
            Text = person.RoleLabel,
            FontFamily = Ui.Body,
            FontSize = 11,
            FontStyle = FontStyles.Italic,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.InkFaded,
        });

        var chip = new Border
        {
            Child = content,
            BorderBrush = selected ? Ui.Ink : Ui.InkFaded,
            BorderThickness = new Thickness(selected ? 2 : 1),
            Background = selected ? Ui.PaperDark : null,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = Cursors.Hand,
            ToolTip = $"Question {person.ShortName}",
        };
        chip.MouseLeftButtonUp += (_, _) =>
        {
            _selectedPersonId = person.Id;
            _lastSpeech = string.Empty;
            _sound.Play(Sfx.Page, 0.3);
            RefreshRoom();
        };
        return chip;
    }

    private void BuildInterview(Character person)
    {
        if (_game is null)
        {
            return;
        }

        bool canAct = _game.CanAct;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var portrait = PortraitRenderer.Create(person, 78);
        portrait.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(portrait, Dock.Left);
        header.Children.Add(portrait);
        var who = new StackPanel();
        who.Children.Add(Ui.Handwriting(person.FullName, 19, weight: FontWeights.Bold));
        who.Children.Add(Ui.Text(TextBank.Capitalise(person.RoleLabel), 13, Ui.InkFaded, italic: true));
        string greeting = person.IsRival
            ? $"{person.ShortName} looks up. {_game.RivalGreeting(_game.HourIndex)}"
            : person.IsYoungChild
                ? $"{person.ShortName} peers at you from behind the furniture."
                : _game.Detective.Apply(TextBank.Greeting(person, new Random(person.PortraitSeed + _game.HourIndex)));
        who.Children.Add(Ui.Text(greeting, 13));
        bool watched = _game.Focused.Contains(person.Id);
        who.Children.Add(Ui.Button(watched ? "◯ Stop watching" : "◯ Keep an eye on them", () => ToggleFocus(person.Id),
            $"Watched suspects (up to {GameSession.MaxFocus}) are circled on the plan, so you always know roughly where they are."));
        header.Children.Add(who);
        InterviewPanel.Children.Add(header);

        var topics = new WrapPanel();
        void AddTopic(string label, Topic topic, string? target = null)
        {
            bool asked = _game.HasAsked(person.Id, topic, target);
            topics.Children.Add(Ui.Button(asked ? $"✓ {label}" : label, () => AskTopic(person.Id, topic, target),
                asked ? "Asked already: hear the answer again (no time)" : "Ten minutes", enabled: canAct || asked));
        }

        if (person.IsRival)
        {
            topics.Children.Add(Ui.Button($"Compare notes ({GameSession.CompareNotesMinutes} min)", () => AskTopic(person.Id, Topic.CompareNotes, null),
                "They may share a tip, or hand back something they've pocketed. Not everything they tell you is true.", canAct));
            topics.Children.Add(Ui.Button("Any theories? (10 min)", () => AskTopic(person.Id, Topic.RivalTheory, null), "Their guess. Take it or leave it.", canAct));
            InterviewPanel.Children.Add(topics);
            AddSpeech();
            return;
        }

        if (person.IsYoungChild)
        {
            AddTopic("What did you see last night?", Topic.Sightings);
            AddTopic("Tell me about yourself", Topic.Self);
            InterviewPanel.Children.Add(topics);
            InterviewPanel.Children.Add(Ui.Text("Children don't tell the whole story, but what they do say is true.", 11, Ui.InkFaded, italic: true));
            AddSpeech();
            return;
        }

        AddTopic("Where were you last night?", Topic.LastNight);
        AddTopic("What did you see?", Topic.Sightings);
        AddTopic("Tell me about yourself", Topic.Self);
        AddTopic($"About {_game.Case.Victim.ShortName}", Topic.Victim);
        AddTopic("Where is everyone today?", Topic.Whereabouts);
        if (_game.CanConfront(person.Id))
        {
            AddTopic("Confront them with their secret", Topic.Confront);
        }

        InterviewPanel.Children.Add(topics);

        // Ask about another member of the household.
        var aboutRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        aboutRow.Children.Add(Ui.Handwriting("Ask about ", 15));
        var others = _game.Case.Suspects.Where(s => s.Id != person.Id).ToList();
        var combo = new ComboBox
        {
            ItemsSource = others.Select(o => _game.HasAsked(person.Id, Topic.AboutPerson, o.Id) ? $"✓ {o.ShortName}" : o.ShortName).ToList(),
            SelectedIndex = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        aboutRow.Children.Add(combo);
        aboutRow.Children.Add(Ui.Button("Ask", () =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < others.Count)
            {
                AskTopic(person.Id, Topic.AboutPerson, others[combo.SelectedIndex].Id);
            }
        }, "Ten minutes", canAct || others.Any(o => _game.HasAsked(person.Id, Topic.AboutPerson, o.Id))));
        InterviewPanel.Children.Add(aboutRow);

        InterviewPanel.Children.Add(Ui.Button($"Leave {person.ShortName} to the sergeant", () => SendChat($"Question {person.ShortName}"),
            $"{_game.Case.SergeantName} questions them while you get on (50 min). Results may be partial.",
            canAct && _game.Sergeant is null));

        AddSpeech();
    }

    private void AddSpeech()
    {
        if (!string.IsNullOrEmpty(_lastSpeech))
        {
            var speech = new TextBlock
            {
                Text = _lastSpeech,
                FontFamily = Ui.Hand,
                FontSize = 16,
                Foreground = Ui.BlueInk,
                TextWrapping = TextWrapping.Wrap,
            };
            InterviewPanel.Children.Add(Ui.Card(speech, Ui.InkFaded, new Thickness(0, 8, 0, 0)));
        }
    }

    private void ToggleFocus(string personId)
    {
        if (_game is null)
        {
            return;
        }

        if (!_game.ToggleFocus(personId))
        {
            _transientWarning = $"You can only keep a close eye on {GameSession.MaxFocus} suspects at once. Stop watching someone first.";
        }
        else
        {
            _sound.Play(Audio.Sfx.Quill, 0.35);
        }

        RefreshAll();
    }

    private void AskTopic(string personId, Topic topic, string? target)
    {
        if (_game is null)
        {
            return;
        }

        InterviewResult result = _game.Ask(personId, topic, target);
        if (!string.IsNullOrEmpty(result.Speech))
        {
            _lastSpeech = result.Speech;
            if (!result.AlreadyAsked)
            {
                _sound.Play(Sfx.Page, 0.35);
            }
        }

        React(result.Messages);
        RefreshAll();
        AfterAction();
    }
}
