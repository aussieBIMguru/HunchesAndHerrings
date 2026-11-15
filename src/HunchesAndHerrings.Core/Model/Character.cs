namespace HunchesAndHerrings.Core.Model;

/// <summary>A person in the house: the victim or one of the suspects.</summary>
public sealed class Character
{
    public string Id { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;

    /// <summary>Honorific such as "Mrs", "Colonel" or "Lady"; empty for servants called by surname.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>How others refer to this person day to day, e.g. "Pike", "Lady Clara", "Colonel Fairfax".</summary>
    public string ShortName { get; init; } = string.Empty;

    public Gender Gender { get; init; }
    public RoleKind Role { get; init; }
    public Station Station { get; init; }

    /// <summary>Description of their place in the household, e.g. "the butler".</summary>
    public string RoleLabel { get; set; } = string.Empty;

    /// <summary>Bedroom or workroom where their belongings (and documents) are kept.</summary>
    public string PersonalRoomId { get; set; } = string.Empty;

    public Traits Traits { get; } = new();
    public MotiveKind Motive { get; set; }
    public bool IsVictim { get; init; }

    /// <summary>Doesn't live in the house: calls at certain hours, and can be sent for.</summary>
    public bool IsVisitor { get; init; }

    /// <summary>The rival detective: not a suspect.</summary>
    public bool IsRival => Role == RoleKind.RivalDetective;

    /// <summary>Painted portrait to show (see <see cref="PortraitCatalog"/>), or null to draw one in ink.</summary>
    public string? PortraitId { get; set; }

    /// <summary>A young child: can be questioned, never accused.</summary>
    public bool IsYoungChild => Role == RoleKind.YoungChild;

    /// <summary>Seed for the procedural portrait.</summary>
    public int PortraitSeed { get; init; }

    public string FullName => Title.Length > 0 ? $"{Title} {FirstName} {Surname}" : $"{FirstName} {Surname}";

    public string Initials => $"{FirstName[..1]}{Surname[..1]}";

    public bool IsMale => Gender == Gender.Male;

    public string He => IsMale ? "he" : "she";
    public string HeCap => IsMale ? "He" : "She";
    public string Him => IsMale ? "him" : "her";
    public string His => IsMale ? "his" : "her";
    public string HisCap => IsMale ? "His" : "Her";

    public override string ToString() => ShortName;
}
