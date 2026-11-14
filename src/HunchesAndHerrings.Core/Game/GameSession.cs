using HunchesAndHerrings.Core.Generation;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Game;

/// <summary>
/// A case in play: the clock, the inspector's position, what has been found and asked, the journal,
/// the sergeant's errands and conversation, and the Deduction meter. Every player action is a method that
/// returns what happened; nothing here throws for ordinary misuse. Invalid actions just return a warning.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>How many suspects you can keep a close eye on at once.</summary>
    public const int MaxFocus = 3;

    private readonly Dictionary<string, Clue> _clueIndex = new();
    private readonly HashSet<string> _discovered = new();
    private readonly List<string> _discoveredOrder = new();
    private readonly List<JournalItem> _journal = new();
    private readonly List<ChatLine> _chat = new();
    private readonly List<TrailStep> _trail = new();
    private List<Contradiction> _contradictions = new();

    public GameSession(CaseFile caseFile, Detective? detective = null)
    {
        Case = caseFile;
        Detective = detective ?? Detective.Default(Gender.Male);
        Settings = caseFile.Settings;
        Minute = GameRules.DayStartMinute;
        EndMinute = Settings.EndHour * 60;
        Deduction = Settings.StartingDeduction;
        foreach (Clue clue in caseFile.Clues)
        {
            _clueIndex[clue.Id] = clue;
        }

        _trail.Add(new TrailStep(RoomId, Minute, true));
        Observe();
        Record(MessageKind.Info, $"You arrive at {caseFile.Manor.Name} with {caseFile.SergeantName}.");
        _chat.Add(new ChatLine(false, Detective.Apply(SergeantVoice.Greeting(Case.SergeantStyle)), Minute));
    }

    // ================================================================== state

    public CaseFile Case { get; }

    /// <summary>The player, and how the household addresses them.</summary>
    public Detective Detective { get; }
    public DifficultySettings Settings { get; }
    public int Minute { get; private set; }
    public int EndMinute { get; }
    public string RoomId { get; private set; } = RoomIds.Hall;
    public int Deduction { get; private set; }
    public int RescuesUsed { get; private set; }
    public SergeantTask? Sergeant { get; private set; }
    public Verdict? Verdict { get; private set; }
    public string Notes { get; set; } = string.Empty;

    public HashSet<string> SearchedRooms { get; } = new();
    public HashSet<string> AskedKeys { get; } = new();
    public HashSet<string> SergeantFound { get; } = new();
    public HashSet<string> Struck { get; } = new();

    /// <summary>Journal keys the inspector has circled.</summary>
    public HashSet<string> Circled { get; } = new();

    /// <summary>Suspects the inspector is keeping a close eye on (up to <see cref="MaxFocus"/>).</summary>
    public HashSet<string> Focused { get; } = new();

    public Dictionary<string, bool> Scrutiny { get; } = new();
    public Dictionary<string, Sighting> LastSeen { get; } = new();
    public List<Clue> ExtraClues { get; } = new();
    public List<LogEntry> Log { get; } = new();

    private int RescueCheckpoint { get; set; }
    private int ActionCount { get; set; }

    public IReadOnlyList<Contradiction> Contradictions => _contradictions;
    public IReadOnlyList<JournalItem> Journal => _journal;
    public IReadOnlyList<ChatLine> Chat => _chat;

    /// <summary>Every room walked through, in order; <see cref="TrailStep.IsStop"/> marks where you stopped.</summary>
    public IReadOnlyList<TrailStep> Trail => _trail;

    /// <summary>Where the sergeant is right now: with you, in the room being searched, or with the person being questioned.</summary>
    public string SergeantRoomId => Sergeant switch
    {
        null => RoomId,
        { Kind: SergeantTaskKind.Search } task => task.TargetId,
        { } task => LocationOf(task.TargetId) is { } room && Case.Manor.Contains(room) ? room : RoomId,
    };

    public IEnumerable<Clue> DiscoveredClues => _discoveredOrder.Select(id => _clueIndex[id]);

    public bool IsTimeUp => Minute >= EndMinute;
    public bool IsOver => Verdict is not null;
    public bool CanAct => !IsOver && !IsTimeUp;
    public int MinutesLeft => Math.Max(0, EndMinute - Minute);

    /// <summary>Fraction of the day used, 0..1.</summary>
    public double TimeUsed => Math.Clamp((Minute - GameRules.DayStartMinute) / (double)(EndMinute - GameRules.DayStartMinute), 0, 1);

    public Room CurrentRoom => Case.Manor[RoomId];

    public int HourIndex => Math.Clamp(Minute / 60 - GameRules.DayStartHour, 0, GameRules.DayHours - 1);

    /// <summary>Last schedule hour (index) that falls before the Chief Constable arrives.</summary>
    public int LastHourIndex => Math.Clamp(Settings.EndHour - 1 - GameRules.DayStartHour, 0, GameRules.DayHours - 1);

    public bool IsDiscovered(string clueId) => _discovered.Contains(clueId);

    public Clue? GetClue(string clueId) => _clueIndex.GetValueOrDefault(clueId);

    public Contradiction? GetContradiction(string key) => _contradictions.FirstOrDefault(c => c.Key == key);

    public string LocationOf(string characterId) => LocationAt(characterId, HourIndex);

    public string LocationAt(string characterId, int hourIndex) =>
        Case.Day.TryGetValue(characterId, out string[]? hours) ? hours[Math.Clamp(hourIndex, 0, hours.Length - 1)] : RoomIds.Away;

    public IReadOnlyList<Character> PeopleIn(string roomId) =>
        Case.Suspects.Where(c => LocationOf(c.Id) == roomId).ToList();

    public IReadOnlyList<Character> PeopleHere => PeopleIn(RoomId);

    public static string AskKey(string personId, Topic topic, string? targetId) => $"{personId}|{topic}|{targetId}";

    public bool HasAsked(string personId, Topic topic, string? targetId = null) => AskedKeys.Contains(AskKey(personId, topic, targetId));

    public string SergeantStatus => Sergeant is null
        ? "at your side"
        : Sergeant.Kind == SergeantTaskKind.Search
            ? $"searching the {Case.Manor.NameOf(Sergeant.TargetId)} (back {Clock.Format(Sergeant.DueMinute)})"
            : $"questioning {Case.NameOf(Sergeant.TargetId)} (back {Clock.Format(Sergeant.DueMinute)})";

    // ================================================================== whereabouts

    /// <summary>
    /// Where everyone is, or is expected to be, at a given hour. Seen and told whereabouts are exact;
    /// rough ones come from household routine and give the room only on Novice or for focused suspects.
    /// </summary>
    public IReadOnlyList<Whereabout> WhereaboutsAt(int hourIndex)
    {
        int hour = GameRules.DayStartHour + hourIndex;
        var told = DiscoveredClues.SelectMany(c => c.Facts.OfType<ScheduleFact>()).ToList();
        var result = new List<Whereabout>();

        foreach (Character s in Case.Suspects)
        {
            string truth = LocationAt(s.Id, hourIndex);
            bool precise = Settings.Difficulty == Difficulty.Novice || Focused.Contains(s.Id);

            if (hourIndex == HourIndex && truth == RoomId)
            {
                result.Add(new Whereabout(s, truth, Certainty.Seen, true));
            }
            else if (LastSeen.TryGetValue(s.Id, out Sighting? seen) && seen.Minute / 60 == hour)
            {
                result.Add(new Whereabout(s, seen.RoomId, Certainty.Seen, true));
            }
            else if (told.LastOrDefault(f => f.CharacterId == s.Id && hour >= f.FromHour && hour < f.ToHour) is { } fact)
            {
                result.Add(new Whereabout(s, fact.RoomId, Certainty.Told, true));
            }
            else
            {
                result.Add(new Whereabout(s, truth, Certainty.Rough, precise));
            }
        }

        return result;
    }

    // ================================================================== player marks

    public void ToggleStrike(string characterId)
    {
        if (!Struck.Remove(characterId))
        {
            Struck.Add(characterId);
        }
    }

    public void ToggleCircle(string journalKey)
    {
        if (!Circled.Remove(journalKey))
        {
            Circled.Add(journalKey);
        }
    }

    /// <summary>Starts or stops keeping an eye on a suspect. Returns false when the limit is reached.</summary>
    public bool ToggleFocus(string characterId)
    {
        if (Focused.Remove(characterId))
        {
            return true;
        }

        if (Focused.Count >= MaxFocus)
        {
            return false;
        }

        Focused.Add(characterId);
        return true;
    }

    // ================================================================== actions

    /// <summary>Walks to any room by the quickest route.</summary>
    public IReadOnlyList<GameMessage> MoveTo(string roomId)
    {
        var msgs = new List<GameMessage>();
        if (!Guard(msgs) || roomId == RoomId)
        {
            return msgs;
        }

        Route? route = Case.Manor.FindRoute(RoomId, roomId);
        if (route is null)
        {
            return Warn(msgs, "There's no way through to there.");
        }

        foreach (string step in route.Path.Skip(1))
        {
            _trail.Add(new TrailStep(step, Minute, step == roomId));
        }

        RoomId = roomId;
        Emit(msgs, MessageKind.Info, $"You make your way to the {CurrentRoom.Name} ({route.Minutes} min).");
        Advance(route.Minutes, msgs);
        Observe();

        var here = PeopleHere;
        if (here.Count > 0)
        {
            Emit(msgs, MessageKind.Info, $"Here: {TextBank.JoinNames(here.Select(p => p.ShortName).ToList())}.");
        }

        return msgs;
    }

    /// <summary>Searches the current room thoroughly.</summary>
    public IReadOnlyList<GameMessage> Search()
    {
        var msgs = new List<GameMessage>();
        if (!Guard(msgs))
        {
            return msgs;
        }

        var found = Case.Clues.Where(c => c.RoomId == RoomId && !_discovered.Contains(c.Id)).ToList();
        SearchedRooms.Add(RoomId);
        Emit(msgs, MessageKind.Info, $"You search the {CurrentRoom.Name} from top to bottom.");
        if (found.Count == 0)
        {
            Emit(msgs, MessageKind.Info, "You find nothing new.");
        }

        foreach (Clue clue in found)
        {
            Discover(clue, msgs, viaSergeant: false);
        }

        Advance(GameRules.SearchMinutes, msgs);
        return msgs;
    }

    /// <summary>Asks someone in the room about a topic.</summary>
    public InterviewResult Ask(string personId, Topic topic, string? targetId = null)
    {
        var msgs = new List<GameMessage>();
        Character? person = Case.Person(personId);
        if (person is null || person.IsVictim)
        {
            return new InterviewResult(string.Empty, Warn(msgs, "There's nobody by that name."), false);
        }

        string key = AskKey(personId, topic, targetId);
        bool already = AskedKeys.Contains(key);
        string speech = ComposeSpeech(person, topic, targetId, key);
        if (already)
        {
            return new InterviewResult(speech, msgs, true);
        }

        if (!Guard(msgs))
        {
            return new InterviewResult(string.Empty, msgs, false);
        }

        if (LocationOf(personId) != RoomId)
        {
            return new InterviewResult(string.Empty, Warn(msgs, $"{person.ShortName} isn't here."), false);
        }

        AskedKeys.Add(key);
        foreach (Clue clue in StatementClues(personId, topic, targetId))
        {
            Discover(clue, msgs, viaSergeant: false);
        }

        Advance(GameRules.QuestionMinutes, msgs);
        return new InterviewResult(speech, msgs, false);
    }

    /// <summary>Lets time pass, e.g. to wait for someone to arrive.</summary>
    public IReadOnlyList<GameMessage> Wait()
    {
        var msgs = new List<GameMessage>();
        if (!Guard(msgs))
        {
            return msgs;
        }

        Emit(msgs, MessageKind.Info, $"You wait a while in the {CurrentRoom.Name}.");
        Advance(GameRules.WaitMinutes, msgs);
        Observe();
        return msgs;
    }

    public IReadOnlyList<GameMessage> SendSergeantToSearch(string roomId)
    {
        var msgs = new List<GameMessage>();
        if (!Guard(msgs) || !SergeantFree(msgs))
        {
            return msgs;
        }

        Sergeant = new SergeantTask(SergeantTaskKind.Search, roomId, Minute + GameRules.SergeantSearchMinutes);
        SergeantSay(msgs, SergeantVoice.Assigned(Case.SergeantStyle, $"search the {Case.Manor.NameOf(roomId)}", RuntimeRng()));
        ActionCount++;
        return msgs;
    }

    public IReadOnlyList<GameMessage> SendSergeantToInterview(string personId)
    {
        var msgs = new List<GameMessage>();
        if (!Guard(msgs) || !SergeantFree(msgs))
        {
            return msgs;
        }

        Sergeant = new SergeantTask(SergeantTaskKind.Interview, personId, Minute + GameRules.SergeantInterviewMinutes);
        SergeantSay(msgs, SergeantVoice.Assigned(Case.SergeantStyle, $"have a word with {Case.NameOf(personId)}", RuntimeRng()));
        ActionCount++;
        return msgs;
    }

    /// <summary>Spends Deduction for a nudge toward an important undiscovered clue.</summary>
    public IReadOnlyList<GameMessage> Hunch()
    {
        var msgs = new List<GameMessage>();
        if (IsOver)
        {
            return msgs;
        }

        if (Deduction < Settings.HunchCost)
        {
            return Warn(msgs, $"A hunch needs {Settings.HunchCost} Deduction. Gather more evidence first.");
        }

        Clue? target = BestUndiscoveredKeyClue(RuntimeRng());
        if (target is null)
        {
            return Warn(msgs, "Your instincts are quiet. You may already hold everything you need.");
        }

        Deduction -= Settings.HunchCost;
        Emit(msgs, MessageKind.Hint, HintText(target));
        ActionCount++;
        return msgs;
    }

    /// <summary>Spends Deduction to judge whether a statement rings true.</summary>
    public IReadOnlyList<GameMessage> Scrutinise(string clueId)
    {
        var msgs = new List<GameMessage>();
        Clue? clue = GetClue(clueId);
        if (clue is null || !_discovered.Contains(clueId) || IsOver)
        {
            return msgs;
        }

        if (!clue.IsStatement)
        {
            return Warn(msgs, "Only spoken accounts can be scrutinised.");
        }

        if (Scrutiny.ContainsKey(clueId))
        {
            return msgs;
        }

        if (Deduction < Settings.ScrutinyCost)
        {
            return Warn(msgs, $"Scrutiny needs {Settings.ScrutinyCost} Deduction.");
        }

        Deduction -= Settings.ScrutinyCost;
        Random rng = RuntimeRng();
        bool correct = rng.NextDouble() < Settings.ScrutinyAccuracy;
        bool ringsTrue = correct ? clue.IsTruthful : !clue.IsTruthful;
        Scrutiny[clueId] = ringsTrue;
        ActionCount++;

        string speaker = clue.SpeakerId is not null ? Case.NameOf(clue.SpeakerId) : "the sergeant";
        Emit(msgs, MessageKind.Hint, ringsTrue
            ? $"You turn {speaker}'s account over in your mind. It rings true."
            : $"You turn {speaker}'s account over in your mind. Something in it rings false.");
        return msgs;
    }

    /// <summary>Names the murderer and ends the case.</summary>
    public Verdict Accuse(string personId, MotiveKind motive, string? evidenceId)
    {
        if (Verdict is not null)
        {
            return Verdict;
        }

        Character murderer = Case.Murderer;
        bool correct = personId == murderer.Id;
        bool motiveOk = correct && motive == murderer.Motive;
        bool evidenceOk = correct && evidenceId is not null && Case.KeyEvidenceIds.Contains(evidenceId);
        int score = (motiveOk ? 1 : 0) + (evidenceOk ? 1 : 0);

        (string grade, string headline) = (correct, score) switch
        {
            (false, _) => ("F", "The wrong person stands in the dock."),
            (true, 2) => ("A", "Case closed. The jury never left the box."),
            (true, 1) => ("B", "A conviction, though defence counsel made heavy weather of it."),
            _ => ("C", "Guilty, by the narrowest of margins. You were lucky."),
        };

        Verdict = new Verdict(correct, motiveOk, evidenceOk, grade, headline, personId, motive, evidenceId);
        Record(correct ? MessageKind.Info : MessageKind.Warning, $"You accuse {Case.NameOf(personId)}. {headline}");
        return Verdict;
    }

    // ================================================================== internals

    private bool Guard(List<GameMessage> msgs)
    {
        if (IsOver)
        {
            return false;
        }

        if (IsTimeUp)
        {
            Warn(msgs, "Time is up. The Chief Constable wants a name.");
            return false;
        }

        return true;
    }

    private bool SergeantFree(List<GameMessage> msgs)
    {
        if (Sergeant is null)
        {
            return true;
        }

        SergeantSay(msgs, $"{SergeantVoice.Busy(Case.SergeantStyle)} I'm {SergeantStatus}.");
        return false;
    }

    /// <summary>Deterministic per-action randomness, so a loaded game plays out the same way.</summary>
    private Random RuntimeRng() => new(unchecked(Case.Seed * 7919 + ActionCount * 104729 + Minute));

    private void Emit(List<GameMessage> msgs, MessageKind kind, string text)
    {
        msgs.Add(new GameMessage(kind, text));
        Record(kind, text);
    }

    /// <summary>The sergeant says something: it goes in the chat and the case log.</summary>
    private void SergeantSay(List<GameMessage> msgs, string text)
    {
        string line = Detective.Apply(text);
        _chat.Add(new ChatLine(false, line, Minute));
        Emit(msgs, MessageKind.Sergeant, $"{Case.SergeantName}: \"{line}\"");
    }

    private List<GameMessage> Warn(List<GameMessage> msgs, string text)
    {
        msgs.Add(new GameMessage(MessageKind.Warning, text));
        return msgs;
    }

    private void Record(MessageKind kind, string text) => Log.Add(new LogEntry(Minute, kind, text));

    private void Observe()
    {
        foreach (Character person in PeopleHere)
        {
            LastSeen[person.Id] = new Sighting(RoomId, Minute);
        }
    }

    private void Advance(int minutes, List<GameMessage> msgs)
    {
        int before = Minute;
        Minute = Math.Min(Minute + minutes, EndMinute);
        ActionCount++;

        if (Minute / 60 != before / 60)
        {
            Emit(msgs, MessageKind.Time, $"Somewhere in the house a clock strikes {Clock.HourWords(Minute / 60)}.");
        }

        ResolveSergeant(msgs);
        CheckRescue(msgs);

        if (IsTimeUp)
        {
            Emit(msgs, MessageKind.Warning, "Time is up. The Chief Constable's carriage is at the door, and he wants a name.");
        }
        else if (EndMinute - Minute <= 60 && EndMinute - before > 60)
        {
            Emit(msgs, MessageKind.Warning, "One hour left before you must name the murderer.");
        }
    }

    private bool Discover(Clue clue, List<GameMessage> msgs, bool viaSergeant)
    {
        if (!_discovered.Add(clue.Id))
        {
            return false;
        }

        _discoveredOrder.Add(clue.Id);
        _journal.Add(new JournalItem(JournalItem.CluePrefix + clue.Id, Minute));
        if (viaSergeant)
        {
            SergeantFound.Add(clue.Id);
        }

        Deduction++;
        Emit(msgs, MessageKind.Clue, $"Noted: {clue.Title}.");
        RefreshContradictions(msgs);
        return true;
    }

    private void RefreshContradictions(List<GameMessage>? msgs)
    {
        var before = _contradictions.Select(c => c.Key).ToHashSet();
        _contradictions = ContradictionFinder.Find(Case, DiscoveredClues.ToList());
        if (msgs is null)
        {
            return;
        }

        foreach (Contradiction c in _contradictions.Where(c => !before.Contains(c.Key)))
        {
            Deduction += 2;
            _journal.Add(new JournalItem(JournalItem.ClashPrefix + c.Key, Minute));
            Emit(msgs, MessageKind.Contradiction, $"Contradiction! {c.Description}");
        }
    }

    private IEnumerable<Clue> StatementClues(string personId, Topic topic, string? targetId) =>
        Case.Clues.Where(c => c.SpeakerId == personId && c.Topic == topic && c.TargetId == targetId);

    private string ComposeSpeech(Character person, Topic topic, string? targetId, string key)
    {
        var rng = new Random(StableHash.Of(key) ^ Case.Seed);
        var lines = StatementClues(person.Id, topic, targetId).Select(c => c.Spoken).Where(s => !string.IsNullOrEmpty(s)).ToList();
        string body = lines.Count > 0
            ? string.Join(" ", lines)
            : TextBank.Fallback(topic, person, targetId is null ? null : Case.Person(targetId), rng);
        string opener = TextBank.Opener(person, rng);
        return Detective.Apply(string.IsNullOrEmpty(opener) ? $"\"{body}\"" : $"\"{opener} {body}\"");
    }

    private void ResolveSergeant(List<GameMessage> msgs)
    {
        if (Sergeant is null || Minute < Sergeant.DueMinute)
        {
            return;
        }

        SergeantTask task = Sergeant;
        Sergeant = null;
        Random rng = RuntimeRng();
        var noted = new List<string>();

        if (task.Kind == SergeantTaskKind.Search)
        {
            var candidates = Case.Clues.Where(c => c.RoomId == task.TargetId && !_discovered.Contains(c.Id)).ToList();
            var found = candidates.Where(_ => rng.NextDouble() < Settings.SergeantSkill).ToList();
            SergeantSay(msgs, $"Back from the {Case.Manor.NameOf(task.TargetId)}. {SergeantVoice.Found(Case.SergeantStyle, found.Count, rng)}");
            foreach (Clue clue in found)
            {
                Discover(clue, msgs, viaSergeant: true);
                noted.Add(clue.Title);
            }
        }
        else
        {
            Character? person = Case.Person(task.TargetId);
            if (person is null || LocationOf(task.TargetId) == RoomIds.Away)
            {
                SergeantSay(msgs, $"Couldn't find {Case.NameOf(task.TargetId)} anywhere. Gone to the village, they say.");
                return;
            }

            var topics = new[] { Topic.LastNight, Topic.Sightings, Topic.Victim };
            var candidates = Case.Clues
                .Where(c => c.SpeakerId == task.TargetId && c.Topic is { } t && topics.Contains(t) && !_discovered.Contains(c.Id))
                .ToList();
            var found = candidates.Where(_ => rng.NextDouble() < Settings.SergeantSkill).ToList();
            SergeantSay(msgs, $"I've had a word with {person.ShortName}. {SergeantVoice.Found(Case.SergeantStyle, found.Count, rng)}");

            foreach (Clue clue in found)
            {
                bool misreport = rng.NextDouble() < Settings.SergeantErrorChance && clue.Facts.OfType<LocationFact>().Any();
                Clue entry = clue;
                if (misreport)
                {
                    entry = Misreport(clue, rng);
                    ExtraClues.Add(entry);
                    _clueIndex[entry.Id] = entry;
                }

                Discover(entry, msgs, viaSergeant: true);
                noted.Add(entry.Title);
            }
        }

        if (noted.Count > 0)
        {
            SergeantSay(msgs, $"It's all in the journal: {string.Join("; ", noted)}.");
        }

        if (rng.NextDouble() < 0.3)
        {
            SergeantSay(msgs, SergeantVoice.Theory(Case.SergeantStyle, rng.Pick(Case.Suspects).ShortName, rng));
        }
    }

    /// <summary>A garbled copy of a statement: one location is noted wrongly.</summary>
    private Clue Misreport(Clue original, Random rng)
    {
        var facts = original.Facts.ToList();
        int index = facts.FindIndex(f => f is LocationFact);
        var loc = (LocationFact)facts[index];
        var rooms = ManorLayout.SharedRooms.Where(r => r != loc.RoomId).ToList();
        facts[index] = loc with { RoomId = rng.Pick(rooms) };

        var notes = facts.OfType<LocationFact>()
            .Select(f => $"{Case.NameOf(f.CharacterId)} in the {Case.Manor.NameOf(f.RoomId)} at {NightSlots.Short(f.Slot)}");

        return new Clue
        {
            Id = $"x{ExtraClues.Count}",
            Kind = ClueKind.Report,
            Title = $"{Case.SergeantName}'s notes: {original.Title}",
            Text = $"Per the sergeant: {string.Join("; ", notes)}.",
            Spoken = original.Spoken,
            Facts = facts,
            SpeakerId = original.SpeakerId,
            Topic = original.Topic,
            TargetId = original.TargetId,
            IsTruthful = false,
            IsKey = false,
        };
    }

    private void CheckRescue(List<GameMessage> msgs)
    {
        double[] checkpoints = { 0.5, 0.75 };
        while (RescueCheckpoint < checkpoints.Length && TimeUsed >= checkpoints[RescueCheckpoint])
        {
            double threshold = checkpoints[RescueCheckpoint];
            RescueCheckpoint++;
            if (RescuesUsed >= Settings.MaxRescues || ProgressSolver.Progress(Case, DiscoveredClues) >= threshold)
            {
                continue;
            }

            Random rng = RuntimeRng();
            Clue? clue = BestUndiscoveredKeyClue(rng);
            if (clue is null)
            {
                continue;
            }

            RescuesUsed++;
            SergeantSay(msgs, SergeantVoice.Rescue(Case.SergeantStyle, rng));
            Discover(clue, msgs, viaSergeant: true);
        }
    }

    /// <summary>The most useful key clue the player hasn't found yet.</summary>
    private Clue? BestUndiscoveredKeyClue(Random rng)
    {
        var undiscovered = Case.Clues.Where(c => c.IsKey && !_discovered.Contains(c.Id)).ToList();
        if (undiscovered.Count == 0)
        {
            return null;
        }

        var scene = undiscovered.Where(c => c.Facts.Any(f => f is SceneFact or TimeOfDeathFact)).ToList();
        if (scene.Count > 0)
        {
            return scene[0];
        }

        var eliminated = ProgressSolver.Eliminated(Case, DiscoveredClues);
        var helpful = Case.Eliminations
            .Where(e => !eliminated.Contains(e.Key))
            .SelectMany(e => e.Value.ClueIds)
            .Distinct()
            .Select(id => _clueIndex[id])
            .Where(c => !_discovered.Contains(c.Id))
            .ToList();
        if (helpful.Count > 0)
        {
            return rng.Pick(helpful);
        }

        var evidence = undiscovered.Where(c => Case.KeyEvidenceIds.Contains(c.Id)).ToList();
        return evidence.Count > 0 ? rng.Pick(evidence) : rng.Pick(undiscovered);
    }

    private string HintText(Clue clue)
    {
        if (clue.RoomId is not null)
        {
            return $"A hunch: the {Case.Manor.NameOf(clue.RoomId)} hasn't given up all its secrets.";
        }

        string who = clue.SpeakerId is not null ? Case.NameOf(clue.SpeakerId) : "someone";
        string about = clue.Topic switch
        {
            Topic.LastNight => "where they were last night",
            Topic.Sightings => "what they saw last night",
            Topic.Self => "themselves",
            Topic.Victim => Case.Victim.ShortName,
            Topic.AboutPerson when clue.TargetId is not null => Case.NameOf(clue.TargetId),
            Topic.Whereabouts => "everyone's movements today",
            _ => "something",
        };

        return $"A hunch: {who} knows more than they've said about {about}.";
    }

    // ================================================================== save / load

    public SaveData ToSaveData() => new()
    {
        Seed = Case.Seed,
        Difficulty = Case.Difficulty,
        Options = Case.Options,
        Detective = Detective,
        Minute = Minute,
        RoomId = RoomId,
        Discovered = _discoveredOrder.ToList(),
        SergeantFound = SergeantFound.ToList(),
        SearchedRooms = SearchedRooms.ToList(),
        AskedKeys = AskedKeys.ToList(),
        Deduction = Deduction,
        RescuesUsed = RescuesUsed,
        RescueCheckpoint = RescueCheckpoint,
        ActionCount = ActionCount,
        Sergeant = Sergeant,
        ExtraClues = ExtraClues.ToList(),
        Scrutiny = new Dictionary<string, bool>(Scrutiny),
        Struck = Struck.ToList(),
        Circled = Circled.ToList(),
        Focused = Focused.ToList(),
        Notes = Notes,
        LastSeen = new Dictionary<string, Sighting>(LastSeen),
        Log = Log.ToList(),
        Journal = _journal.ToList(),
        Chat = _chat.ToList(),
        Trail = _trail.ToList(),
    };

    /// <summary>Rebuilds a session from saved progress, regenerating the case from its seed and options.</summary>
    public static GameSession FromSaveData(SaveData data)
    {
        var session = new GameSession(CaseGenerator.Generate(data.Seed, data.Options ?? new CaseOptions(data.Difficulty)), data.Detective);
        session.Log.Clear();
        session.LastSeen.Clear();
        session._chat.Clear();

        foreach (Clue extra in data.ExtraClues)
        {
            session.ExtraClues.Add(extra);
            session._clueIndex[extra.Id] = extra;
        }

        foreach (string id in data.Discovered.Where(session._clueIndex.ContainsKey))
        {
            if (session._discovered.Add(id))
            {
                session._discoveredOrder.Add(id);
            }
        }

        session.Minute = Math.Clamp(data.Minute, GameRules.DayStartMinute, session.EndMinute);
        session.RoomId = session.Case.Manor.Contains(data.RoomId) ? data.RoomId : RoomIds.Hall;
        session.Deduction = Math.Max(0, data.Deduction);
        session.RescuesUsed = data.RescuesUsed;
        session.RescueCheckpoint = data.RescueCheckpoint;
        session.ActionCount = data.ActionCount;
        session.Sergeant = data.Sergeant;
        session.Notes = data.Notes ?? string.Empty;
        session.SergeantFound.UnionWith(data.SergeantFound);
        session.SearchedRooms.UnionWith(data.SearchedRooms);
        session.AskedKeys.UnionWith(data.AskedKeys);
        session.Struck.UnionWith(data.Struck);
        session.Circled.UnionWith(data.Circled);
        session.Focused.UnionWith(data.Focused.Take(MaxFocus));
        foreach (var (k, v) in data.Scrutiny)
        {
            session.Scrutiny[k] = v;
        }

        foreach (var (k, v) in data.LastSeen)
        {
            session.LastSeen[k] = v;
        }

        session.Log.AddRange(data.Log);
        session._chat.AddRange(data.Chat);
        if (data.Trail.Count > 0)
        {
            session._trail.Clear();
            session._trail.AddRange(data.Trail.Where(t => session.Case.Manor.Contains(t.RoomId)));
        }
        session.RefreshContradictions(null);

        // Journal order: as saved if available, otherwise clues then contradictions.
        var validKeys = session._discoveredOrder.Select(id => JournalItem.CluePrefix + id)
            .Concat(session._contradictions.Select(c => JournalItem.ClashPrefix + c.Key))
            .ToHashSet();
        session._journal.AddRange(data.Journal.Where(j => validKeys.Contains(j.Key)));
        foreach (string key in validKeys.Where(k => session._journal.All(j => j.Key != k)))
        {
            session._journal.Add(new JournalItem(key, session.Minute));
        }

        return session;
    }
}
