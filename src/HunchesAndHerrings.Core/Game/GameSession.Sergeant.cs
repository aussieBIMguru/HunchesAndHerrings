using System.Text;
using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Game;

/// <summary>Talking to the sergeant: a small keyword parser that turns plain English into orders.</summary>
public sealed partial class GameSession
{
    private static readonly string[] SearchWords = { "search", "look", "check", "examine", "inspect", "go through", "turn over" };
    private static readonly string[] QuestionWords = { "question", "ask", "talk", "speak", "interview", "interrogate", "quiz", "grill", "have a word" };
    private static readonly string[] TheoryWords = { "think", "theory", "suspect", "reckon", "opinion", "idea", "ideas", "guess", "thoughts", "who did", "whodunnit" };
    private static readonly string[] AdviceWords = { "help", "stuck", "next", "advice", "suggest", "what now", "hint", "where do i", "what should" };
    private static readonly string[] RecapWords = { "recap", "summary", "summarise", "progress", "how are we", "how am i" };
    private static readonly string[] StatusWords = { "status", "busy", "where are you", "what are you doing", "you doing" };
    private static readonly string[] SendForWords = { "send for", "fetch", "summon", "call for", "get hold of", "bring" };
    private static readonly string[] GreetingWords = { "hello", "hi", "morning", "afternoon", "evening", "hey" };

    /// <summary>Gives the sergeant an instruction or question in plain words.</summary>
    public IReadOnlyList<GameMessage> SergeantCommand(string text)
    {
        var msgs = new List<GameMessage>();
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return msgs;
        }

        _chat.Add(new ChatLine(true, trimmed, Minute));
        if (IsOver)
        {
            SergeantSay(msgs, "The case is closed, {SIR}. Nothing more to be done.");
            return msgs;
        }

        string said = Normalise(trimmed);
        Character? person = MatchPerson(said);
        string? room = MatchRoom(said);
        var rng = RuntimeRng();

        if (Has(said, SendForWords) && person is not null)
        {
            msgs.AddRange(SendFor(person.Id));
        }
        else if (Has(said, StatusWords))
        {
            SergeantSay(msgs, Sergeant is null ? "Right here at your side, {SIR}." : $"I'm {SergeantStatus}.");
        }
        else if (Has(said, "where") && person is not null)
        {
            SergeantSay(msgs, WhereIs(person));
        }
        else if (Has(said, SearchWords))
        {
            string target = room ?? RoomId;
            msgs.AddRange(SendSergeantToSearch(target));
        }
        else if (Has(said, QuestionWords) && person is not null)
        {
            msgs.AddRange(SendSergeantToInterview(person.Id));
        }
        else if (Has(said, QuestionWords) || Has(said, "where"))
        {
            SergeantSay(msgs, "Who did you have in mind, {SIR}? Give me a name or their position: the cook, the colonel, that sort of thing.");
        }
        else if (Has(said, TheoryWords))
        {
            SergeantSay(msgs, SergeantVoice.Theory(Case.SergeantStyle, (person ?? rng.Pick(Case.Suspects)).ShortName, rng));
        }
        else if (Has(said, AdviceWords))
        {
            SergeantSay(msgs, Advice());
        }
        else if (Has(said, RecapWords))
        {
            SergeantSay(msgs, Recap());
        }
        else if (Has(said, GreetingWords))
        {
            SergeantSay(msgs, SergeantVoice.Greeting(Case.SergeantStyle));
        }
        else
        {
            SergeantSay(msgs, SergeantVoice.Puzzled(Case.SergeantStyle));
        }

