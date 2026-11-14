namespace HunchesAndHerrings.Core.Model;

/// <summary>A murder weapon, the room it was taken from, and how it reads as evidence.</summary>
public sealed record Weapon(string Id, string Name, string OriginRoomId, bool Heavy, string SceneText, string MissingText);

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
    };
}
