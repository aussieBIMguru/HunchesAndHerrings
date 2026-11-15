using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Core.Generation;

/// <summary>Static description of a household role: who fills it and where they spend their time.</summary>
/// <param name="Kind">The role.</param>
/// <param name="Station">Servant, family or guest.</param>
/// <param name="MaleChance">Probability the role is filled by a man.</param>
/// <param name="DayRooms">Rooms they frequent during the day.</param>
/// <param name="NightRooms">Rooms they might plausibly be in of an evening.</param>
/// <param name="PersonalRoomId">Where their belongings are kept; empty means "assign a guest bedroom".</param>
/// <param name="FrailChance">Probability of being frail or injured.</param>
/// <param name="Motives">Motives that suit the role.</param>
public sealed record RoleDef(
    RoleKind Kind,
    Station Station,
    double MaleChance,
    string[] DayRooms,
    string[] NightRooms,
    string PersonalRoomId,
    double FrailChance,
    MotiveKind[] Motives);

/// <summary>All roles a suspect can take.</summary>
public static class RoleCatalog
{
    public static IReadOnlyList<RoleDef> Servants { get; } = new[]
    {
        new RoleDef(RoleKind.Butler, Station.Servant, 1.0,
            new[] { RoomIds.Pantry, RoomIds.Dining, RoomIds.Hall, RoomIds.ServantsHall, RoomIds.WineCellar },
            new[] { RoomIds.Pantry, RoomIds.ServantsHall, RoomIds.WineCellar, RoomIds.Dining },
            RoomIds.Pantry, 0.15, new[] { MotiveKind.Dismissal, MotiveKind.Blackmail, MotiveKind.Revenge, MotiveKind.Inheritance }),
        new RoleDef(RoleKind.Housekeeper, Station.Servant, 0.0,
            new[] { RoomIds.ServantsHall, RoomIds.Kitchen, RoomIds.Master, RoomIds.Blue, RoomIds.Rose, RoomIds.Green, RoomIds.Gallery, RoomIds.Landing },
            new[] { RoomIds.ServantsHall, RoomIds.Kitchen, RoomIds.Quarters, RoomIds.Scullery },
            RoomIds.ServantsHall, 0.25, new[] { MotiveKind.Dismissal, MotiveKind.Inheritance, MotiveKind.Revenge, MotiveKind.Blackmail }),
        new RoleDef(RoleKind.Cook, Station.Servant, 0.3,
            new[] { RoomIds.Kitchen, RoomIds.Kitchen, RoomIds.Scullery, RoomIds.ServantsHall, RoomIds.WineCellar },
            new[] { RoomIds.Kitchen, RoomIds.ServantsHall, RoomIds.Scullery },
            RoomIds.Kitchen, 0.2, new[] { MotiveKind.Dismissal, MotiveKind.Debt, MotiveKind.Revenge }),
        new RoleDef(RoleKind.Gardener, Station.Servant, 0.75,
            new[] { RoomIds.Gardens, RoomIds.Gardens, RoomIds.Conservatory, RoomIds.Boiler },
            new[] { RoomIds.ServantsHall, RoomIds.Boiler, RoomIds.Conservatory, RoomIds.Kitchen },
            RoomIds.Conservatory, 0.25, new[] { MotiveKind.Dismissal, MotiveKind.Revenge, MotiveKind.Inheritance }),
        new RoleDef(RoleKind.Maid, Station.Servant, 0.0,
            new[] { RoomIds.Scullery, RoomIds.Drawing, RoomIds.Dining, RoomIds.Master, RoomIds.Blue, RoomIds.Rose, RoomIds.Green, RoomIds.Quarters, RoomIds.Hall },
            new[] { RoomIds.Quarters, RoomIds.Scullery, RoomIds.Kitchen, RoomIds.ServantsHall },
            RoomIds.Quarters, 0.05, new[] { MotiveKind.Dismissal, MotiveKind.Jealousy, MotiveKind.Blackmail }),
        new RoleDef(RoleKind.Valet, Station.Servant, 1.0,
            new[] { RoomIds.Quarters, RoomIds.Master, RoomIds.Pantry, RoomIds.Boiler, RoomIds.Billiard },
            new[] { RoomIds.Quarters, RoomIds.ServantsHall, RoomIds.Boiler, RoomIds.WineCellar },
            RoomIds.Quarters, 0.05, new[] { MotiveKind.Dismissal, MotiveKind.Blackmail, MotiveKind.Debt }),
        new RoleDef(RoleKind.Governess, Station.Servant, 0.3,
            new[] { RoomIds.Library, RoomIds.Gallery, RoomIds.Gardens, RoomIds.Quarters, RoomIds.Drawing },
            new[] { RoomIds.Quarters, RoomIds.Library, RoomIds.Gallery },
            RoomIds.Quarters, 0.1, new[] { MotiveKind.Jealousy, MotiveKind.Dismissal, MotiveKind.Blackmail, MotiveKind.Revenge }),
    };

