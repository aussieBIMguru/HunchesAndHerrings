namespace HunchesAndHerrings.Core.Model;

/// <summary>A murder weapon, the room it was taken from, and how it reads as evidence.</summary>
public sealed record Weapon(string Id, string Name, string OriginRoomId, bool Heavy, string SceneText, string MissingText, bool Poison = false);

/// <summary>All weapons a case can use.</summary>
public static class WeaponCatalog
{
    public static IReadOnlyList<Weapon> All { get; } = new[]
    {
        new Weapon("candlestick", "Silver candlestick", RoomIds.Dining, true,
            "A heavy silver candlestick lies beneath the furniture, its base dented and dark.",
            "One of the pair of silver candlesticks is missing from the sideboard; a clean ring marks where it stood."),
        new Weapon("poker", "Fire poker", RoomIds.Drawing, true,
            "A brass-handled fire poker lies on the rug, bent at the tip.",
            "The companion set by the drawing-room hearth is short its poker."),
        new Weapon("letteropener", "Letter opener", RoomIds.Study, false,
            "A slim steel letter opener lies near the body, hastily wiped on a curtain.",
            "The desk tray holds pens and sealing wax, but the letter opener is gone."),
        new Weapon("bottle", "Wine bottle", RoomIds.WineCellar, true,
            "Shards of a claret bottle glitter around the body; only the neck is whole.",
            "A gap in the oldest rack: one bottle of the '68 claret is missing."),
        new Weapon("shears", "Pruning shears", RoomIds.Conservatory, false,
            "A pair of pruning shears, the blades stained, has been tossed behind a planter.",
            "The hook above the potting bench where the pruning shears hang is empty."),
        new Weapon("cane", "Silver-topped cane", RoomIds.Hall, false,
            "A silver-topped walking cane lies across the threshold, its knob dented.",
            "The hall stand holds umbrellas and two canes; a third, silver-topped, is missing."),
        new Weapon("bookend", "Brass bookend", RoomIds.Library, true,
            "A brass bookend in the shape of a lion lies by the body.",
            "On the third shelf a row of books has slumped sideways: its brass bookend is gone."),
        new Weapon("rollingpin", "Marble rolling pin", RoomIds.Kitchen, true,
            "A floury marble rolling pin lies on the floor, utterly out of place.",
            "The marble rolling pin is missing from its rack above the pastry board."),
        new Weapon("cue", "Billiard cue", RoomIds.Billiard, true,
            "A billiard cue, snapped in two, lies by the body.",
            "The cue rack has a gap; one cue is unaccounted for."),
        new Weapon("flatiron", "Flat iron", RoomIds.Scullery, true,
            "A cast-iron flat iron sits incongruously on the floor beside the body.",
            "One of the three flat irons is missing from beside the scullery range."),
        new Weapon("mallet", "Croquet mallet", RoomIds.Gardens, true,
            "A croquet mallet, its head split, lies half hidden under the furniture.",
            "The croquet box by the garden door is one mallet short."),
        new Weapon("bust", "Marble bust", RoomIds.Gallery, true,
            "A small marble bust of Lord Palmerston lies on its side by the body, one ear chipped.",
            "In the Long Gallery an empty plinth stands where a marble bust used to be."),
        new Weapon("sabre", "Cavalry sabre", RoomIds.Gallery, false,
            "An old cavalry sabre lies by the body, the blade wiped but the guard still dark.",
            "Two brackets on the gallery wall hold nothing: the crossed sabres are now one."),
        new Weapon("cord", "Curtain cord", RoomIds.Drawing, false,
            "A length of tasselled curtain cord lies beside the body. The marks on the throat match it.",
            "One drawing-room curtain hangs loose: its tasselled cord has been cut away."),
        new Weapon("decanter", "Cut-glass decanter", RoomIds.Dining, true,
            "A heavy cut-glass decanter lies by the body, its stopper gone and its base clouded with blood.",
            "The tantalus on the dining-room sideboard is missing its port decanter."),
    };

    /// <summary>Poisons, for the quieter kind of murder.</summary>
    public static IReadOnlyList<Weapon> Poisons { get; } = new[]
    {
        new Weapon("arsenic", "Arsenic", RoomIds.Scullery, false,
            "A glass by the body holds a gritty white residue. The victim's lips are blue and the room smells of vomit: arsenic.",
            "The tin of arsenical rat poison under the scullery sink is much lighter than the last entry in the poison book.", true),
        new Weapon("digitalis", "Foxglove (digitalis)", RoomIds.Conservatory, false,
            "A teacup by the body holds a greenish, bitter dreg. The victim's pulse, the doctor says, must have slowed to nothing: digitalis, from foxglove.",
            "In the conservatory a row of potted foxgloves has been stripped of its leaves.", true),
        new Weapon("prussic", "Prussic acid", RoomIds.Boiler, false,
            "The victim's glass smells faintly of bitter almonds. Prussic acid kills in moments.",
            "The jar of cyanide kept in the boiler room for the wasps' nests has been opened; the wax seal is broken.", true),
        new Weapon("laudanum", "Laudanum", RoomIds.Master, false,
            "A tumbler by the body smells of laudanum, and the victim's pupils are pinpricks. A dose to kill a horse.",
            "The laudanum bottle in the master bedroom's medicine chest is all but empty, though it was new on Sunday.", true),
        new Weapon("strychnine", "Strychnine", RoomIds.Pantry, false,
            "The victim's body is arched and rigid, the face fixed in a dreadful grin: strychnine, in the nightcap.",
            "The vermin killer in the butler's pantry has been tampered with: the packet is torn and re-folded.", true),
    };
}
