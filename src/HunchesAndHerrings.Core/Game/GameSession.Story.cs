using HunchesAndHerrings.Core.Generation;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Game;

/// <summary>The rival detective, confronting people with their secrets, sending for callers, and circled phrases.</summary>
public sealed partial class GameSession
{
    /// <summary>Minutes spent comparing notes with the rival.</summary>
    public const int CompareNotesMinutes = 15;

    /// <summary>Minutes for a messenger to fetch a caller, at the least.</summary>
    public const int SendForMinutes = 5;

    /// <summary>How many of the rival's tips you've had.</summary>
    public int RivalTipsGiven { get; private set; }

    /// <summary>Pocketed clues the rival has handed back.</summary>
    public HashSet<string> RivalReturned { get; } = new();

    /// <summary>Times you've asked the rival for something they're keeping.</summary>
    public int RivalPleas { get; private set; }

    /// <summary>Callers you've sent for.</summary>
    public IReadOnlyList<Summons> Summoned => _summons;


    // ================================================================== confronting secrets

    /// <summary>True when you hold a clue that exposes this person's secret.</summary>
    public bool CanConfront(string personId) => DiscoveredClues.Any(c => c.SecretOf == personId);

    // ================================================================== the rival

    /// <summary>True while the rival has a clue pocketed (taken, and not yet handed back).</summary>
    private bool RivalHolds(string clueId) =>
        Case.Rival is { } rival
        && rival.Pockets.Any(p => p.ClueId == clueId && p.HourIndex <= HourIndex)
        && !RivalReturned.Contains(clueId);

    /// <summary>A warning when the rival has someone closeted this hour, otherwise null.</summary>
    private string? ClosetedWithRival(string personId)
    {
        if (Case.Rival is not { } rival || personId == rival.Detective.Id)
        {
            return null;
        }

        return rival.Closeted.TryGetValue(HourIndex, out string? who) && who == personId && Case.Person(personId) is { } person
            ? Detective.Apply(StoryBank.RivalCloseted(rival.Detective, person))
            : null;
    }

    private InterviewResult AskRival(Topic topic, List<GameMessage> msgs)
    {
        RivalPlan rival = Case.Rival!;
        Character r = rival.Detective;
        Random rng = RuntimeRng();
        string speech;

        if (topic == Topic.RivalTheory)
        {
            bool insightful = rng.NextDouble() < rival.Insight;
            if (insightful && Case.IsAccident)
            {
                speech = StoryBank.RivalTheoryAccident(rival.Style);
            }
            else
            {
                var pool = insightful ? Case.Culprits.ToList() : Case.Suspects.Where(s => !Case.IsCulprit(s.Id)).ToList();
                Character named = rng.Pick(pool.Count > 0 ? pool : Case.Suspects.ToList());
                speech = StoryBank.RivalTheory(rival.Style, named.ShortName, rng);
            }

            Advance(GameRules.QuestionMinutes, msgs);
            return new InterviewResult(Detective.Apply($"\"{speech}\""), msgs, false);
        }

        // Comparing notes: first anything they've pocketed, then their tips.
        var held = rival.Pockets.Where(p => RivalHolds(p.ClueId)).ToList();
        if (held.Count > 0 && (rival.Generous || RivalPleas >= 1))
        {
            speech = StoryBank.RivalReturns(rival.Style);
            RivalReturned.Add(held[0].ClueId);
            Discover(_clueIndex[held[0].ClueId], msgs, viaSergeant: false);
        }
        else if (held.Count > 0)
        {
            RivalPleas++;
            speech = StoryBank.RivalKeeps(rival.Style);
        }
        else if (RivalTipsGiven < rival.TipIds.Count)
        {
            Clue tip = _clueIndex[rival.TipIds[RivalTipsGiven]];
            RivalTipsGiven++;
            speech = tip.Spoken ?? tip.Text;
            Discover(tip, msgs, viaSergeant: false);
        }
        else
        {
            speech = StoryBank.RivalNothing(rival.Style);
        }

        Advance(CompareNotesMinutes, msgs);
        return new InterviewResult(Detective.Apply($"\"{speech}\""), msgs, false);
    }

    /// <summary>What the rival says when you first meet them in a room.</summary>
    public string RivalGreeting(int salt) =>
        Case.Rival is { } rival ? Detective.Apply(StoryBank.RivalGreeting(rival.Style, new Random(Case.Seed + salt))) : string.Empty;

    /// <summary>Now and then, word reaches you of what the rival is up to.</summary>
    private void RivalRumour(List<GameMessage> msgs)
    {
        if (Case.Rival is not { } rival)
        {
            return;
        }

        Random rng = RuntimeRng();
        if (rng.NextDouble() > 0.35)
        {
            return;
        }

        string room = Case.Manor.NameOf(LocationOf(rival.Detective.Id));
        string name = rival.Detective.ShortName;
        Emit(msgs, MessageKind.Info, rng.Pick(new[]
        {
            $"Word below stairs is that {name} has been nosing about the {room}.",
            $"You hear {name}'s voice in the {room}, holding forth to anyone who'll listen.",
            $"A housemaid mentions that {name} has \"as good as solved it\", apparently.",
        }));
    }

    // ================================================================== sending for callers

