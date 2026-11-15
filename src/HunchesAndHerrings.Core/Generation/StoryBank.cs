using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

/// <summary>A kind of fatal accident: where it happens and the evidence that shows what really occurred.</summary>
/// <param name="Id">Short identifier.</param>
/// <param name="RoomId">Where it happens.</param>
/// <param name="Cause">The evidence of the accident at the scene (key).</param>
/// <param name="Warning">What a servant says about the hazard beforehand.</param>
/// <param name="Summary">One-line account for the verdict.</param>
public sealed record AccidentKind(string Id, string RoomId, string Cause, string Warning, string Summary);

/// <summary>A secret someone in the house is keeping.</summary>
/// <param name="Id">Short identifier.</param>
/// <param name="Label">Notebook label, e.g. "gambling debts".</param>
/// <param name="DocTitle">Title of the document that exposes it.</param>
/// <param name="DocText">Its text: {n} name, {kw} keyword, {room} and {time} for the rendezvous.</param>
/// <param name="Confession">What they say when confronted: {room}, {time}, {kw}.</param>
public sealed record SecretKind(string Id, string Label, string DocTitle, string DocText, string Confession);

/// <summary>Stock prose for the larger shapes of a story: lockdowns, accidents, secrets and the rival.</summary>
public static class StoryBank
{
    public static IReadOnlyList<Lockdown> Lockdowns { get; } = new[]
    {
        new Lockdown("flood", "The river burst its banks in the night; the lane is under three feet of water and nobody can leave.", false, false),
        new Lockdown("snow", "Snow fell heavily after midnight; the drifts are up to the hedgetops and the house is cut off.", false, false),
        new Lockdown("bridge", "The old bridge over the Wend gave way in the storm, and the house is cut off until it can be shored up.", false, false),
        new Lockdown("police", "By order of the Chief Constable, no member of the household may leave the grounds. Callers on official business may come and go.", true, false),
        new Lockdown("quarantine", "Scarlet fever has broken out in the village; the doctor has placed the house in quarantine, and only he and the constable may pass the lodge.", true, false),
        new Lockdown("will", "The late master's own instructions, held by the family solicitor, forbid any of the household to leave until the will is read at six this evening.", true, true),
        new Lockdown("causeway", "The house stands on the tidal island, and the causeway is under water until the evening ebb. Only a boat can come across, and only at slack water.", true, false),
        new Lockdown("strike", "The branch line is shut by a strike and both carriage horses are lame; nobody is going anywhere far today, though the village is a walk away.", true, true),
        new Lockdown("fog", "A fog so thick has come down off the moor that a man can't see his own boots; the constable has told the household to stay put.", true, false),
    };

    public static IReadOnlyList<AccidentKind> Accidents { get; } = new[]
    {
        new AccidentKind("ladder", RoomIds.Library,
            "The library ladder lies on its side. Its third rung has snapped clean through, rotten at the core. The wound on the victim's head matches the corner of the brass fender exactly, and there is blood on the fender and nowhere else.",
            "I told the master that ladder rung was rotten, {SIR}. Told him a fortnight ago. He would go up it after his books.",
            "climbed the library ladder for a book; a rotten rung gave way and the fall against the fender killed {him}"),
        new AccidentKind("cellarstep", RoomIds.WineCellar,
            "The fourth cellar step is worn hollow and greasy, and there is a long skid mark on it. The candle the victim carried lies at the bottom, snuffed in the fall. The injuries are those of a fall down stone steps: no one blow, but many.",
            "Those cellar steps are a deathtrap, {SIR}. I've asked for a rail a dozen times.",
            "went down to the cellar for a bottle by candlelight, slipped on the worn step and fell"),
        new AccidentKind("draught", RoomIds.Master,
            "The victim's spectacles lie broken on the floor by the bed, a lens cracked through. Without them the victim could not have read the dose marked on the bottle. The bottle is the victim's own, and only the victim's prints are on it.",
            "The doctor warned the master about measuring that draught by eye. Said he'd do himself a mischief one day.",
            "broke {his} spectacles, measured {his} sleeping draught by eye and took far too much"),
        new AccidentKind("gas", RoomIds.Study,
            "The gas tap by the study fireplace is turned half on, but the jet is unlit: the flame blew out in the draught from the chimney. The window was shut fast from inside. The victim's lips are cherry red, as they are with coal gas.",
            "The study gas has been blowing out all week, {SIR}. The fitter was to come Monday.",
            "dozed in the study with the gas turned low; the flame blew out and the gas did the rest"),
        new AccidentKind("conservatory", RoomIds.Conservatory,
            "A pane in the conservatory roof has slipped its rotten glazing bar and fallen. The shards and the bar lie where the victim stood, and there is glass in the wound. Nobody struck this blow: it fell from above.",
            "That roof's been letting go of its panes all winter. I've told them and told them.",
            "was standing beneath the conservatory roof when a loose pane fell"),
    };