        return msgs;
    }

    /// <summary>The sergeant's idea of where someone is right now.</summary>
    private string WhereIs(Character person)
    {
        Whereabout w = WhereaboutsAt(HourIndex).First(x => x.Person == person);
        string n = person.ShortName;
        if (w.RoomId == RoomIds.Away)
        {
            return person.IsVisitor
                ? $"{n} isn't at the house just now. We could send for {person.Him}, if you like: say \"send for {n}\"."
                : $"{n} has gone off to the village, I believe. Back later this afternoon.";
        }

        string room = Case.Manor.NameOf(w.RoomId);
        return w.Certainty switch
        {
            Certainty.Seen when w.RoomId == RoomId => $"{n} is right here in the {room} with us, {{SIR}}.",
            Certainty.Seen => $"You saw {n} in the {room} yourself, not long ago.",
            Certainty.Told => $"{n} should be in the {room} about now. That's what we were told.",
            _ when w.Precise => $"If {person.He} keeps to {person.His} habits, {n} will be in the {room}.",
            _ => $"Somewhere {FloorPhrase(Case.Manor[w.RoomId].Floor)}, I'd guess. Ask the butler or housekeeper about everyone's movements, or circle {person.Him} in the notebook and I'll keep a closer eye.",
        };
    }

    private static string FloorPhrase(Floor floor) => floor switch
    {
        Floor.Upper => "upstairs",
        Floor.Cellar => "down below stairs",
        _ => "on the ground floor",
    };

    /// <summary>Free, gentle guidance based on what the player has and hasn't done.</summary>
    private string Advice()
    {
        string scene = Case.CrimeScene.Name;
        if (!SearchedRooms.Contains(Case.CrimeSceneId) && !DiscoveredClues.Any(c => c.RoomId == Case.CrimeSceneId))
        {
            return $"I'd start with the {scene}, {{SIR}}. The body and the room will tell us when it happened, and something about who.";
        }

        if (Case.Clues.Any(c => c.RoomId == Case.CrimeSceneId && c.IsKey && !IsDiscovered(c.Id)))
        {
            return $"There's more to be had from the {scene}. It's worth another look.";
        }

        if (_contradictions.Count > 0 && RuntimeRng().NextDouble() < 0.4)
        {
            return "Mind those clashes in the notebook. Somebody's lying, and it might not be the somebody you'd expect.";
        }

        var eliminated = ProgressSolver.Eliminated(Case, DiscoveredClues);
        var unaccounted = Case.Suspects.Where(s => !Case.IsCulprit(s.Id) && !eliminated.Contains(s.Id)).ToList();
        if (unaccounted.Count == 0)
        {
            return Case.IsAccident
                ? "By my reckoning every one of them can be put somewhere else when it happened, {SIR}. Makes you wonder if anyone did it at all."
                : "By my reckoning there's precious few left in the frame, {SIR}. Shall we see about an arrest?";
        }

        var callers = Case.Visitors.Where(v => CanSendFor(v.Id)).ToList();
        if (callers.Count > 0 && RuntimeRng().NextDouble() < 0.25)
        {
            return $"{callers[0].ShortName} isn't at the house just now, but we could send for {callers[0].Him}. Just say the word.";
        }

        var secrets = Case.Suspects.Where(s => CanConfront(s.Id) && !HasAsked(s.Id, Topic.Confront)).ToList();
        if (secrets.Count > 0)
        {
            return $"We've found something {secrets[0].ShortName} would rather we hadn't. Put it to {secrets[0].Him}, {{SIR}}.";
        }

        // Point at someone still in the frame without saying whether they're guilty.
        var inFrame = Case.Suspects.Where(s => !eliminated.Contains(s.Id)).ToList();
        Character pick = RuntimeRng().Pick(inFrame);
        if (!HasAsked(pick.Id, Topic.LastNight))
        {
            return $"Has anyone asked {pick.ShortName} where they were last night? I'd want that in the notebook.";
        }

        if (!HasAsked(pick.Id, Topic.Sightings))
        {
            return $"{pick.ShortName} might have seen something last night. Worth asking.";
        }

        if (!SearchedRooms.Contains(pick.PersonalRoomId))
        {
            return $"We've not been through {pick.ShortName}'s things. They'll be in the {Case.Manor.NameOf(pick.PersonalRoomId)}.";
        }

        return $"There's still {unaccounted.Count + Case.CulpritIds.Count} of them we can't rule out. Ask folk about each other: servants notice everything.";
    }

    private string Recap()
    {
        int clues = _discoveredOrder.Count;
        int innocents = Case.Suspects.Count - Case.CulpritIds.Count;
        int cleared = ProgressSolver.Eliminated(Case, DiscoveredClues).Count;
        string time = MinutesLeft >= 60 ? $"{MinutesLeft / 60} hours and {MinutesLeft % 60} minutes" : $"{MinutesLeft} minutes";
        return $"We've {clues} things in the journal and {_contradictions.Count} clashes. By my reckoning you could rule out {cleared} of the {innocents} innocent folk already. That leaves {time} before the Chief Constable arrives.";
    }

    // ------------------------------------------------------------------ parsing

    private static string Normalise(string text)
    {
        var sb = new StringBuilder(" ");
        foreach (char ch in text.ToLowerInvariant())
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        sb.Append(' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Insert(0, " ") + " ";
    }

    private static bool Has(string said, params string[] words) => words.Any(w => said.Contains($" {w} ", StringComparison.Ordinal));

    private Character? MatchPerson(string said)
    {
        Character? best = null;
        int bestLength = 0;
        foreach (Character s in Household)
        {
            foreach (string alias in PersonAliases(s))
            {
                if (alias.Length > bestLength && said.Contains($" {alias} ", StringComparison.Ordinal))
                {
                    best = s;
                    bestLength = alias.Length;
                }
            }
        }

        return best;
    }

    private static IEnumerable<string> PersonAliases(Character s)
    {
        yield return Normalise(s.ShortName).Trim();
        yield return s.Surname.ToLowerInvariant();
        yield return s.FirstName.ToLowerInvariant();
        foreach (string word in Normalise(s.RoleLabel).Trim().Split(' ').Where(w => w.Length > 3 && w is not "the" and not "and" and not "army" and not "family" and not "lady's" and not "lady"))
        {
            yield return word;
        }

        switch (s.Role)
        {
            case RoleKind.Maid: yield return "maid"; break;
            case RoleKind.Reverend: yield return "vicar"; yield return "reverend"; break;
            case RoleKind.Colonel: yield return "colonel"; break;
            case RoleKind.Doctor: yield return "doctor"; yield return "dr"; break;
            case RoleKind.Spouse: yield return s.IsMale ? "husband" : "wife"; break;
            case RoleKind.BusinessPartner: yield return "partner"; break;
            case RoleKind.Constable: yield return "constable"; yield return "policeman"; break;
            case RoleKind.RivalDetective: yield return "rival"; yield return "detective"; break;
            case RoleKind.YoungChild: yield return "child"; yield return "boy"; yield return "girl"; break;
        }
    }

    private string? MatchRoom(string said)
    {
        if (Has(said, "here", "this room"))
        {
            return RoomId;
        }

        if (Has(said, "scene", "crime scene", "body"))
        {
            return Case.CrimeSceneId;
        }

        string? best = null;
        int bestLength = 0;
        foreach (Room room in Case.Manor.Rooms)
        {
            foreach (string alias in RoomAliases(room))
            {
                if (alias.Length > bestLength && said.Contains($" {alias} ", StringComparison.Ordinal))
                {
                    best = room.Id;
                    bestLength = alias.Length;
                }
            }
        }

        return best;
    }

    private static IEnumerable<string> RoomAliases(Room room)
    {
        string name = Normalise(room.Name).Trim();
        yield return name;
        yield return name.Replace(" room", string.Empty).Replace("servants s", "servants");
        switch (room.Id)
        {
            case RoomIds.WineCellar: yield return "cellar"; yield return "wine"; break;
            case RoomIds.Billiard: yield return "billiards"; yield return "billiard"; break;
            case RoomIds.Gardens: yield return "garden"; yield return "grounds"; break;
            case RoomIds.Master: yield return "master"; yield return "master bedroom"; break;
            case RoomIds.Quarters: yield return "attic"; yield return "quarters"; break;
            case RoomIds.ServantsHall: yield return "servants hall"; break;
            case RoomIds.Pantry: yield return "pantry"; break;
            case RoomIds.Gallery: yield return "gallery"; break;
            case RoomIds.BackPassage: yield return "back stairs"; yield return "passage"; break;
            case RoomIds.Hall: yield return "entrance"; yield return "hall"; break;
        }
    }
}
