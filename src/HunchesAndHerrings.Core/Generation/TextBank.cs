using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

/// <summary>
/// Prose templates for dialogue, documents and notebook entries. Kept apart from the generator
/// so the logic stays readable and the writing can be extended freely.
/// </summary>
public static class TextBank
{
    private static readonly Dictionary<string, string[]> Activities = new()
    {
        [RoomIds.WineCellar] = new[] { "fetching up a bottle of claret", "checking the racks against the cellar book" },
        [RoomIds.Boiler] = new[] { "seeing to the boiler", "banking up the coal for the night" },
        [RoomIds.ServantsHall] = new[] { "at my supper", "mending by the fire", "at a hand of cribbage" },
        [RoomIds.Kitchen] = new[] { "scouring the pans", "setting the bread to prove", "warming some milk" },
        [RoomIds.Pantry] = new[] { "polishing the silver", "going over the accounts" },
        [RoomIds.Scullery] = new[] { "at the washing-up", "scrubbing the pots" },
        [RoomIds.Library] = new[] { "reading", "looking for a book", "dozing over the newspaper" },
        [RoomIds.Study] = new[] { "going over some papers", "writing letters" },
        [RoomIds.Billiard] = new[] { "knocking the balls about", "playing a frame of billiards" },
        [RoomIds.Conservatory] = new[] { "seeing to the orchids", "taking the air among the palms" },
        [RoomIds.Hall] = new[] { "winding the hall clock", "waiting about" },
        [RoomIds.Drawing] = new[] { "at the pianoforte", "playing patience", "sitting by the fire" },
        [RoomIds.Dining] = new[] { "taking a late glass of port", "laying the table for breakfast" },
        [RoomIds.Gardens] = new[] { "taking a turn about the gardens", "walking by the yews" },
        [RoomIds.Master] = new[] { "resting", "reading in bed" },
        [RoomIds.Blue] = new[] { "resting", "writing letters", "reading in bed" },
        [RoomIds.Rose] = new[] { "resting", "writing letters", "reading in bed" },
        [RoomIds.Green] = new[] { "resting", "writing letters", "reading in bed" },
        [RoomIds.Gallery] = new[] { "walking off my dinner", "looking at the portraits" },
        [RoomIds.Quarters] = new[] { "abed", "darning stockings" },
        [RoomIds.Landing] = new[] { "passing through" },
        [RoomIds.BackPassage] = new[] { "passing through" },
        [RoomIds.Away] = new[] { "home in the village", "on the road home" },
    };

    public static string Activity(string roomId, Random rng) =>
        Activities.TryGetValue(roomId, out string[]? list) ? rng.Pick(list) : "minding my own business";

    public static string SlotLead(int slot, Random rng)
    {
        string t = slot switch { 0 => "nine", 1 => "ten", _ => "eleven" };
        return rng.Next(3) switch
        {
            0 => $"At {t} o'clock",
            1 => $"Around {t}",
            _ => $"Come {t}",
        };
    }

