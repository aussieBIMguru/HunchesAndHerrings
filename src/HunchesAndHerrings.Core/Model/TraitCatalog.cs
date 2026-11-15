using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// One possible value of a trait, with every piece of prose the game needs for it. Templates may use
/// {n} (the person's name), {he} {He} {him} {his} {His}, {init} (initials) and, in scene text, {spill}
/// (what a print was left in).
/// </summary>
/// <param name="Id">Canonical value stored in facts.</param>
/// <param name="Short">A word or two for the suspect matrix.</param>
/// <param name="Desc">Lower-case description, e.g. "smokes a pipe".</param>
/// <param name="Self">What the person says about themselves.</param>
/// <param name="Other">What someone else says about them.</param>
/// <param name="DocTitle">Title of a document or object that reveals it.</param>
/// <param name="DocText">Text of that document or object.</param>
/// <param name="SceneTag">How the scene evidence is labelled, e.g. "pipe smoker"; null if it can't appear at a scene.</param>
/// <param name="Scene">What the evidence looks like at the scene; null if it can't appear at a scene.</param>
public sealed record TraitValue(
    string Id,
    string Short,
    string Desc,
    string Self,
    string Other,
    string DocTitle,
    string DocText,
    string? SceneTag = null,
    string? Scene = null)
{
    public bool CanBeScene => Scene is not null;
}

/// <summary>A category of identifying characteristic, its values and how it's tested.</summary>
public sealed class TraitDef
{
    public required TraitCategory Category { get; init; }

    /// <summary>Column heading.</summary>
    public required string Label { get; init; }

    public TraitRule Rule { get; init; } = TraitRule.Match;

    public TraitFit Fit { get; init; } = TraitFit.Any;

    /// <summary>Plain to see on a person's face (drawn on their portrait), so nobody lies about it.</summary>
    public bool Visible { get; init; }

    public required IReadOnlyList<TraitValue> Values { get; init; }

    /// <summary>Picks a value for a new character.</summary>
    public required Func<Character, Random, string> Assign { get; init; }

    public IReadOnlyList<TraitValue> SceneValues => Values.Where(v => v.CanBeScene).ToList();

    public TraitValue Value(string id) => Values.FirstOrDefault(v => v.Id == id) ?? Values[0];

    /// <summary>A value that would clear someone, given what the scene shows.</summary>
    public string ClearingValue(string sceneValue, Random rng)
    {
        if (Rule == TraitRule.Avoid)
        {
            return sceneValue;
        }

        var others = Values.Where(v => v.Id != sceneValue).Select(v => v.Id).ToList();
        return rng.Pick(others);
    }

    /// <summary>A value that would make someone fit the scene.</summary>
    public string FittingValue(string sceneValue) =>
        Rule == TraitRule.Avoid ? Values.First(v => v.Id != sceneValue).Id : sceneValue;
}

/// <summary>Every identifying characteristic a case can draw on: forty of them.</summary>
public static class TraitCatalog
{
    private static readonly Dictionary<TraitCategory, TraitDef> Defs;

    static TraitCatalog()
    {
        Defs = Build().ToDictionary(d => d.Category);
    }

    public static IReadOnlyCollection<TraitDef> All => Defs.Values;

    public static TraitDef Get(TraitCategory category) => Defs[category];

    /// <summary>Fills the placeholders in a trait template.</summary>
    public static string Fill(string template, Character? who, string spill = "")
    {
        string text = template.Replace("{spill}", spill, StringComparison.Ordinal);
        if (who is null)
        {
            return text;
        }

        return text
            .Replace("{n}", who.ShortName, StringComparison.Ordinal)
            .Replace("{init}", who.Initials, StringComparison.Ordinal)
            .Replace("{He}", who.HeCap, StringComparison.Ordinal)
            .Replace("{he}", who.He, StringComparison.Ordinal)
            .Replace("{His}", who.HisCap, StringComparison.Ordinal)
            .Replace("{his}", who.His, StringComparison.Ordinal)
            .Replace("{him}", who.Him, StringComparison.Ordinal);
    }

    private static string Weighted(Random rng, params (string Value, double Weight)[] options)
    {
        double total = options.Sum(o => o.Weight);
        double roll = rng.NextDouble() * total;
        foreach (var (value, weight) in options)
        {
            roll -= weight;
            if (roll < 0)
            {
                return value;
            }
        }

        return options[^1].Value;
    }

    private static bool Servant(Character c) => c.Station == Station.Servant;

    private static string YesNo(Random rng, double chance, string yes, string no) => rng.Chance(chance) ? yes : no;