    public static IReadOnlyList<RoleDef> Gentry { get; } = new[]
    {
        new RoleDef(RoleKind.Spouse, Station.Family, 0.5,
            new[] { RoomIds.Master, RoomIds.Drawing, RoomIds.Conservatory, RoomIds.Gallery, RoomIds.Gardens },
            new[] { RoomIds.Drawing, RoomIds.Conservatory, RoomIds.Gallery, RoomIds.Library },
            RoomIds.Master, 0.15, new[] { MotiveKind.Inheritance, MotiveKind.Jealousy, MotiveKind.Revenge }),
        new RoleDef(RoleKind.Heir, Station.Family, 0.6,
            new[] { RoomIds.Billiard, RoomIds.Library, RoomIds.Gardens, RoomIds.Gallery },
            new[] { RoomIds.Billiard, RoomIds.Library, RoomIds.Drawing },
            string.Empty, 0.05, new[] { MotiveKind.Inheritance, MotiveKind.Debt, MotiveKind.Disinheritance }),
        new RoleDef(RoleKind.Ward, Station.Family, 0.4,
            new[] { RoomIds.Gallery, RoomIds.Drawing, RoomIds.Gardens, RoomIds.Conservatory },
            new[] { RoomIds.Drawing, RoomIds.Gallery, RoomIds.Library },
            string.Empty, 0.05, new[] { MotiveKind.Disinheritance, MotiveKind.Jealousy, MotiveKind.Inheritance }),
        new RoleDef(RoleKind.Colonel, Station.Guest, 1.0,
            new[] { RoomIds.Billiard, RoomIds.Library, RoomIds.Gardens, RoomIds.Drawing },
            new[] { RoomIds.Billiard, RoomIds.Library, RoomIds.Drawing },
            string.Empty, 0.3, new[] { MotiveKind.Debt, MotiveKind.Revenge, MotiveKind.Blackmail }),
        new RoleDef(RoleKind.Doctor, Station.Guest, 0.7,
            new[] { RoomIds.Library, RoomIds.Drawing, RoomIds.Conservatory, RoomIds.Gallery },
            new[] { RoomIds.Library, RoomIds.Drawing, RoomIds.Billiard },
            string.Empty, 0.1, new[] { MotiveKind.Debt, MotiveKind.Blackmail, MotiveKind.Jealousy }),
        new RoleDef(RoleKind.Reverend, Station.Guest, 1.0,
            new[] { RoomIds.Gallery, RoomIds.Conservatory, RoomIds.Library, RoomIds.Gardens },
            new[] { RoomIds.Library, RoomIds.Drawing, RoomIds.Gallery },
            string.Empty, 0.35, new[] { MotiveKind.Revenge, MotiveKind.Blackmail, MotiveKind.Debt }),
        new RoleDef(RoleKind.Solicitor, Station.Guest, 1.0,
            new[] { RoomIds.Study, RoomIds.Library, RoomIds.Dining, RoomIds.Drawing },
            new[] { RoomIds.Library, RoomIds.Billiard, RoomIds.Drawing },
            string.Empty, 0.15, new[] { MotiveKind.Debt, MotiveKind.Blackmail, MotiveKind.Disinheritance }),
        new RoleDef(RoleKind.BusinessPartner, Station.Guest, 0.7,
            new[] { RoomIds.Billiard, RoomIds.Study, RoomIds.Drawing, RoomIds.Hall, RoomIds.Library },
            new[] { RoomIds.Billiard, RoomIds.Drawing, RoomIds.Library },
            string.Empty, 0.1, new[] { MotiveKind.Debt, MotiveKind.Revenge, MotiveKind.Inheritance }),
        new RoleDef(RoleKind.Companion, Station.Guest, 0.0,
            new[] { RoomIds.Drawing, RoomIds.Gallery, RoomIds.Conservatory, RoomIds.Gardens },
            new[] { RoomIds.Drawing, RoomIds.Gallery, RoomIds.Library },
            string.Empty, 0.1, new[] { MotiveKind.Jealousy, MotiveKind.Inheritance, MotiveKind.Blackmail }),
        new RoleDef(RoleKind.Child, Station.Family, 0.55,
            new[] { RoomIds.Billiard, RoomIds.Library, RoomIds.Gardens, RoomIds.Drawing, RoomIds.Gallery },
            new[] { RoomIds.Billiard, RoomIds.Library, RoomIds.Drawing, RoomIds.Gallery },
            string.Empty, 0.03, new[] { MotiveKind.Inheritance, MotiveKind.Debt, MotiveKind.Disinheritance, MotiveKind.Insurance }),
    };