    public static IReadOnlyList<SecretKind> Secrets { get; } = new[]
    {
        new SecretKind("gambling", "gambling debts", "A bookmaker's letter",
            "A bookmaker's letter to {n}: \"Your account, re: the {kw} meeting, now stands at £340. I will be paid, one way or another.\" Pencilled on the back: \"{room}, {time}. Bring what you can.\"",
            "Very well. I owe money, a great deal of it, over the {kw} meeting. At {time} I was in the {room}, meeting the man's runner. Not where I said."),
        new SecretKind("engagement", "a secret engagement", "A secret love letter",
            "A love letter to {n}, signed only \"Your own, for ever\": \"The {room} at {time}, my darling, as before. When we are married none of them shall part us. Remember {kw}.\"",
            "Yes. I am secretly engaged, since {kw}, and nobody here knows. At {time} I was in the {room}, meeting my intended. I lied to protect them."),
        new SecretKind("letters", "stolen letters", "A bundle of letters",
            "A bundle of letters, not addressed to {n}, tied with ribbon and hidden in a hatbox. One is marked \"{kw}\". A note in {n}'s hand: \"Return them in the {room} at {time}?\"",
            "I took those letters, the {kw} letters. I meant to give them back. I went to the {room} at {time} to put them where they'd be found."),
        new SecretKind("character", "a forged character", "A forged reference",
            "A servant's character in a hand that is plainly {n}'s own, signed with the name of a lady who died in {kw}. A note: \"Mr F. the agent, {room}, {time}.\"",
            "My character's forged. The lady in {kw} never knew me. At {time} I was in the {room} seeing the agent who wrote it. Please don't turn me off."),
        new SecretKind("drink", "secret drinking", "An empty bottle",
            "Behind {n}'s washstand, three empty gin bottles and a pawn ticket from {kw}. On the label of one, in pencil: \"{room}, {time}: the rest of the case.\"",
            "I drink, {SIR}. More than I should. At {time} I was in the {room}, at the bottle I'd hidden there. I'd sooner die than have it known."),
        new SecretKind("child", "a secret child", "A letter from a foster-mother",
            "A letter to {n} from a woman in {kw}: \"The boy is well and asks for you. The money came. Will you come at Easter?\" A pencilled note: \"Post it from the {room} at {time}.\"",
            "I have a son, {SIR}, at {kw}, with a foster-mother. Nobody here knows. At {time} I was in the {room}, writing to him. I couldn't say so."),
        new SecretKind("pawn", "pawned heirlooms", "A pawnbroker's ticket",
            "Three pawn tickets from a shop in {kw}, made out to {n}, for a brooch, a snuff box and a silver spoon. One is torn: \"{room}, {time}.\"",
            "I pawned some things. Old things nobody would miss. I meant to redeem them. At {time} I was in the {room}, putting the last one back."),
        new SecretKind("spiritualist", "seances", "A spiritualist's card",
            "A card from a medium in {kw}: \"The spirits will speak again. The {room}, at {time}, with the lamps out.\" {n}'s name is written on the back.",
            "I've been holding seances, {SIR}, trying to reach someone I lost. At {time} I was in the {room} with the lamps out. I know how it sounds."),
    };

    public static IReadOnlyList<string> Keywords { get; } = new[]
    {
        "Brighton", "Harrogate", "Monte Carlo", "Doncaster", "Cheltenham", "Calcutta", "Margate", "Baden-Baden",
        "Epsom", "Torquay", "Malvern", "Bath",
    };

    public static string SecretTime(int slot) => slot switch
    {
        0 => "nine",
        1 => "ten",
        _ => "eleven",
    };

    // ------------------------------------------------------------------ rival