    /// <summary>True when this person is a caller who could be sent for right now.</summary>
    public bool CanSendFor(string personId) =>
        Case.Lockdown.AllowsVisitors
        && Case.Person(personId) is { IsVisitor: true }
        && LocationOf(personId) == RoomIds.Away
        && _summons.All(s => s.PersonId != personId || s.ToHour <= HourIndex);

    /// <summary>Sends a messenger for a caller from outside. They arrive within the hour or two and wait in the drawing room.</summary>
    public IReadOnlyList<GameMessage> SendFor(string personId)
    {
        var msgs = new List<GameMessage>();
        if (!Guard(msgs))
        {
            return msgs;
        }

        Character? person = Case.Person(personId);
        if (person is null || !person.IsVisitor)
        {
            return Warn(msgs, "Only callers from outside the house can be sent for.");
        }

        if (!Case.Lockdown.AllowsVisitors)
        {
            return Warn(msgs, "Nobody can get through to the house today.");
        }

        if (!CanSendFor(personId))
        {
            return Warn(msgs, $"{person.ShortName} is already here, or already on the way.");
        }

        int from = Math.Min(GameRules.DayHours - 1, HourIndex + (Minute % 60 < 30 ? 1 : 2));
        string room = Case.CrimeSceneId == RoomIds.Drawing ? RoomIds.Library : RoomIds.Drawing;
        _summons.Add(new Summons(personId, from, Math.Min(GameRules.DayHours, from + 2), room));
        Emit(msgs, MessageKind.Info,
            $"You send the bootboy running for {person.ShortName}. {person.HeCap} should be waiting in the {Case.Manor.NameOf(room)} by {Clock.Hour(GameRules.DayStartHour + from)}.");
        Advance(SendForMinutes, msgs);
        return msgs;
    }

    // ================================================================== highlights in the journal

    /// <summary>
    /// Ringed runs of words inside journal entries, by journal key. Spans are word indices into the entry's text
    /// (split on spaces), kept sorted, never overlapping and never touching: touching spans are merged.
    /// </summary>
    public Dictionary<string, List<WordSpan>> Highlights { get; } = new();

    /// <summary>The text a journal entry shows, which highlight word indices refer to.</summary>
    public string JournalText(string journalKey)
    {
        if (journalKey.StartsWith(JournalItem.CluePrefix, StringComparison.Ordinal))
        {
            return GetClue(journalKey[JournalItem.CluePrefix.Length..])?.Text ?? string.Empty;
        }

        return GetContradiction(journalKey[JournalItem.ClashPrefix.Length..])?.Description ?? string.Empty;
    }

    /// <summary>The words of an entry's text, as highlight indices count them.</summary>
    public static string[] Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The highlighted phrases of an entry, as text.</summary>
    public IReadOnlyList<string> HighlightedPhrases(string journalKey)
    {
        if (!Highlights.TryGetValue(journalKey, out List<WordSpan>? spans))
        {
            return Array.Empty<string>();
        }

        string[] words = Words(JournalText(journalKey));
        return spans
            .Where(s => s.First < words.Length)
            .Select(s => string.Join(' ', words[s.First..(Math.Min(s.Last, words.Length - 1) + 1)]).Trim(',', '.', ';', ':', '"'))
            .ToList();
    }

    /// <summary>Rings a run of words, merging it with any ringed run it overlaps or touches.</summary>
    public void AddHighlight(string journalKey, int first, int last)
    {
        (first, last) = (Math.Min(first, last), Math.Max(first, last));
        if (first < 0)
        {
            return;
        }

        if (!Highlights.TryGetValue(journalKey, out List<WordSpan>? spans))
        {
            spans = new List<WordSpan>();
            Highlights[journalKey] = spans;
        }

        // Absorb every span that overlaps or sits right next to the new one.
        var touching = spans.Where(s => s.First <= last + 1 && s.Last >= first - 1).ToList();
        foreach (WordSpan s in touching)
        {
            first = Math.Min(first, s.First);
            last = Math.Max(last, s.Last);
            spans.Remove(s);
        }

        spans.Add(new WordSpan(first, last));
        spans.Sort((a, b) => a.First.CompareTo(b.First));
    }

    /// <summary>Rubs out a run of words: any ringed span it covers shrinks, splits in two or disappears.</summary>
    public void EraseHighlight(string journalKey, int first, int last)
    {
        (first, last) = (Math.Min(first, last), Math.Max(first, last));
        if (!Highlights.TryGetValue(journalKey, out List<WordSpan>? spans))
        {
            return;
        }

        var result = new List<WordSpan>();
        foreach (WordSpan s in spans)
        {
            if (s.Last < first || s.First > last)
            {
                result.Add(s);
                continue;
            }

            if (s.First < first)
            {
                result.Add(new WordSpan(s.First, first - 1));
            }

            if (s.Last > last)
            {
                result.Add(new WordSpan(last + 1, s.Last));
            }
        }

        if (result.Count == 0)
        {
            Highlights.Remove(journalKey);
        }
        else
        {
            Highlights[journalKey] = result;
        }
    }

    /// <summary>Rubs out every ring inside one entry.</summary>
    public void ClearHighlights(string journalKey) => Highlights.Remove(journalKey);
}
