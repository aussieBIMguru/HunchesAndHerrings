namespace HunchesAndHerrings.Core.Model;

/// <summary>Well-known room identifiers.</summary>
public static class RoomIds
{
    // Cellar
    public const string WineCellar = "cellar";
    public const string Boiler = "boiler";
    public const string ServantsHall = "servantshall";
    public const string Kitchen = "kitchen";
    public const string Pantry = "pantry";
    public const string Scullery = "scullery";

    // Ground
    public const string Conservatory = "conservatory";
    public const string Library = "library";
    public const string Study = "study";
    public const string Billiard = "billiard";
    public const string Hall = "hall";
    public const string BackPassage = "backpassage";
    public const string Gardens = "gardens";
    public const string Drawing = "drawing";
    public const string Dining = "dining";

    // Upper
    public const string Master = "master";
    public const string Gallery = "gallery";
    public const string Blue = "blue";
    public const string Landing = "landing";
    public const string Rose = "rose";
    public const string Green = "green";
    public const string Quarters = "quarters";

    /// <summary>Pseudo-location for someone who has left the house for a while.</summary>
    public const string Away = "away";
}

/// <summary>Builds the fixed floor plan of the manor. Only the house's name varies between cases.</summary>
public static class ManorLayout
{
    public static Manor Create(string name)
    {
        var rooms = new List<Room>
        {
            // ---- Cellar ----
            new(RoomIds.WineCellar, "Wine Cellar", Floor.Cellar, new RoomRect(20, 20, 180, 160),
                "Racks of dusty bottles recede into the dark. The air is cold and smells of cork and damp stone."),
            new(RoomIds.Boiler, "Boiler Room", Floor.Cellar, new RoomRect(20, 200, 180, 180),
                "The great iron boiler ticks and groans. Coal dust coats every surface."),
            new(RoomIds.ServantsHall, "Servants' Hall", Floor.Cellar, new RoomRect(220, 20, 220, 170),
                "A long scrubbed table, a row of bells on the wall, and the smell of yesterday's mutton."),
            new(RoomIds.Kitchen, "Kitchen", Floor.Cellar, new RoomRect(220, 210, 220, 170),
                "Copper pans hang above a black range that never quite goes out."),
            new(RoomIds.Pantry, "Butler's Pantry", Floor.Cellar, new RoomRect(460, 20, 160, 170),
                "The butler's domain: locked plate cupboards, decanters and a ledger open on the desk."),
            new(RoomIds.Scullery, "Scullery", Floor.Cellar, new RoomRect(460, 210, 160, 170),
                "Stone sinks, a mangle, and a drain that gurgles quietly to itself."),

            // ---- Ground ----
            new(RoomIds.Conservatory, "Conservatory", Floor.Ground, new RoomRect(20, 20, 140, 170),
                "Glass and wrought iron, palms and ferns, and the steady drip of condensation."),
            new(RoomIds.Library, "Library", Floor.Ground, new RoomRect(180, 20, 140, 110),
                "Floor-to-ceiling shelves, a rolling ladder and two worn leather armchairs."),
            new(RoomIds.Study, "Study", Floor.Ground, new RoomRect(340, 20, 120, 110),
                "A heavy desk, a locked bureau, and the smell of ink and old cigar smoke."),
            new(RoomIds.Billiard, "Billiard Room", Floor.Ground, new RoomRect(480, 20, 140, 110),
                "Green baize under a low lamp. The cue rack stands by the window."),
            new(RoomIds.Hall, "Entrance Hall", Floor.Ground, new RoomRect(180, 150, 280, 110),
                "Black and white tiles, the grand staircase, and a tall clock that watches everyone."),
            new(RoomIds.BackPassage, "Back Passage", Floor.Ground, new RoomRect(480, 150, 140, 110),
                "A narrow servants' corridor, the back stairs winding up and down.", isPassage: true),
            new(RoomIds.Gardens, "Gardens", Floor.Ground, new RoomRect(20, 210, 140, 170),
                "Gravel paths, clipped yews and the dark line of the shrubbery.", isOutdoors: true),
            new(RoomIds.Drawing, "Drawing Room", Floor.Ground, new RoomRect(180, 280, 170, 100),
                "Chintz, a pianoforte, and a fire laid every evening whether wanted or not."),
            new(RoomIds.Dining, "Dining Room", Floor.Ground, new RoomRect(370, 280, 250, 100),
                "A long mahogany table set for twelve, silver gleaming on the sideboard."),

            // ---- Upper ----
            new(RoomIds.Master, "Master Bedroom", Floor.Upper, new RoomRect(20, 20, 200, 160),
                "A great four-poster bed and heavy curtains drawn against the light."),
            new(RoomIds.Gallery, "Long Gallery", Floor.Upper, new RoomRect(240, 20, 220, 110),
                "Ancestors in oils glare down the length of the gallery."),
            new(RoomIds.Blue, "Blue Room", Floor.Upper, new RoomRect(480, 20, 140, 160),
                "A guest room papered in blue birds and trailing vines."),
            new(RoomIds.Landing, "Landing", Floor.Upper, new RoomRect(240, 150, 220, 100),
                "The upper landing, where the grand stair and the corridors meet.", isPassage: true),
            new(RoomIds.Rose, "Rose Room", Floor.Upper, new RoomRect(20, 200, 200, 180),
                "A guest room in faded rose chintz, a writing desk by the window."),
            new(RoomIds.Green, "Green Room", Floor.Upper, new RoomRect(240, 270, 220, 110),
                "A guest room in sage green with a washstand and a narrow bed."),
            new(RoomIds.Quarters, "Servants' Quarters", Floor.Upper, new RoomRect(480, 200, 140, 180),
                "Iron bedsteads under the eaves, a shared washstand and a single candle."),
        };

        var connections = new List<Connection>
        {
            // Cellar
            new(RoomIds.ServantsHall, RoomIds.WineCellar, false),
            new(RoomIds.ServantsHall, RoomIds.Kitchen, false),
            new(RoomIds.ServantsHall, RoomIds.Pantry, false),
            new(RoomIds.Kitchen, RoomIds.Scullery, false),
            new(RoomIds.WineCellar, RoomIds.Boiler, false),
            new(RoomIds.Kitchen, RoomIds.Boiler, false),
            new(RoomIds.Pantry, RoomIds.Scullery, false),

            // Ground
            new(RoomIds.Hall, RoomIds.Library, false),
            new(RoomIds.Hall, RoomIds.Study, false),
            new(RoomIds.Hall, RoomIds.Drawing, false),
            new(RoomIds.Hall, RoomIds.Dining, false),
            new(RoomIds.Hall, RoomIds.Conservatory, false),
            new(RoomIds.Hall, RoomIds.BackPassage, false),
            new(RoomIds.Library, RoomIds.Study, false),
            new(RoomIds.Study, RoomIds.Billiard, false),
            new(RoomIds.Billiard, RoomIds.BackPassage, false),
            new(RoomIds.Dining, RoomIds.BackPassage, false),
            new(RoomIds.Drawing, RoomIds.Dining, false),
            new(RoomIds.Conservatory, RoomIds.Gardens, false),
            new(RoomIds.Gardens, RoomIds.Drawing, false),
            new(RoomIds.Conservatory, RoomIds.Library, false),

            // Upper
            new(RoomIds.Landing, RoomIds.Gallery, false),
            new(RoomIds.Landing, RoomIds.Master, false),
            new(RoomIds.Landing, RoomIds.Blue, false),
            new(RoomIds.Landing, RoomIds.Rose, false),
            new(RoomIds.Landing, RoomIds.Green, false),
            new(RoomIds.Landing, RoomIds.Quarters, false),
            new(RoomIds.Gallery, RoomIds.Master, false),
            new(RoomIds.Gallery, RoomIds.Blue, false),

            // Stairs
            new(RoomIds.Hall, RoomIds.Landing, true),
            new(RoomIds.BackPassage, RoomIds.ServantsHall, true),
            new(RoomIds.BackPassage, RoomIds.Quarters, true),
        };

        return new Manor(name, rooms, connections);
    }

    /// <summary>Rooms that several people might share of an evening (not bedrooms or passages).</summary>
    public static readonly IReadOnlyList<string> SharedRooms = new[]
    {
        RoomIds.Library, RoomIds.Billiard, RoomIds.Drawing, RoomIds.Conservatory, RoomIds.Gallery,
        RoomIds.Dining, RoomIds.WineCellar, RoomIds.ServantsHall, RoomIds.Kitchen, RoomIds.Scullery, RoomIds.Boiler,
    };

    /// <summary>Rooms whose disuse can be proven by physical evidence (cold grate, untouched lamps).</summary>
    public static readonly IReadOnlyList<string> ProvablyEmptyRooms = new[]
    {
        RoomIds.Library, RoomIds.Billiard, RoomIds.Drawing, RoomIds.Conservatory, RoomIds.Gallery,
        RoomIds.Dining, RoomIds.WineCellar, RoomIds.Boiler,
    };
}