    private static readonly string[] RivalMale = { "Lucius Pemberton-Hale", "Barnaby Crisp", "Aurelius Fitch", "Maximilian Dorrance" };
    private static readonly string[] RivalFemale = { "Octavia Thrale", "Henrietta Gaunt", "Philippa Strand", "Cressida Moon" };

    public static (string First, string Surname) RivalName(Random rng, Gender gender)
    {
        string full = rng.Pick(gender == Gender.Male ? RivalMale : RivalFemale);
        int space = full.IndexOf(' ');
        return (full[..space], full[(space + 1)..]);
    }

    public static string RivalEngagedBy(Random rng) => rng.Pick(new[]
    {
        "the insurers", "the family solicitor", "a London newspaper", "the victim's estranged brother", "nobody at all: they simply turned up",
    });

    public static string RivalLabel(RivalStyle style) => style switch
    {
        RivalStyle.Pompous => "the celebrated private detective",
        RivalStyle.Sly => "a private inquiry agent",
        _ => "a newspaperman turned detective",
    };

    public static string RivalGreeting(RivalStyle style, Random rng) => style switch
    {
        RivalStyle.Pompous => rng.Pick(new[] { "Ah, the official police. How quaint. Do try to keep up, {DET}.", "My dear {DET}! I have the case three parts solved already." }),
        RivalStyle.Sly => rng.Pick(new[] { "{DET}. We keep running into each other.", "Oh, it's you. I was just leaving." }),
        _ => rng.Pick(new[] { "{DET}! Any comment for the morning edition?", "Beat you to it again, {DET}. You'll read all about it." }),
    };

    public static string RivalTipIntro(RivalStyle style, Random rng) => style switch
    {
        RivalStyle.Pompous => rng.Pick(new[] { "Very well, a crumb from my table:", "I shall be magnanimous:" }),
        RivalStyle.Sly => rng.Pick(new[] { "Between ourselves:", "I didn't tell you this, mind:" }),
        _ => rng.Pick(new[] { "Here's one for free:", "Off the record:" }),
    };

    public static string RivalNothing(RivalStyle style) => style switch
    {
        RivalStyle.Pompous => "I have given you quite enough, {DET}. The rest you must earn.",
        RivalStyle.Sly => "I've nothing more for you. Not yet, anyway.",
        _ => "I'm tapped out, {DET}. Buy the paper tomorrow.",
    };

    public static string RivalReturns(RivalStyle style) => style switch
    {
        RivalStyle.Pompous => "Oh, this? I took it for safe keeping. You may have it; I have quite finished with it.",
        RivalStyle.Sly => "You'll be wanting this, I suppose. I only borrowed it.",
        _ => "Fair's fair. I lifted this earlier; it's yours now. Don't say I never gave you anything.",
    };

    public static string RivalKeeps(RivalStyle style) => style switch
    {
        RivalStyle.Pompous => "I may have something of interest in my pocket. You shall have it when I've finished with it.",
        RivalStyle.Sly => "If I'd found anything, {DET}, I'd hardly hand it over, would I? Ask me later.",
        _ => "Sorry, {DET}: the editor gets first look. Try me in an hour.",
    };

    public static string RivalTheory(RivalStyle style, string name, Random rng) => style switch
    {
        RivalStyle.Pompous => rng.Pick(new[] { $"It is obvious, {{DET}}. Look at {name}. Simply look.", $"{name}. I have known it since breakfast." }),
        RivalStyle.Sly => rng.Pick(new[] { $"I'm keeping an eye on {name}. That's all I'll say.", $"Watch {name}'s hands when you ask about last night." }),
        _ => rng.Pick(new[] { $"My money's on {name}. Front page, you mark my words.", $"{name}. Bet you a sovereign." }),
    };

    public static string RivalTheoryAccident(RivalStyle style) => style switch
    {
        RivalStyle.Pompous => "Has it occurred to you, {DET}, that there may be no murderer at all? No? It wouldn't.",
        RivalStyle.Sly => "Are you quite sure it was murder?",
        _ => "Between you and me, I've a feeling this one won't sell papers. Nobody did it.",
    };

    public static string RivalCloseted(Character rival, Character person) =>
        $"{person.ShortName} is closeted with {rival.ShortName}, and the door is shut. You'll have to come back.";

    public static string RivalPocketed(Character rival) =>
        $"A drawer stands open and the dust has been disturbed: someone has been through here before you. You suspect {rival.ShortName}.";
}
