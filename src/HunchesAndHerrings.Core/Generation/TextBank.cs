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

    public static string TraitSelf(TraitCategory category, string value, Random rng) => category switch
    {
        TraitCategory.Smoking => value switch
        {
            "None" => rng.Pick(new[] { "I've never touched tobacco. A filthy habit.", "Smoke? Never. I can't abide the smell." }),
            "Pipe" => rng.Pick(new[] { "I take a pipe of an evening; it settles the nerves.", "A pipe after supper, same as my father before me." }),
            "Cigar" => rng.Pick(new[] { "A cigar after dinner, nothing more.", "I enjoy a good Havana, if that's a crime." }),
            _ => rng.Pick(new[] { "The odd cigarette. Turkish, if you must know.", "A cigarette now and then. Don't look so shocked." }),
        },
        TraitCategory.Handedness => value == "Left"
            ? rng.Pick(new[] { "I write with my left hand; they never could beat it out of me at school.", "Cack-handed, my old nurse called me. The left does all the work." })
            : rng.Pick(new[] { "Right-handed, like any sensible person.", "I do everything with my right hand, {DET}. Why?" }),
        TraitCategory.ShoeSize => value switch
        {
            "Small" => "My boots? I've small feet; the cobbler says so every time.",
            "Medium" => "My boots are an ordinary size. Nothing remarkable.",
            _ => "I've feet like barges. My boots are made special in the village.",
        },
        TraitCategory.Allergy => value switch
        {
            "None" => "Nothing troubles my constitution, thank you.",
            "Lilies" => "Lilies set me sneezing something terrible. I can't stay in a room with them.",
            "Cats" => "I can't be near that cat; my eyes stream for hours.",
            "Horsehair" => "Horsehair brings me out in hives, so I keep off the settees.",
            _ => "Dust sets my chest wheezing. I keep out of the shut-up rooms.",
        },
        _ => value == TraitRules.Frail
            ? rng.Pick(new[] { "My gout has been dreadful; I can barely lift a teacup this week.", "I sprained my wrist last Tuesday. It's still strapped, see?", "My back has been bad all winter. I can't lift a thing." })
            : rng.Pick(new[] { "Strong as an ox, me.", "I'm hale enough, thank you." }),
    };

    public static string TraitOther(TraitCategory category, string value, Character t, Random rng)
    {
        string n = t.ShortName;
        return category switch
        {
            TraitCategory.Smoking => value switch
            {
                "None" => $"{n} doesn't smoke. Won't have tobacco near {t.Him}.",
                "Pipe" => rng.Pick(new[] { $"{n}'s never without that pipe.", $"{n} smokes a pipe; you can smell it on the stairs." }),
                "Cigar" => $"{n} smokes cigars. The curtains reek of them.",
                _ => $"{n} has taken up cigarettes. Very modern.",
            },
            TraitCategory.Handedness => value == "Left"
                ? $"{n} is left-handed. Writes all smudged and backwards."
                : $"{n}? Right-handed, I'm sure of it.",
            TraitCategory.ShoeSize => value switch
            {
                "Small" => $"{n} has dainty little feet.",
                "Medium" => $"{n}'s boots are an ordinary size. I've cleaned them, I'd know.",
                _ => $"{n} has enormous feet. Leaves prints like a carthorse.",
            },
            TraitCategory.Allergy => value switch
            {
                "None" => $"{n} is as healthy as a horse.",
                "Lilies" => $"{n} can't abide lilies. Sneezes fit to wake the dead.",
                "Cats" => $"{n} goes red and streaming if the cat comes near.",
                "Horsehair" => $"{n} won't sit on a horsehair settee. Comes out in hives.",
                _ => $"{n} wheezes at the first speck of dust.",
            },
            _ => value == TraitRules.Frail
                ? $"{n} is in a bad way. Gout, or a bad back; can barely lift a cup."
                : $"{n}'s as strong as a navvy.",
        };
    }

    public static (string Title, string Text) TraitDocument(TraitCategory category, string value, Character t)
    {
        string n = t.ShortName;
        return category switch
        {
            TraitCategory.Smoking => value switch
            {
                "None" => ("A temperance pledge", $"A signed temperance card in {n}'s name, promising to abjure strong drink and tobacco alike."),
                "Pipe" => ("A briar pipe", $"A well-used briar pipe and a pouch of shag tobacco, initialled {t.Initials}. {n}'s, plainly."),
                "Cigar" => ("A cigar case", $"A silver cigar case engraved for {n}, half full of Havanas."),
                _ => ("Turkish cigarettes", $"A packet of Turkish cigarettes and a vesta case belonging to {n}."),
            },
            TraitCategory.Handedness => value == "Left"
                ? ("A smudged letter", $"A half-written letter by {n}: the ink is smeared along every line, as only a left-handed writer smudges it.")
                : ("A neat letter", $"A letter by {n} in a neat, right-slanting hand; the pen rests to the right of the blotter."),
            TraitCategory.ShoeSize => ("A pair of boots", $"{n}'s boots stand by the bed: {BootWords(value)}."),
            TraitCategory.Allergy => value switch
            {
                "None" => ("A medical certificate", $"An insurance medical certificate for {n}: \"Robust constitution. No sensitivities of any kind.\""),
                "Lilies" => ("A prescription", $"A prescription for {n}: \"For the hay fever brought on by lilies. Avoid the flowers entirely.\""),
                "Cats" => ("A prescription", $"A prescription for {n}: \"For the violent reaction to cats. Keep the animal away.\""),
                "Horsehair" => ("A prescription", $"A prescription for {n}: \"Ointment for the hives caused by horsehair.\""),
                _ => ("A prescription", $"A prescription for {n}: \"For the asthma aggravated by dust. Avoid unaired rooms.\""),
            },
            _ => value == TraitRules.Frail
                ? ("Liniment and a sling", $"A bottle of liniment and a sling, with a doctor's note: \"{n}: no lifting of any kind for a fortnight.\"")
                : ("A pair of dumb-bells", $"A pair of iron dumb-bells, plainly well used by {n}."),
        };
    }

    public static string BootWords(string size) => size switch
    {
        "Small" => "small and narrow",
        "Medium" => "of quite ordinary size",
        _ => "enormous",
    };

    // ------------------------------------------------------------------ scene

    public static string SceneText(TraitCategory category, string value, string roomId)
    {
        string spill = roomId switch
        {
            RoomIds.Kitchen => "flour",
            RoomIds.Gardens or RoomIds.Conservatory => "soft earth",
            RoomIds.Boiler or RoomIds.WineCellar => "coal dust",
            _ => "spilled ink",
        };

        return category switch
        {
            TraitCategory.Smoking => value switch
            {
                "Pipe" => "Fresh pipe dottle has been knocked out by the body, still faintly warm when the body was found. The killer smokes a pipe.",
                "Cigar" => "A half-smoked cigar has been ground into the floor by the body. The killer smokes cigars.",
                _ => "Two cigarette ends lie by the body, the paper still fresh. The killer smokes cigarettes.",
            },
            TraitCategory.Handedness => "The wound is on the right side of the head, dealt from the front: the blow was struck left-handed.",
            TraitCategory.ShoeSize => $"A clear boot-print in {spill} beside the body: {BootWords(value)}.",
            TraitCategory.Allergy => value switch
            {
                "Lilies" => "A great vase of fresh lilies stands by the body, their pollen dusting everything. Anyone sensitive to lilies would have been sneezing helplessly.",
                "Cats" => "The house cat was found shut in with the body; its hairs are on everything. Anyone who reacts to cats could not have endured it.",
                "Horsehair" => "The horsehair settee was torn open in the struggle and stuffing is everywhere. Anyone sensitive to horsehair would be covered in hives.",
                _ => "The struggle raised a great cloud of dust in the long-shut room. Anyone with a dust-sensitive chest would have been choking.",
            },
            _ => "The blow was delivered with terrible force, and the body was dragged several feet. No feeble or injured hand did this.",
        };
    }

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