    /// <summary>People who don't live at the house but call at it: they can be sent for.</summary>
    public static IReadOnlyList<RoleDef> Visitors { get; } = new[]
    {
        new RoleDef(RoleKind.Doctor, Station.Guest, 0.7,
            new[] { RoomIds.Master, RoomIds.Drawing, RoomIds.Hall },
            new[] { RoomIds.Master, RoomIds.Drawing, RoomIds.Library },
            RoomIds.Hall, 0.1, new[] { MotiveKind.Debt, MotiveKind.Blackmail, MotiveKind.Insurance }),
        new RoleDef(RoleKind.Constable, Station.Guest, 0.8,
            new[] { RoomIds.Hall, RoomIds.ServantsHall, RoomIds.Kitchen },
            new[] { RoomIds.ServantsHall, RoomIds.Kitchen, RoomIds.Hall },
            RoomIds.Hall, 0.05, new[] { MotiveKind.Revenge }),
        new RoleDef(RoleKind.Reverend, Station.Guest, 1.0,
            new[] { RoomIds.Drawing, RoomIds.Library, RoomIds.Hall },
            new[] { RoomIds.Drawing, RoomIds.Library },
            RoomIds.Hall, 0.3, new[] { MotiveKind.Revenge, MotiveKind.Blackmail }),
        new RoleDef(RoleKind.Solicitor, Station.Guest, 1.0,
            new[] { RoomIds.Study, RoomIds.Library, RoomIds.Hall },
            new[] { RoomIds.Study, RoomIds.Library },
            RoomIds.Hall, 0.15, new[] { MotiveKind.Debt, MotiveKind.Disinheritance }),
    };

    public static RoleDef Get(RoleKind kind) =>
        Servants.Concat(Gentry).First(r => r.Kind == kind);

    public static RoleDef Visitor(RoleKind kind) => Visitors.First(r => r.Kind == kind);

    /// <summary>Human-readable motive name for menus.</summary>
    public static string MotiveName(MotiveKind motive) => motive switch
    {
        MotiveKind.Inheritance => "Inheritance",
        MotiveKind.Debt => "Debt owed to the victim",
        MotiveKind.Dismissal => "Facing dismissal",
        MotiveKind.Jealousy => "Jealousy / forbidden love",
        MotiveKind.Blackmail => "Being blackmailed",
        MotiveKind.Revenge => "Revenge for an old wrong",
        MotiveKind.Disinheritance => "About to be disinherited",
        MotiveKind.Affair => "An affair",
        MotiveKind.Silence => "To keep a secret buried",
        MotiveKind.Recognition => "A secret parentage",
        MotiveKind.Negligence => "Hiding a fatal blunder",
        MotiveKind.Imposture => "Exposure as an impostor",
        MotiveKind.Insurance => "A life insurance policy",
        MotiveKind.Theft => "Caught stealing",
        MotiveKind.Accident => "None: it was an accident",
        _ => "No motive",
    };
}