    public static string JoinNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => string.Empty,
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
    };

    public static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>How a speaker refers to the victim.</summary>
    public static string VictimRef(Character speaker, Character victim)
    {
        bool lord = victim.IsMale;
        return speaker.Role switch
        {
            RoleKind.Spouse => lord ? "my husband" : "my wife",
            RoleKind.Heir => lord ? "my uncle" : "my aunt",
            RoleKind.Ward => "my guardian",
            RoleKind.Child => lord ? "my father" : "my mother",
            _ when speaker.Station == Station.Servant => lord ? "his lordship" : "her ladyship",
            _ => victim.ShortName,
        };
    }

    /// <summary>A short sentence that opens an answer, coloured by station.</summary>
    public static string Opener(Character speaker, Random rng)
    {
        string[] servant = { "Begging your pardon, {SIR}.", "Very good, {DET}.", "I'll tell you what I told the constable.", string.Empty, string.Empty };
        string[] gentry = { "Really, {DET}.", "If you must.", "Very well.", "I have already told your constable all this.", string.Empty };
        return rng.Pick(speaker.Station == Station.Servant ? servant : gentry);
    }

    public static string Greeting(Character c, Random rng)
    {
        if (c.Station == Station.Servant)
        {
            return rng.Pick(new[]
            {
                $"{c.ShortName} straightens up. \"{{DET}}. How can I help?\"",
                $"{c.ShortName} wipes {c.His} hands nervously. \"Is it about last night, {{SIR}}?\"",
                $"{c.ShortName} bobs {c.His} head. \"{{Sir}}.\"",
            });
        }

        return rng.Pick(new[]
        {
            $"{c.ShortName} regards you coolly. \"{{DET}}. I trust this won't take long.\"",
            $"{c.ShortName} sets down a teacup. \"Ah. The police.\"",
            $"{c.ShortName} looks pale and tired. \"Ask what you must, {{DET}}.\"",
        });
    }

    // ------------------------------------------------------------------ traits

    public static string TraitSelf(TraitCategory category, string value) =>
        TraitCatalog.Get(category).Value(value).Self;

    public static string TraitOther(TraitCategory category, string value, Character t) =>
        TraitCatalog.Fill(TraitCatalog.Get(category).Value(value).Other, t);

    public static (string Title, string Text) TraitDocument(TraitCategory category, string value, Character t)
    {
        TraitValue v = TraitCatalog.Get(category).Value(value);
        return (v.DocTitle, TraitCatalog.Fill(v.DocText, t));
    }

    // ------------------------------------------------------------------ scene

    /// <summary>What a print might have been left in, depending on the room.</summary>
    public static string Spill(string roomId) => roomId switch
    {
        RoomIds.Kitchen or RoomIds.Pantry => "flour",
        RoomIds.Gardens or RoomIds.Conservatory => "soft earth",
        RoomIds.Boiler or RoomIds.WineCellar => "coal dust",
        RoomIds.Scullery => "soapy water",
        RoomIds.Dining => "spilled port",
        _ => "spilled ink",
    };

    public static string SceneText(TraitCategory category, string value, string roomId) =>
        TraitCatalog.Fill(TraitCatalog.Get(category).Value(value).Scene ?? string.Empty, null, Spill(roomId));

    // ------------------------------------------------------------------ motives

    public static string MotiveAdmit(MotiveKind motive, string victimRef, Random rng) => motive switch
    {
        MotiveKind.Inheritance => $"I'm remembered in the will, I don't deny it. Who isn't, in this house?",
        MotiveKind.Debt => $"Yes, {victimRef} lent me money. I fully intended to repay it.",
        MotiveKind.Dismissal => $"{Capitalise(victimRef)} gave me notice last week. Unjustly, I might add.",
        MotiveKind.Jealousy => $"There was someone I cared for, and {victimRef} forbade it. That's all I'll say.",
        MotiveKind.Blackmail => $"{Capitalise(victimRef)} knew something about me. Something old. It doesn't matter now.",
        MotiveKind.Revenge => $"{Capitalise(victimRef)}'s family ruined mine, years ago. That's old history.",
        MotiveKind.Disinheritance => $"{Capitalise(victimRef)} was talking of changing the will. We quarrelled about it.",
        MotiveKind.Affair => $"There was... an attachment. {Capitalise(victimRef)} found out. That's all I'll say.",
        MotiveKind.Silence => $"{Capitalise(victimRef)} knew something about me I'd rather the world didn't.",
        MotiveKind.Recognition => $"{Capitalise(victimRef)} and I had a private understanding. A family matter.",
        MotiveKind.Negligence => $"I attended {victimRef} professionally. There's no more to it than that.",
        MotiveKind.Imposture => $"{Capitalise(victimRef)} asked me some impertinent questions about my past. I answered them.",
        MotiveKind.Insurance => $"Yes, there's a policy on {victimRef}'s life. A prudent arrangement.",
        MotiveKind.Theft => $"{Capitalise(victimRef)} accused me of taking things. It was a misunderstanding.",
        _ => rng.Pick(new[] { $"I'd no quarrel with {victimRef}.", $"{Capitalise(victimRef)} was good to me." }),
    };

    public static string MotiveDeny(string victimRef, Random rng) => rng.Pick(new[]
    {
        $"{Capitalise(victimRef)} and I got on well enough. Ask anyone.",
        $"I had every respect for {victimRef}. Every respect.",
        $"A terrible loss. Terrible. I can't think who'd do such a thing.",
        $"We had our differences, as anyone does. Nothing more.",
    });

    public static string MotiveGossip(MotiveKind motive, Character t, string victimRef) => motive switch
    {
        MotiveKind.Inheritance => $"{t.ShortName} stands to come into a good deal now, mark my words.",
        MotiveKind.Debt => $"{t.ShortName} owed {victimRef} a small fortune. Everyone knew it.",
        MotiveKind.Dismissal => $"{t.ShortName} was to be turned off without a character. {t.HeCap} took it very hard.",
        MotiveKind.Jealousy => $"{t.ShortName} was sweet on someone {victimRef} would never allow.",
        MotiveKind.Blackmail => $"I heard {victimRef} had something on {t.ShortName}. Something nasty.",
        MotiveKind.Revenge => $"{t.ShortName} has never forgiven the family for what happened to {t.His} father.",
        MotiveKind.Disinheritance => $"{Capitalise(victimRef)} meant to cut {t.ShortName} out of the will.",
        MotiveKind.Affair => $"{t.ShortName} has been carrying on with someone in this house. I've seen the looks.",
        MotiveKind.Silence => $"{t.ShortName} has a secret, and {victimRef} had found it out. I'd stake my life on it.",
        MotiveKind.Recognition => $"Have you looked at {t.ShortName}'s face? The family likeness. Nobody says it aloud.",
        MotiveKind.Negligence => $"{t.ShortName} was in a fearful state this morning. Kept asking after the medicine bottle.",
        MotiveKind.Imposture => $"There's something not right about {t.ShortName}. {t.HeCap} gets the little things wrong.",
        MotiveKind.Insurance => $"{t.ShortName} took out a policy on {victimRef}'s life not three months ago.",
        MotiveKind.Theft => $"Things have been going missing, and {victimRef} had it in mind that {t.ShortName} was taking them.",
        _ => $"{t.ShortName}? No reason in the world to wish anyone harm.",
    };

    public static (string Title, string Text) MotiveDocument(MotiveKind motive, Character t, Character victim, Random rng) => motive switch
    {
        MotiveKind.Inheritance => ("A copy of the will", $"A copy of {victim.ShortName}'s will, leaving a substantial sum to {t.ShortName}."),
        MotiveKind.Debt => ("An overdue IOU", $"An IOU for £{rng.Next(4, 40) * 100}, signed by {t.ShortName} and marked \"overdue — final demand\" in {victim.ShortName}'s hand."),
        MotiveKind.Dismissal => ("A letter of dismissal", $"A letter dismissing {t.ShortName} without a character, signed by {victim.ShortName} and dated yesterday."),
        MotiveKind.Jealousy => ("Torn love letters", $"A bundle of love letters addressed to {t.ShortName}. The last is torn across: \"{victim.ShortName} has forbidden it.\""),
        MotiveKind.Blackmail => ("A sealed envelope", $"A sealed envelope in {victim.ShortName}'s desk, marked \"Concerning {t.ShortName}: the Brighton affair\". Inside, a list of payments."),
        MotiveKind.Revenge => ("A yellowed cutting", $"A yellowed newspaper cutting about the ruin of the {t.Surname} family, heavily underlined. It belongs to {t.ShortName}."),
        MotiveKind.Disinheritance => ("A draft will", $"A draft of a new will, unsigned, cutting {t.ShortName} out entirely."),
        MotiveKind.Affair => ("Love letters", $"A ribbon-tied bundle of love letters to {t.ShortName}, the last one blotted: \"If {victim.ShortName} ever learns of this, we are finished.\""),
        MotiveKind.Silence => ("A note in the victim's hand", $"A note in {victim.ShortName}'s hand: \"Spoke to {t.ShortName}. Gave {t.Him} until Monday to tell the truth, or I shall.\""),
        MotiveKind.Recognition => ("A baptismal certificate", $"A baptismal certificate for {t.FirstName}, \"father unnamed\", with a note pinned to it in {victim.ShortName}'s hand: \"Mine. God forgive me.\""),
        MotiveKind.Negligence => ("A prescription", $"A prescription in {t.ShortName}'s hand for {victim.ShortName}'s sleeping draught. The dose is written as thirty drops: ten times the usual."),
        MotiveKind.Imposture => ("A letter of inquiry", $"A reply to {victim.ShortName} from the agent in London: \"I can find no record of any {t.FullName}. The person you describe is not who he claims.\""),
        MotiveKind.Insurance => ("An insurance policy", $"A life policy on {victim.ShortName} for £{rng.Next(3, 20) * 1000}, the beneficiary {t.ShortName}, dated this spring."),
        MotiveKind.Theft => ("A list of missing things", $"A list in {victim.ShortName}'s hand: a snuff box, two spoons, a brooch. Beside it: \"{t.Initials}? Speak to the constable.\""),
        _ => ("A letter", $"An unremarkable letter to {t.ShortName}."),
    };

    public static string MotiveNote(MotiveKind motive) => motive switch
    {
        MotiveKind.Inheritance => "inherits under the will",
        MotiveKind.Debt => "owed the victim money",
        MotiveKind.Dismissal => "was being dismissed",
        MotiveKind.Jealousy => "a forbidden attachment",
        MotiveKind.Blackmail => "was being blackmailed",
        MotiveKind.Revenge => "an old family grudge",
        MotiveKind.Disinheritance => "was to be cut out of the will",
        MotiveKind.Affair => "an affair",
        MotiveKind.Silence => "a secret the victim knew",
        MotiveKind.Recognition => "a secret parentage",
        MotiveKind.Negligence => "a medical error",
        MotiveKind.Imposture => "may not be who they claim",
        MotiveKind.Insurance => "an insurance policy",
        MotiveKind.Theft => "suspected of stealing",
        MotiveKind.Accident => "no motive",
        _ => "no motive",
    };

    public static string MotiveDetail(MotiveKind motive, Character m, Character victim) => motive switch
    {
        MotiveKind.Inheritance => $"{m.ShortName} could not wait for {victim.ShortName}'s money any longer.",
        MotiveKind.Debt => $"{m.ShortName} owed {victim.ShortName} more than {m.He} could ever repay, and the debt had been called in.",
        MotiveKind.Dismissal => $"{m.ShortName} was about to be thrown out without a character, which meant ruin.",
        MotiveKind.Jealousy => $"{victim.ShortName} had forbidden {m.ShortName}'s attachment, and {m.He} would not give it up.",
        MotiveKind.Blackmail => $"{victim.ShortName} had been bleeding {m.ShortName} dry over an old scandal.",
        MotiveKind.Revenge => $"{m.ShortName} had nursed a grudge for years over the ruin of {m.His} family.",
        MotiveKind.Disinheritance => $"{victim.ShortName} was about to cut {m.ShortName} out of the will.",
        MotiveKind.Affair => $"An affair lay at the root of it: {m.ShortName} would not let {victim.ShortName} stand in the way.",
        MotiveKind.Silence => $"{victim.ShortName} had discovered {m.ShortName}'s secret and meant to expose it.",
        MotiveKind.Recognition => $"{m.ShortName} was {victim.ShortName}'s unacknowledged child, and had been denied once too often.",
        MotiveKind.Negligence => $"{m.ShortName} had made a fatal error with the dose, and lied to save {m.His} reputation.",
        MotiveKind.Imposture => $"{victim.ShortName} had found out that {m.ShortName} was an impostor.",
        MotiveKind.Insurance => $"{m.ShortName} stood to collect on a policy on {victim.ShortName}'s life.",
        MotiveKind.Theft => $"{victim.ShortName} had caught {m.ShortName} stealing, and meant to call the constable.",
        _ => $"{m.ShortName}'s reasons died with {victim.ShortName}.",
    };

    // ------------------------------------------------------------------ fallbacks

    public static string Fallback(Topic topic, Character speaker, Character? target, Random rng) => topic switch
    {
        Topic.Sightings => rng.Pick(new[] { "I saw nobody worth mentioning, {SIR}.", "I didn't see a soul about. I kept to myself." }),
        Topic.Self => "There's nothing much to tell about me.",
        Topic.AboutPerson when target is not null => rng.Pick(new[]
        {
            $"{target.ShortName}? I couldn't tell you much. Keeps {target.Him}self to {target.Him}self.",
            $"I hardly know {target.ShortName}, {{DET}}.",
            $"{target.ShortName} and I don't move in the same circles.",
        }),
        Topic.Whereabouts => "I couldn't say where anyone will be. I've my own work to see to.",
        _ => "I've nothing to add, {SIR}.",
    };

    public static string EmptyRoomText(string roomName) =>
        $"The {roomName} saw no use last night: the grate is cold and swept, the lamps are full and untrimmed, and the housemaid's chalk mark on the door is unbroken. Nobody was in here at any hour of the evening.";
}