    private static IEnumerable<TraitDef> Build()
    {
        yield return new TraitDef
        {
            Category = TraitCategory.Smoking,
            Label = "Tobacco",
            Values = new TraitValue[]
            {
                new("None", "none", "doesn't smoke", "I've never touched tobacco. A filthy habit.", "{n} doesn't smoke. Won't have tobacco near {him}.",
                    "A temperance pledge", "A signed temperance card in {n}'s name, promising to abjure strong drink and tobacco alike."),
                new("Pipe", "pipe", "smokes a pipe", "I take a pipe of an evening; it settles the nerves.", "{n}'s never without that pipe. You can smell it on the stairs.",
                    "A briar pipe", "A well-used briar pipe and a pouch of shag tobacco, initialled {init}. {n}'s, plainly.",
                    "pipe smoker", "Fresh pipe dottle has been knocked out by the body, still faintly warm when it was found. The killer smokes a pipe."),
                new("Cigar", "cigar", "smokes cigars", "A cigar after dinner, nothing more.", "{n} smokes cigars. The curtains reek of them.",
                    "A cigar case", "A silver cigar case engraved for {n}, half full of Havanas.",
                    "cigar smoker", "A half-smoked cigar has been ground into the floor by the body. The killer smokes cigars."),
                new("Cigarette", "cigarettes", "smokes cigarettes", "The odd cigarette. Turkish, if you must know.", "{n} has taken up cigarettes. Very modern.",
                    "Turkish cigarettes", "A packet of Turkish cigarettes and a vesta case belonging to {n}.",
                    "cigarette smoker", "Two cigarette ends lie by the body, the paper still fresh. The killer smokes cigarettes."),
            },
            Assign = (c, rng) => rng.Chance(c.IsMale ? 0.6 : 0.25)
                ? (c.IsMale ? rng.Pick(new[] { "Pipe", "Cigar", "Cigarette" }) : "Cigarette")
                : "None",
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Handedness,
            Label = "Hand",
            Fit = TraitFit.Blow,
            Values = new TraitValue[]
            {
                new("Right", "right", "right-handed", "Right-handed, like any sensible person.", "{n}? Right-handed, I'm sure of it.",
                    "A neat letter", "A letter by {n} in a neat, right-slanting hand; the pen rests to the right of the blotter."),
                new("Left", "left", "left-handed", "I write with my left hand; they never could beat it out of me at school.", "{n} is left-handed. Writes all smudged and backwards.",
                    "A smudged letter", "A half-written letter by {n}: the ink is smeared along every line, as only a left-handed writer smudges it.",
                    "left-handed blow", "The wound is on the right side of the head, dealt from the front: the blow was struck left-handed."),
            },
            Assign = (_, rng) => YesNo(rng, 0.2, "Left", "Right"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.ShoeSize,
            Label = "Boots",
            Values = new TraitValue[]
            {
                new("Small", "small", "has small feet", "My boots? I've small feet; the cobbler says so every time.", "{n} has dainty little feet.",
                    "A pair of boots", "{n}'s boots stand by the bed: small and narrow.",
                    "small boot-print", "A clear boot-print in {spill} beside the body: small and narrow."),
                new("Medium", "medium", "has ordinary-sized feet", "My boots are an ordinary size. Nothing remarkable.", "{n}'s boots are an ordinary size. I've cleaned them, I'd know.",
                    "A pair of boots", "{n}'s boots stand by the bed: of quite ordinary size.",
                    "medium boot-print", "A clear boot-print in {spill} beside the body: of quite ordinary size."),
                new("Large", "large", "has large feet", "I've feet like barges. My boots are made special in the village.", "{n} has enormous feet. Leaves prints like a carthorse.",
                    "A pair of boots", "{n}'s boots stand by the bed: enormous.",
                    "large boot-print", "A clear boot-print in {spill} beside the body: enormous."),
            },
            Assign = (c, rng) => c.IsMale
                ? Weighted(rng, ("Small", 0.1), ("Medium", 0.45), ("Large", 0.45))
                : Weighted(rng, ("Small", 0.55), ("Medium", 0.4), ("Large", 0.05)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Allergy,
            Label = "Allergy",
            Rule = TraitRule.Avoid,
            Values = new TraitValue[]
            {
                new("None", "none", "has no allergies", "Nothing troubles my constitution, thank you.", "{n} is as healthy as a horse.",
                    "A medical certificate", "An insurance medical certificate for {n}: \"Robust constitution. No sensitivities of any kind.\""),
                new("Lilies", "lilies", "is allergic to lilies", "Lilies set me sneezing something terrible. I can't stay in a room with them.", "{n} can't abide lilies. Sneezes fit to wake the dead.",
                    "A prescription", "A prescription for {n}: \"For the hay fever brought on by lilies. Avoid the flowers entirely.\"",
                    "lilies present", "A great vase of fresh lilies stands by the body, their pollen dusting everything. Anyone sensitive to lilies would have been sneezing helplessly."),
                new("Cats", "cats", "is allergic to cats", "I can't be near that cat; my eyes stream for hours.", "{n} goes red and streaming if the cat comes near.",
                    "A prescription", "A prescription for {n}: \"For the violent reaction to cats. Keep the animal away.\"",
                    "cat present", "The house cat was found shut in with the body; its hairs are on everything. Anyone who reacts to cats could not have endured it."),
                new("Horsehair", "horsehair", "is allergic to horsehair", "Horsehair brings me out in hives, so I keep off the settees.", "{n} won't sit on a horsehair settee. Comes out in hives.",
                    "A prescription", "A prescription for {n}: \"Ointment for the hives caused by horsehair.\"",
                    "horsehair everywhere", "A horsehair cushion was torn open in the struggle and stuffing is everywhere. Anyone sensitive to horsehair would be covered in hives."),
                new("Dust", "dust", "has a dust-sensitive chest", "Dust sets my chest wheezing. I keep out of the shut-up rooms.", "{n} wheezes at the first speck of dust.",
                    "A prescription", "A prescription for {n}: \"For the asthma aggravated by dust. Avoid unaired rooms.\"",
                    "thick dust raised", "The struggle raised a great cloud of dust from the old hangings. Anyone with a dust-sensitive chest would have been choking."),
            },
            Assign = (_, rng) => rng.Chance(0.35) ? rng.Pick(new[] { "Lilies", "Cats", "Horsehair", "Dust" }) : "None",
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Strength,
            Label = "Strength",
            Fit = TraitFit.Blow,
            Values = new TraitValue[]
            {
                new(TraitRules.Sound, "sound", "able-bodied", "I'm hale enough, thank you. Strong as an ox.", "{n}'s as strong as a navvy.",
                    "A pair of dumb-bells", "A pair of iron dumb-bells, plainly well used by {n}.",
                    "great strength", "The blow was delivered with terrible force, and the body was dragged several feet. No feeble or injured hand did this."),
                new(TraitRules.Frail, "frail", "frail or injured", "My back has been bad all winter. I can barely lift a teacup this week.", "{n} is in a bad way. Gout, or a bad back; can barely lift a cup.",
                    "Liniment and a sling", "A bottle of liniment and a sling, with a doctor's note: \"{n}: no lifting of any kind for a fortnight.\""),
            },
            Assign = (c, rng) => YesNo(rng, c.Role switch
            {
                RoleKind.Colonel or RoleKind.Reverend => 0.3,
                RoleKind.Housekeeper or RoleKind.Gardener => 0.25,
                _ => 0.1,
            }, TraitRules.Frail, TraitRules.Sound),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Height,
            Label = "Height",
            Values = new TraitValue[]
            {
                new("Short", "short", "is short", "I'm no taller than a schoolboy, {DET}, as you can see.", "{n}'s a little slip of a thing. Needs the steps for the top shelf.",
                    "A tailor's measure", "A tailor's card for {n}: \"Height five feet and one inch. Take up all hems.\"",
                    "a short reach", "The candle on the high bracket was never touched, though it would have served better than the one used: someone short, who couldn't reach."),
                new("Average", "middling", "is of middling height", "I'm of a common height, neither here nor there.", "{n}'s neither tall nor short. Ordinary.",
                    "A tailor's measure", "A tailor's card for {n}: \"Height five feet and seven inches. Regular fitting.\""),
                new("Tall", "tall", "is tall", "I'm forever cracking my head on these old lintels.", "{n} is a great tall beanpole. Has to duck under the doors.",
                    "A tailor's measure", "A tailor's card for {n}: \"Height six feet and two inches. Extra length in sleeve and leg.\"",
                    "a tall killer", "A smear of blood on the top of the door frame, higher than most could reach: the killer is tall."),
            },
            Assign = (c, rng) => c.IsMale
                ? Weighted(rng, ("Short", 0.15), ("Average", 0.5), ("Tall", 0.35))
                : Weighted(rng, ("Short", 0.4), ("Average", 0.5), ("Tall", 0.1)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.HairColour,
            Label = "Hair",
            Visible = true,
            Values = new TraitValue[]
            {
                new("Dark", "dark", "has dark brown hair", "My hair? Brown as a chestnut, always has been.", "{n} has dark brown hair.",
                    "A hairbrush", "{n}'s hairbrush, full of dark brown hairs.",
                    "a dark brown hair", "A single dark brown hair is caught in the victim's cuff button."),
                new("Black", "black", "has black hair", "Black as a crow's wing, my mother used to say.", "{n}'s hair is black as soot.",
                    "A hairbrush", "{n}'s hairbrush, full of coarse black hairs.",
                    "a black hair", "A single black hair is caught in the victim's cuff button."),
                new("Auburn", "auburn", "has red hair", "I'm a redhead and I've the temper to match, they tell me.", "{n} has red hair. Ginger as a fox.",
                    "A hairbrush", "{n}'s hairbrush, full of auburn hairs.",
                    "a red hair", "A single bright auburn hair is caught in the victim's cuff button."),
                new("Fair", "fair", "has fair hair", "I'm fair-haired, as you see.", "{n} is fair as straw.",
                    "A hairbrush", "{n}'s hairbrush, full of fair hairs.",
                    "a fair hair", "A single fair hair is caught in the victim's cuff button."),
                new("Grey", "grey", "has grey hair", "Grey as a badger, these days.", "{n} has gone quite grey.",
                    "A hairbrush", "{n}'s hairbrush, full of grey hairs.",
                    "a grey hair", "A single grey hair is caught in the victim's cuff button."),
            },
            Assign = (c, rng) => c.Role is RoleKind.Colonel or RoleKind.Reverend or RoleKind.Housekeeper && rng.Chance(0.6)
                ? "Grey"
                : Weighted(rng, ("Dark", 0.35), ("Black", 0.2), ("Auburn", 0.12), ("Fair", 0.23), ("Grey", 0.1)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Scent,
            Label = "Scent",
            Values = new TraitValue[]
            {
                new("None", "none", "wears no scent", "Scent? I'm not a dandy, {DET}.", "{n} never wears scent.",
                    "A plain washstand", "{n}'s washstand: carbolic soap and nothing else. Not a drop of scent."),
                new("Lavender", "lavender", "wears lavender water", "A little lavender water. My mother always wore it.", "{n} smells of lavender. You can tell where {he}'s been.",
                    "A bottle of scent", "A bottle of lavender water on {n}'s dressing table, nearly empty.",
                    "lavender scent", "The room still smells faintly of lavender water. Nobody who lives in here wears it."),
                new("Violets", "violets", "wears violet scent", "Parma violets. It's my one extravagance.", "{n} wears violet scent; it's too strong by half.",
                    "A bottle of scent", "A cut-glass bottle of Parma violet scent among {n}'s things.",
                    "violet scent", "There is a strong smell of violet scent about the body, as if someone wearing it leaned close."),
                new("BayRum", "bay rum", "wears bay rum", "A splash of bay rum after shaving. Nothing more.", "{n} smells of bay rum. Every morning, regular as the clock.",
                    "A bottle of bay rum", "A bottle of bay rum among {n}'s shaving things.",
                    "bay rum", "The victim's collar smells of bay rum, though the victim never wore it."),
                new("Sandalwood", "sandalwood", "wears sandalwood", "Sandalwood oil. I picked up the habit abroad.", "{n} reeks of sandalwood. Says it's from India.",
                    "A phial of sandalwood", "A small phial of sandalwood oil, wrapped in a handkerchief marked {init}.",
                    "sandalwood", "A heavy, sweet smell of sandalwood hangs in the room."),
            },
            Assign = (c, rng) => c.IsMale
                ? Weighted(rng, ("None", 0.55), ("BayRum", 0.3), ("Sandalwood", 0.1), ("Lavender", 0.05))
                : Weighted(rng, ("None", 0.4), ("Lavender", 0.3), ("Violets", 0.25), ("Sandalwood", 0.05)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Spectacles,
            Label = "Spectacles",
            Visible = true,
            Values = new TraitValue[]
            {
                new("None", "none", "doesn't wear spectacles", "My eyes are as sharp as they ever were.", "{n} has eyes like a hawk.",
                    "An oculist's note", "An oculist's note for {n}: \"Sight excellent. No correction required.\""),
                new("Spectacles", "spectacles", "wears spectacles", "I'm blind as a mole without my spectacles.", "{n} can't see a yard without {his} spectacles.",
                    "A spectacle case", "A spare pair of spectacles in a case marked with {n}'s name.",
                    "spectacle glass", "A tiny curved sliver of spectacle glass glints on the floor beside the body."),
            },
            Assign = (c, rng) => YesNo(rng, c.Role switch
            {
                RoleKind.Doctor => 0.7,
                RoleKind.Governess or RoleKind.Solicitor => 0.6,
                RoleKind.Housekeeper or RoleKind.Reverend or RoleKind.Colonel => 0.35,
                _ => 0.1,
            }, "Spectacles", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Gait,
            Label = "Gait",
            Values = new TraitValue[]
            {
                new("Sound", "sound", "walks soundly", "I walk five miles before breakfast.", "{n} walks quick and light.",
                    "Walking boots", "A pair of {n}'s walking boots, worn evenly on both heels."),
                new("Limp", "limp", "walks with a limp", "I've a bad knee. I limp, and I'm not ashamed of it.", "{n} has a limp. Drags the left foot.",
                    "A built-up boot", "One of {n}'s boots has a thickened sole and is worn down on one side: a limp.",
                    "a limping walker", "The prints across the floor are uneven: one foot set down firmly, the other dragged. The killer limps."),
            },
            Assign = (c, rng) => YesNo(rng, c.Role is RoleKind.Colonel or RoleKind.Gardener ? 0.3 : 0.1, "Limp", "Sound"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Gloves,
            Label = "Gloves",
            Values = new TraitValue[]
            {
                new("Kid", "kid", "wears kid gloves", "Kid gloves, always. One must keep one's hands nice.", "{n} is never without kid gloves.",
                    "Kid gloves", "A pair of pale kid gloves in {n}'s drawer.",
                    "kid glove", "A smear on the polished door plate shows the fine grain of a kid glove."),
                new("Wool", "wool", "wears woollen gloves", "Knitted gloves. My hands are always cold.", "{n} goes about in woollen gloves, even indoors.",
                    "Woollen gloves", "A pair of darned woollen gloves belonging to {n}.",
                    "wool fibres", "Grey wool fibres are snagged on the splintered edge of the desk: the killer wore woollen gloves."),
                new("Leather", "leather", "wears heavy leather gloves", "Leather gauntlets. You need them for rough work.", "{n} wears great leather gauntlets for the rough work.",
                    "Leather gauntlets", "A pair of scuffed leather gauntlets belonging to {n}.",
                    "heavy leather gloves", "The marks on the throat and the scuffed door handle were made by heavy leather gloves."),
                new("None", "none", "never wears gloves", "Gloves? I can't abide them.", "{n} never wears gloves. Hands like a labourer.",
                    "A pair of bare hands", "{n}'s drawer holds handkerchiefs and studs, but no gloves of any kind."),
            },
            Assign = (c, rng) => Servant(c)
                ? Weighted(rng, ("Wool", 0.4), ("Leather", 0.3), ("None", 0.3))
                : Weighted(rng, ("Kid", 0.6), ("Wool", 0.2), ("Leather", 0.1), ("None", 0.1)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.SignetRing,
            Label = "Ring",
            Values = new TraitValue[]
            {
                new("None", "none", "wears no ring", "I wear no rings. They catch on everything.", "{n} wears no rings at all.",
                    "A bare ring dish", "{n}'s ring dish holds collar studs and nothing else."),
                new("Signet", "signet", "wears a signet ring", "My grandfather's signet. I never take it off.", "{n} wears a heavy signet ring on the little finger.",
                    "A ring box", "An empty ring box stamped with {n}'s crest: the ring it holds is worn every day.",
                    "a signet ring", "A deep scratch in the varnish, and a bruise on the victim's temple, both bear the oval mark of a signet ring."),
            },
            Assign = (c, rng) => YesNo(rng, Servant(c) ? 0.1 : 0.45, "Signet", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Snuff,
            Label = "Snuff",
            Values = new TraitValue[]
            {
                new("No", "no", "doesn't take snuff", "Snuff? A disgusting habit.", "{n} doesn't take snuff.",
                    "A clean handkerchief", "{n}'s handkerchiefs are spotless: no snuff stains."),
                new("Yes", "snuff", "takes snuff", "A pinch of snuff clears the head wonderfully.", "{n} takes snuff. Brown stains on every handkerchief.",
                    "A snuff box", "An enamelled snuff box belonging to {n}, half full.",
                    "spilled snuff", "Grains of brown snuff are scattered on the victim's lapel."),
            },
            Assign = (c, rng) => YesNo(rng, c.IsMale ? 0.2 : 0.05, "Yes", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.BootNails,
            Label = "Soles",
            Values = new TraitValue[]
            {
                new("Smooth", "smooth", "wears smooth-soled shoes", "I wear proper shoes, not those clodhoppers.", "{n} wears smooth soles. Slips on the tiles.",
                    "Evening shoes", "{n}'s shoes: smooth leather soles, polished to a mirror.",
                    "smooth soles", "The scuffs on the floor were made by smooth leather soles: no nails."),
                new("Hobnailed", "hobnails", "wears hobnailed boots", "Hobnails. They last for ever.", "{n} clatters about in hobnailed boots. You hear {him} coming.",
                    "Hobnailed boots", "{n}'s boots, the soles studded with hobnails.",
                    "hobnails", "The floor is scored by hobnails: the killer wore nailed boots."),
            },
            Assign = (c, rng) => YesNo(rng, Servant(c) ? 0.6 : 0.1, "Hobnailed", "Smooth"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Sweets,
            Label = "Sweets",
            Values = new TraitValue[]
            {
                new("None", "none", "doesn't eat sweets", "Sweets rot the teeth.", "{n} never touches sweets.",
                    "A bare pocket", "{n}'s coat pockets hold nothing but a handkerchief."),
                new("Peppermints", "peppermints", "sucks peppermints", "I'm partial to a peppermint.", "{n} always has a peppermint on the go.",
                    "A bag of peppermints", "A paper bag of peppermints in {n}'s pocket.",
                    "a peppermint wrapper", "A twist of waxed paper that held a peppermint lies by the body."),
                new("Liquorice", "liquorice", "chews liquorice", "Liquorice. Good for the chest.", "{n} chews liquorice all day. Black teeth to show for it.",
                    "A tin of liquorice", "A tin of liquorice pieces belonging to {n}.",
                    "liquorice", "A half-chewed piece of liquorice is stuck to the floor."),
                new("BarleySugar", "barley sugar", "sucks barley sugar", "A barley sugar now and then, for my throat.", "{n} sucks barley sugar like a child.",
                    "A jar of barley sugar", "A jar of barley-sugar twists among {n}'s things.",
                    "barley sugar", "A sticky barley-sugar twist has been dropped by the body."),
            },
            Assign = (_, rng) => Weighted(rng, ("None", 0.55), ("Peppermints", 0.2), ("Liquorice", 0.12), ("BarleySugar", 0.13)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.ClothColour,
            Label = "Clothes",
            Values = new TraitValue[]
            {
                new("Black", "black", "dresses in black", "I always wear black.", "{n} dresses in black like a crow.",
                    "A wardrobe", "{n}'s wardrobe: all black.",
                    "a black thread", "A thread of black cloth is caught on a nail by the door."),
                new("Grey", "grey", "dresses in grey", "Grey is serviceable.", "{n} is always in grey.",
                    "A wardrobe", "{n}'s wardrobe: grey, and more grey.",
                    "a grey thread", "A thread of grey cloth is caught on a nail by the door."),
                new("Navy", "navy", "dresses in navy blue", "Navy blue, as a rule.", "{n} wears navy blue.",
                    "A wardrobe", "{n}'s wardrobe: navy serge and blue wool.",
                    "a navy thread", "A thread of navy-blue cloth is caught on a nail by the door."),
                new("Crimson", "crimson", "wears crimson", "I like a little colour. Crimson, mostly.", "{n} wears crimson. Hard to miss.",
                    "A wardrobe", "{n}'s wardrobe: crimson velvet and red wool.",
                    "a crimson thread", "A thread of crimson cloth is caught on a nail by the door."),
                new("Green", "green", "wears green", "Bottle green suits me.", "{n} is forever in green.",
                    "A wardrobe", "{n}'s wardrobe: bottle green, top to bottom.",
                    "a green thread", "A thread of bottle-green cloth is caught on a nail by the door."),
                new("Brown", "brown", "wears brown", "Brown tweed, mostly.", "{n} wears brown tweed.",
                    "A wardrobe", "{n}'s wardrobe: brown tweeds and fustian.",
                    "a brown thread", "A thread of brown tweed is caught on a nail by the door."),
            },
            Assign = (c, rng) => Servant(c)
                ? Weighted(rng, ("Black", 0.5), ("Grey", 0.2), ("Brown", 0.2), ("Navy", 0.1))
                : Weighted(rng, ("Black", 0.15), ("Grey", 0.15), ("Navy", 0.2), ("Crimson", 0.15), ("Green", 0.15), ("Brown", 0.2)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Rouge,
            Label = "Rouge",
            Values = new TraitValue[]
            {
                new("No", "no", "wears no rouge", "Paint? Certainly not.", "{n} never paints {his} face.",
                    "A plain dressing table", "{n}'s dressing table: soap, a comb, nothing more."),
                new("Yes", "rouge", "wears rouge", "A touch of rouge. Everyone does now.", "{n} paints {his} cheeks. Thinks nobody notices.",
                    "A pot of rouge", "A small pot of rouge hidden at the back of {n}'s drawer.",
                    "a smear of rouge", "There is a smear of rouge on the rim of the victim's glass, and on the cuff of the victim's sleeve."),
            },
            Assign = (c, rng) => YesNo(rng, c.IsMale ? 0.03 : Servant(c) ? 0.1 : 0.45, "Yes", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.HairOil,
            Label = "Hair oil",
            Values = new TraitValue[]
            {
                new("None", "none", "uses no hair oil", "I wash my hair in rainwater. Nothing else.", "{n} uses no oil on {his} hair.",
                    "A clean pillow", "{n}'s pillowcase is perfectly clean: no hair oil."),
                new("Macassar", "macassar", "uses Macassar oil", "Macassar oil. It keeps the hair in order.", "{n} plasters {his} hair with Macassar oil.",
                    "A bottle of Macassar oil", "A bottle of Macassar oil on {n}'s washstand.",
                    "Macassar oil", "There's a greasy mark of Macassar oil on the antimacassar of the chair by the body: someone leaned back there."),
                new("Pomade", "pomade", "uses pomade", "A little pomade. I'm not vain.", "{n} smells of rose pomade.",
                    "A jar of pomade", "A jar of rose pomade among {n}'s things.",
                    "rose pomade", "A smear of rose-scented pomade on the high wing of the chair by the body."),
            },
            Assign = (c, rng) => c.IsMale
                ? Weighted(rng, ("None", 0.45), ("Macassar", 0.4), ("Pomade", 0.15))
                : Weighted(rng, ("None", 0.6), ("Pomade", 0.35), ("Macassar", 0.05)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Buttons,
            Label = "Buttons",
            Values = new TraitValue[]
            {
                new("Brass", "brass", "wears brass buttons", "Brass buttons. They polish up nicely.", "{n}'s coat has brass buttons.",
                    "A coat", "{n}'s coat, with a row of brass buttons, one loose.",
                    "a brass button", "A brass button, torn off with a scrap of thread, lies under the body."),
                new("Jet", "jet", "wears jet buttons", "Jet. I'm still in half-mourning.", "{n} wears jet buttons. Black as night.",
                    "A coat", "{n}'s coat, with jet buttons, one missing its thread.",
                    "a jet button", "A jet button, torn off with a scrap of thread, lies under the body."),
                new("Horn", "horn", "wears horn buttons", "Plain horn buttons. Nothing showy.", "{n} has horn buttons on everything.",
                    "A coat", "{n}'s coat, with plain horn buttons.",
                    "a horn button", "A horn button, torn off with a scrap of thread, lies under the body."),
                new("Pearl", "pearl", "wears pearl buttons", "Mother-of-pearl. A small vanity.", "{n}'s waistcoat has mother-of-pearl buttons.",
                    "A waistcoat", "{n}'s waistcoat, with mother-of-pearl buttons.",
                    "a pearl button", "A mother-of-pearl button, torn off with a scrap of thread, lies under the body."),
            },
            Assign = (c, rng) => Servant(c)
                ? Weighted(rng, ("Brass", 0.45), ("Horn", 0.45), ("Jet", 0.1))
                : Weighted(rng, ("Jet", 0.3), ("Pearl", 0.35), ("Horn", 0.2), ("Brass", 0.15)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Knots,
            Label = "Knots",
            Values = new TraitValue[]
            {
                new("Landsman", "landsman", "knows nothing of knots", "Knots? I can barely tie my bootlaces.", "{n} couldn't tie a knot to save {his} life.",
                    "A tangle of string", "A hopeless tangle of string in {n}'s drawer."),
                new("Sailor", "sailor", "ties a sailor's knots", "I was at sea as a boy. I can tie anything.", "{n} was a sailor once. Ties everything with proper knots.",
                    "A ship's papers", "An old ship's discharge paper for {n}, rated able seaman.",
                    "a sailor's knot", "The curtain cord that held the door was tied in a neat bowline: a sailor's knot."),
            },
            Assign = (c, rng) => YesNo(rng, c.Role is RoleKind.Colonel or RoleKind.Gardener or RoleKind.Valet or RoleKind.Butler ? 0.25 : 0.08, "Sailor", "Landsman"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Medicine,
            Label = "Medicine",
            Fit = TraitFit.Poison,
            Values = new TraitValue[]
            {
                new("Untrained", "untrained", "knows nothing of medicine", "I couldn't tell one powder from another.", "{n} knows nothing about medicine.",
                    "A household almanac", "{n}'s almanac, its page on remedies untouched."),
                new("Trained", "trained", "has medical training", "I've had some training in nursing and physic.", "{n} has medical training. Nursed in the war, they say.",
                    "A nursing certificate", "A certificate of training in nursing and dispensing, made out to {n}.",
                    "a practised hand", "The dose was measured to the grain and the glass rinsed with care: whoever did this knows medicine."),
            },
            Assign = (c, rng) => c.Role == RoleKind.Doctor ? "Trained" : YesNo(rng, c.Role is RoleKind.Housekeeper or RoleKind.Governess ? 0.35 : 0.08, "Trained", "Untrained"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Literacy,
            Label = "Letters",
            Values = new TraitValue[]
            {
                new("Literate", "reads", "reads and writes", "I read a good deal, and write a fair hand.", "{n} reads anything put in front of {him}.",
                    "A writing case", "{n}'s writing case: ink, nibs and half a dozen letters.",
                    "a written lure", "A note found on the body, in a careful hand, asked the victim to come here at this hour. The killer can write."),
                new("Unlettered", "can't read", "can't read or write", "I never had my letters, {SIR}. I make my mark.", "{n} can't read. Has me read {his} letters out.",
                    "A wage book", "{n}'s wage book, signed each quarter with a cross."),
            },
            Assign = (c, rng) => YesNo(rng, Servant(c) ? 0.3 : 0.0, "Unlettered", "Literate"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Ink,
            Label = "Ink",
            Values = new TraitValue[]
            {
                new("Black", "black", "writes in black ink", "Black ink. What else is there?", "{n} writes in black ink.",
                    "An inkwell", "{n}'s inkwell, filled with black ink.",
                    "black ink", "An inky thumbprint on the door plate: black ink."),
                new("Blue", "blue", "writes in blue ink", "Blue-black ink, always.", "{n} uses blue ink.",
                    "An inkwell", "{n}'s inkwell, filled with blue ink.",
                    "blue ink", "An inky thumbprint on the door plate: blue ink."),
                new("Violet", "violet", "writes in violet ink", "Violet ink. It's rather pretty.", "{n} writes everything in violet ink.",
                    "An inkwell", "{n}'s inkwell, filled with violet ink.",
                    "violet ink", "An inky thumbprint on the door plate: violet ink."),
                new("Green", "green", "writes in green ink", "Green ink. An eccentricity of mine.", "{n} writes in green ink, of all things.",
                    "An inkwell", "{n}'s inkwell, filled with green ink.",
                    "green ink", "An inky thumbprint on the door plate: green ink."),
            },
            Assign = (_, rng) => Weighted(rng, ("Black", 0.45), ("Blue", 0.3), ("Violet", 0.15), ("Green", 0.1)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Keys,
            Label = "Keys",
            Values = new TraitValue[]
            {
                new("None", "none", "holds no house keys", "I've no keys. I ring for whatever I need.", "{n} hasn't a key to anything.",
                    "An empty key hook", "The hook by {n}'s door is empty: no keys issued."),
                new("Keys", "keys", "holds a set of house keys", "I carry the house keys, of course.", "{n} has a full set of house keys.",
                    "A ring of keys", "A heavy ring of house keys, labelled in {n}'s hand.",
                    "a key-holder", "The door was locked from outside, with a key: no forcing, no scratches. The killer holds a house key."),
            },
            Assign = (c, rng) => c.Role is RoleKind.Butler or RoleKind.Housekeeper or RoleKind.Spouse ? "Keys" : YesNo(rng, Servant(c) ? 0.3 : 0.15, "Keys", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Dog,
            Label = "The dog",
            Values = new TraitValue[]
            {
                new("Familiar", "known", "is known to the dog", "The terrier knows me. Follows me about.", "The dog adores {n}. Never makes a sound at {him}.",
                    "A dog's lead", "The terrier's lead hangs in {n}'s room; {he} walks it most days.",
                    "a silent dog", "The terrier sleeps across the threshold and barks at any stranger, yet nobody heard a sound from it all night. The killer is someone the dog knows."),
                new("Stranger", "stranger", "is a stranger to the dog", "That dog barks at me every time I cross the hall.", "The dog can't abide {n}. Barks the house down.",
                    "A torn trouser hem", "A trouser hem of {n}'s, torn by the terrier's teeth."),
            },
            Assign = (c, rng) => Servant(c) || c.Station == Station.Family ? "Familiar" : YesNo(rng, 0.6, "Stranger", "Familiar"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Scratches,
            Label = "Scratches",
            Fit = TraitFit.Blow,
            Values = new TraitValue[]
            {
                new("Unmarked", "unmarked", "has no scratches", "Not a mark on me. See for yourself.", "{n}'s hands are unmarked. I noticed at breakfast.",
                    "A clean towel", "{n}'s washstand towel is spotless."),
                new("Scratched", "scratched", "has fresh scratches", "I scratched my hand on the rose bushes, that's all.", "{n} has fresh scratches on {his} wrist. Says it was the cat.",
                    "A bloodied towel", "A towel in {n}'s room, spotted with blood from a scratch.",
                    "a scratched killer", "There is skin and blood under the victim's fingernails. The victim fought, and the killer will bear fresh scratches."),
            },
            Assign = (_, rng) => YesNo(rng, 0.12, "Scratched", "Unmarked"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Accent,
            Label = "Voice",
            Values = new TraitValue[]
            {
                new("Southern", "southern", "speaks the Queen's English", "I speak as I was taught at school.", "{n} speaks very proper. Southern.",
                    "A school report", "An old school report for {n}, from a good southern school.",
                    "a southern voice", "The scullery boy, passing, heard raised voices: the other voice spoke very proper, southern English."),
                new("Northern", "northern", "speaks with a northern accent", "I'm from Yorkshire, and I'll not hide it.", "{n} is from up north. You can hear it.",
                    "A letter from home", "A letter to {n} from family in Leeds.",
                    "a northern voice", "The scullery boy, passing, heard raised voices: the other voice was flat and northern."),
                new("Scots", "Scots", "speaks with a Scots burr", "Aberdeen born and bred.", "{n} is a Scot. Rolls {his} r's.",
                    "A letter from home", "A letter to {n} from family in Aberdeen.",
                    "a Scots voice", "The scullery boy, passing, heard raised voices: the other voice had a Scots burr."),
                new("Irish", "Irish", "speaks with an Irish lilt", "County Cork, {SIR}. I don't deny it.", "{n} is Irish. Lovely voice.",
                    "A letter from home", "A letter to {n} from family in Cork.",
                    "an Irish voice", "The scullery boy, passing, heard raised voices: the other voice had an Irish lilt."),
                new("Welsh", "Welsh", "speaks with a Welsh lilt", "I'm from the valleys.", "{n} is Welsh. Sings in the bath.",
                    "A letter from home", "A letter to {n} from family in Merthyr.",
                    "a Welsh voice", "The scullery boy, passing, heard raised voices: the other voice was Welsh."),
                new("WestCountry", "West Country", "speaks with a West Country burr", "Somerset, born and bred.", "{n}'s West Country. Talks slow.",
                    "A letter from home", "A letter to {n} from family in Taunton.",
                    "a West Country voice", "The scullery boy, passing, heard raised voices: the other voice had a slow West Country burr."),
            },
            Assign = (c, rng) => Servant(c)
                ? Weighted(rng, ("Northern", 0.25), ("Scots", 0.15), ("Irish", 0.2), ("Welsh", 0.15), ("WestCountry", 0.15), ("Southern", 0.1))
                : Weighted(rng, ("Southern", 0.7), ("Scots", 0.1), ("Northern", 0.1), ("Irish", 0.1)),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Candle,
            Label = "Candle",
            Values = new TraitValue[]
            {
                new("Beeswax", "beeswax", "lights beeswax candles", "Beeswax candles, naturally.", "{n} burns beeswax. The best.",
                    "A candlestick", "{n}'s bedroom candlestick, with a stub of good beeswax.",
                    "beeswax drips", "Drips of pale beeswax candle on the floor by the body: the killer carried a beeswax candle."),
                new("Tallow", "tallow", "lights tallow candles", "Tallow dips. We're not given beeswax below stairs.", "{n} uses tallow dips. Smokes something awful.",
                    "A tallow dip", "{n}'s candlestick, with a greasy stub of tallow.",
                    "tallow drips", "Drips of yellow tallow on the floor by the body: the killer carried a tallow dip."),
            },
            Assign = (c, rng) => Servant(c) ? YesNo(rng, 0.85, "Tallow", "Beeswax") : YesNo(rng, 0.9, "Beeswax", "Tallow"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Tremor,
            Label = "Hands",
            Values = new TraitValue[]
            {
                new("Steady", "steady", "has steady hands", "Steady as a rock, my hands.", "{n} has the steadiest hands in the house.",
                    "A fine embroidery", "A piece of very fine needlework by {n}: every stitch exact.",
                    "a steady hand", "Not a drop spilled, not a grain dropped: whoever did this had perfectly steady hands."),
                new("Tremor", "shaky", "has shaking hands", "My hands shake. Age, I suppose.", "{n}'s hands shake so {he} can barely hold a cup.",
                    "Spilled ink", "{n}'s letters: the writing wavers, and ink is spilled across the blotter.",
                    "a shaking hand", "Powder and drops are spilled everywhere around the glass: the hand that did this was shaking."),
            },
            Assign = (c, rng) => YesNo(rng, c.Role is RoleKind.Colonel or RoleKind.Reverend ? 0.3 : 0.1, "Tremor", "Steady"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Riding,
            Label = "Riding",
            Values = new TraitValue[]
            {
                new("No", "no", "doesn't ride", "I never sit a horse if I can help it.", "{n} won't go near a horse.",
                    "Town shoes", "{n}'s shoes: no riding boots of any kind."),
                new("Rides", "rides", "rides", "I ride out every morning.", "{n} is always in the saddle.",
                    "Riding boots", "{n}'s riding boots, spurs still buckled on.",
                    "a rider's spur", "A long scratch in the floor, of the sort a spur makes: the killer wore riding boots."),
            },
            Assign = (c, rng) => YesNo(rng, Servant(c) ? 0.15 : 0.5, "Rides", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Botany,
            Label = "Plants",
            Fit = TraitFit.Poison,
            Values = new TraitValue[]
            {
                new("No", "no", "knows nothing of plants", "One flower looks much like another to me.", "{n} can't tell a rose from a cabbage.",
                    "A pressed-flower album", "A pressed-flower album of {n}'s, every label wrong."),
                new("Knows", "botanist", "knows plants well", "I know my plants. Every one in the conservatory.", "{n} knows every plant in the garden, and which will kill you.",
                    "A botany notebook", "{n}'s botany notebook, with a page on poisonous plants heavily annotated.",
                    "a knowing poisoner", "The poison was drawn from a garden plant, prepared just so: whoever did it knows plants."),
            },
            Assign = (c, rng) => c.Role == RoleKind.Gardener ? "Knows" : YesNo(rng, 0.2, "Knows", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Vertigo,
            Label = "Heights",
            Rule = TraitRule.Avoid,
            Values = new TraitValue[]
            {
                new("None", "fine", "has a head for heights", "Heights don't trouble me.", "{n} would climb the church tower for a wager.",
                    "A climbing photograph", "A photograph of {n} perched on an Alpine ridge."),
                new("Vertigo", "vertigo", "suffers from vertigo", "I can't climb so much as a stepladder. Vertigo.", "{n} has vertigo. Won't even go up the library ladder.",
                    "A doctor's note", "A doctor's note for {n}: \"Severe vertigo. Avoid ladders, ledges and stairs without rail.\"",
                    "a climb", "The window was reached from outside along the narrow ledge, a long drop below. No one with a fear of heights could have done it."),
            },
            Assign = (_, rng) => YesNo(rng, 0.15, "Vertigo", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.WalkingStick,
            Label = "Stick",
            Values = new TraitValue[]
            {
                new("None", "none", "carries no stick", "I need no stick, thank you.", "{n} walks without a stick.",
                    "An empty stand", "{n} has no walking stick among {his} things."),
                new("Cane", "stick", "carries a walking stick", "I'm never without my stick.", "{n} goes about with a walking stick. Taps it on the floor.",
                    "A walking stick", "A walking stick with a worn brass ferrule, belonging to {n}.",
                    "a stick's ferrule", "Small round marks beside the footprints, every other step: the killer carries a walking stick."),
            },
            Assign = (c, rng) => YesNo(rng, c.Station == Station.Servant ? 0.05 : 0.35, "Cane", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.WatchChain,
            Label = "Watch",
            Values = new TraitValue[]
            {
                new("None", "none", "carries no watch", "I've no watch. I go by the hall clock.", "{n} hasn't a watch to {his} name.",
                    "A bare waistcoat", "{n}'s waistcoat has no watch pocket worn at all."),
                new("Chain", "chain", "wears a watch on a chain", "My father's watch, on its chain.", "{n} is forever checking that watch on its chain.",
                    "A watch chain", "A watch chain of {n}'s, one link newly mended.",
                    "a broken chain link", "A single gilt link from a watch chain lies under the body, prised open in the struggle."),
            },
            Assign = (c, rng) => YesNo(rng, Servant(c) ? 0.25 : 0.6, "Chain", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.GoldTooth,
            Label = "Teeth",
            Values = new TraitValue[]
            {
                new("None", "own", "has their own teeth", "My own teeth, every one.", "{n} has a fine set of teeth.",
                    "A dentist's bill", "A dentist's bill for {n}: a cleaning, nothing more."),
                new("Gold", "gold tooth", "has a gold tooth", "A gold tooth. I lost the old one in a fall.", "{n} has a gold tooth. Flashes when {he} smiles.",
                    "A dentist's bill", "A dentist's bill for {n}: \"To one gold crown, fitted.\"",
                    "a gold tooth", "The bootboy saw a figure on the stairs, lit by a candle: \"Smiled at me, and there was a flash of gold in the mouth.\""),
            },
            Assign = (_, rng) => YesNo(rng, 0.1, "Gold", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Cyclist,
            Label = "Bicycle",
            Values = new TraitValue[]
            {
                new("No", "no", "doesn't cycle", "A bicycle? I'd break my neck.", "{n} wouldn't be seen on a bicycle.",
                    "A pair of town shoes", "{n}'s things: no cycling clips, no oil."),
                new("Cycles", "cycles", "rides a bicycle", "I cycle everywhere. Splendid exercise.", "{n} goes about on one of those new bicycles.",
                    "Trouser clips", "A pair of cycling clips and an oil can belonging to {n}.",
                    "bicycle oil", "A smear of bicycle chain oil on the door frame, at knee height."),
            },
            Assign = (c, rng) => YesNo(rng, c.Station == Station.Family ? 0.35 : 0.15, "Cycles", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Tattoo,
            Label = "Tattoo",
            Values = new TraitValue[]
            {
                new("None", "none", "has no tattoo", "Tattoos are for sailors and convicts.", "{n} hasn't a mark on {him}.",
                    "A clean bill", "A medical record for {n}: \"No distinguishing marks.\""),
                new("Tattoo", "tattoo", "has a tattoo on the wrist", "A little anchor on my wrist. A youthful folly.", "{n} has a tattoo on the wrist. Keeps it covered.",
                    "A medical record", "A medical record for {n}: \"Distinguishing marks: a tattoo on the left wrist.\"",
                    "a tattoo glimpsed", "The bootboy, who saw a hand on the banister by candlelight, swears there was a blue tattoo on the wrist."),
            },
            Assign = (c, rng) => YesNo(rng, c.IsMale ? 0.12 : 0.03, "Tattoo", "None"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Pianist,
            Label = "Piano",
            Values = new TraitValue[]
            {
                new("No", "no", "can't play the piano", "I've no ear for music.", "{n} can't play a note.",
                    "Unopened music", "{n}'s sheet music lies uncut in its wrapper."),
                new("Plays", "plays", "plays the piano", "I play a little. Chopin, mostly.", "{n} plays the piano beautifully.",
                    "Sheet music", "Well-thumbed piano music with {n}'s pencilled fingerings.",
                    "the piano played", "The household heard the piano played loud at this hour, to cover whatever else was done: someone who plays."),
            },
            Assign = (c, rng) => YesNo(rng, c.Station == Station.Servant ? 0.08 : 0.5, "Plays", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Photography,
            Label = "Camera",
            Values = new TraitValue[]
            {
                new("No", "no", "isn't a photographer", "Photography? Smelly business.", "{n} has no time for photography.",
                    "Clean fingers", "{n}'s gloves and handkerchiefs: no chemical stains at all."),
                new("Yes", "camera", "is an amateur photographer", "I take photographs. I've a darkroom set up.", "{n} is mad for photography. Black stains on {his} fingers.",
                    "A camera", "A camera and a box of plates belonging to {n}, and a bottle of silver nitrate.",
                    "silver nitrate stains", "Black silver-nitrate stains on the door plate: the marks a photographer's fingers leave."),
            },
            Assign = (c, rng) => YesNo(rng, c.Station == Station.Servant ? 0.03 : 0.25, "Yes", "No"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Stammer,
            Label = "Speech",
            Values = new TraitValue[]
            {
                new("Clear", "clear", "speaks clearly", "I speak plainly, I hope.", "{n} speaks clear as a bell.",
                    "Elocution prizes", "An elocution prize certificate for {n}."),
                new("Stammer", "stammer", "stammers", "I st-stammer, when I'm upset. Forgive me.", "{n} has a stammer. Worse when {he}'s angry.",
                    "A speech therapist's card", "A card from a speech physician in London, made out to {n}.",
                    "a stammering voice", "The kitchen maid heard the quarrel through the wall. The killer stammered over every hard word."),
            },
            Assign = (_, rng) => YesNo(rng, 0.1, "Stammer", "Clear"),
        };

        yield return new TraitDef
        {
            Category = TraitCategory.Sleep,
            Label = "Sleep",
            Rule = TraitRule.Avoid,
            Values = new TraitValue[]
            {
                new("None", "wakeful", "sleeps lightly", "I sleep badly. I'm awake half the night.", "{n} is up at all hours.",
                    "A half-burned candle", "{n}'s candle is burned right down: {he} reads late."),
                new("Draught", "draught", "takes a sleeping draught", "I take a draught at bedtime; nothing wakes me after.", "{n} takes a sleeping draught every night. Dead to the world.",
                    "A sleeping draught", "A bottle of sleeping draught by {n}'s bed, the dose marked on the label.",
                    "late at night", "The killer moved about the house wide awake at an hour when anyone who'd taken a sleeping draught would have been dead to the world."),
            },
            Assign = (_, rng) => YesNo(rng, 0.15, "Draught", "None"),
        };
    }
}
