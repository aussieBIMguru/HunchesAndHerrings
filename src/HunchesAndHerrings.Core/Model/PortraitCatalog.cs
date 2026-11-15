using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Model;

/// <summary>How old someone in a portrait looks.</summary>
public enum Age
{
    Child,
    Young,
    Middle,
    Old,
}

/// <summary>
/// A painted portrait (Assets/Portraits/{Id}.jpg) and what it shows. A character given a portrait has their hair
/// colour and spectacles set to match it, so the face on the card is honest evidence.
/// </summary>
/// <param name="Label">Overrides the role description, e.g. "the curate" instead of "the vicar".</param>
/// <param name="FirstNames">Given names that suit the sitter, instead of the general name bank.</param>
/// <param name="Surnames">Surnames that suit the sitter (not used for family members, who share the family name).</param>
public sealed record Portrait(
    string Id,
    Gender Gender,
    Age Age,
    string Hair,
    bool Spectacles,
    RoleKind[] Roles,
    string? Label = null,
    string[]? FirstNames = null,
    string[]? Surnames = null);

/// <summary>Every painted portrait, and which roles it suits. Roles without a match fall back to an ink drawing.</summary>
public static class PortraitCatalog
{
    private const RoleKind Victim = RoleKind.Master;

    public static IReadOnlyList<Portrait> All { get; } = new[]
    {
        // Children, police and detectives
        new Portrait("kids_0", Gender.Male, Age.Child, "Dark", false, new[] { RoleKind.YoungChild }),
        new Portrait("kids_1", Gender.Female, Age.Child, "Fair", false, new[] { RoleKind.YoungChild }),
        new Portrait("kids_2", Gender.Male, Age.Child, "Dark", false, new[] { RoleKind.YoungChild }),
        new Portrait("kids_3", Gender.Female, Age.Child, "Fair", false, new[] { RoleKind.YoungChild }),
        new Portrait("kids_4", Gender.Male, Age.Child, "Fair", false, new[] { RoleKind.YoungChild }),
        new Portrait("kids_5", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Constable }),
        new Portrait("kids_6", Gender.Female, Age.Young, "Auburn", false, new[] { RoleKind.RivalDetective }),
        new Portrait("kids_7", Gender.Male, Age.Old, "Grey", false, new[] { RoleKind.RivalDetective }),
        new Portrait("kids_8", Gender.Female, Age.Young, "Dark", false, new[] { RoleKind.Constable }),
        new Portrait("kids_9", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.RivalDetective }),

        // Below stairs
        new Portrait("staff_0", Gender.Male, Age.Old, "Grey", false, new[] { RoleKind.Butler }),
        new Portrait("staff_1", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Gardener }),
        new Portrait("staff_2", Gender.Male, Age.Young, "Dark", false, new[] { RoleKind.Valet }),
        new Portrait("staff_3", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Gardener, RoleKind.Valet }),
        new Portrait("staff_4", Gender.Male, Age.Old, "Grey", false, new[] { RoleKind.Butler, RoleKind.Valet }),
        new Portrait("staff_5", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.Housekeeper }),
        new Portrait("staff_6", Gender.Female, Age.Middle, "Grey", false, new[] { RoleKind.Cook, RoleKind.Housekeeper }),
        new Portrait("staff_7", Gender.Female, Age.Young, "Dark", false, new[] { RoleKind.Maid }),
        new Portrait("staff_8", Gender.Female, Age.Middle, "Dark", false, new[] { RoleKind.Governess, RoleKind.Housekeeper, RoleKind.Companion }),
        new Portrait("staff_9", Gender.Female, Age.Young, "Fair", false, new[] { RoleKind.Maid, RoleKind.Cook }),

        // Gentry
        new Portrait("gentry_0", Gender.Male, Age.Young, "Dark", false, new[] { RoleKind.Heir, RoleKind.Child, RoleKind.Ward }),
        new Portrait("gentry_1", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.BusinessPartner, RoleKind.Solicitor, RoleKind.Spouse, Victim }),
        new Portrait("gentry_2", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Colonel }),
        new Portrait("gentry_3", Gender.Male, Age.Young, "Black", true, new[] { RoleKind.Heir, RoleKind.Child, RoleKind.Ward, RoleKind.Doctor }),
        new Portrait("gentry_4", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.BusinessPartner, RoleKind.Spouse, Victim }),
        new Portrait("gentry_5", Gender.Female, Age.Young, "Fair", false, new[] { RoleKind.Child, RoleKind.Ward, RoleKind.Heir, RoleKind.Companion }),
        new Portrait("gentry_6", Gender.Female, Age.Middle, "Dark", false, new[] { RoleKind.Spouse, RoleKind.BusinessPartner, Victim }),
        new Portrait("gentry_7", Gender.Female, Age.Young, "Black", false, new[] { RoleKind.Heir, RoleKind.Child, RoleKind.Ward, RoleKind.Companion }),
        new Portrait("gentry_8", Gender.Female, Age.Middle, "Auburn", false, new[] { RoleKind.Spouse, RoleKind.BusinessPartner, Victim }),
        new Portrait("gentry_9", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.Spouse, RoleKind.Companion, Victim }),

        // Elders and professionals
        new Portrait("elders_0", Gender.Male, Age.Old, "Grey", true, new[] { RoleKind.Colonel, RoleKind.Solicitor, Victim }),
        new Portrait("elders_1", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Doctor, RoleKind.Solicitor, RoleKind.BusinessPartner, RoleKind.Spouse }),
        new Portrait("elders_2", Gender.Male, Age.Young, "Dark", false, new[] { RoleKind.Heir, RoleKind.Child, RoleKind.Ward }),
        new Portrait("elders_3", Gender.Male, Age.Middle, "Fair", false, new[] { RoleKind.BusinessPartner, RoleKind.Colonel, Victim }),
        new Portrait("elders_4", Gender.Male, Age.Middle, "Black", false, new[] { RoleKind.Doctor, RoleKind.Solicitor, RoleKind.Butler, RoleKind.Valet }),
        new Portrait("elders_5", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.Spouse, RoleKind.Housekeeper, Victim }),
        new Portrait("elders_6", Gender.Female, Age.Middle, "Auburn", false, new[] { RoleKind.Spouse, RoleKind.BusinessPartner, RoleKind.Companion }),
        new Portrait("elders_7", Gender.Female, Age.Young, "Dark", false, new[] { RoleKind.Child, RoleKind.Ward, RoleKind.Companion, RoleKind.Heir }),
        new Portrait("elders_8", Gender.Female, Age.Old, "Grey", true, new[] { RoleKind.Governess, RoleKind.Housekeeper, RoleKind.Companion, RoleKind.Doctor }),
        new Portrait("elders_9", Gender.Female, Age.Young, "Dark", false, new[] { RoleKind.Governess, RoleKind.Companion, RoleKind.Spouse }),

        // Filling the gaps
        new Portrait("extra_0", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Reverend }),
        new Portrait("extra_1", Gender.Male, Age.Middle, "Dark", false, new[] { RoleKind.Cook }),
        new Portrait("extra_2", Gender.Female, Age.Middle, "Dark", false, new[] { RoleKind.Gardener }),
        new Portrait("extra_3", Gender.Female, Age.Middle, "Dark", false, new[] { RoleKind.Doctor }),
        new Portrait("extra_4", Gender.Male, Age.Young, "Dark", true, new[] { RoleKind.Governess, RoleKind.Ward, RoleKind.Heir }),
        new Portrait("extra_5", Gender.Female, Age.Young, "Dark", false, new[] { RoleKind.Maid }),
        new Portrait("extra_6", Gender.Male, Age.Young, "Dark", false, new[] { RoleKind.Valet }),
        new Portrait("extra_7", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.Housekeeper }),
        new Portrait("extra_8", Gender.Female, Age.Young, "Dark", false, new[] { RoleKind.Maid, RoleKind.Cook }),
        new Portrait("extra_9", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.RivalDetective }),

        // Older servants, companions and professionals
        new Portrait("more_0", Gender.Male, Age.Old, "Grey", false, new[] { RoleKind.Butler }),
        new Portrait("more_1", Gender.Male, Age.Old, "Grey", false, new[] { RoleKind.Gardener }),
        new Portrait("more_2", Gender.Male, Age.Middle, "Grey", false, new[] { RoleKind.Valet, RoleKind.Butler }),
        new Portrait("more_3", Gender.Female, Age.Middle, "Dark", true, new[] { RoleKind.Companion, RoleKind.Governess }),
        new Portrait("more_4", Gender.Female, Age.Young, "Fair", false, new[] { RoleKind.Companion, RoleKind.Ward }),
        new Portrait("more_5", Gender.Male, Age.Old, "Grey", false, new[] { RoleKind.Spouse, Victim, RoleKind.Solicitor }),
        new Portrait("more_6", Gender.Female, Age.Middle, "Auburn", false, new[] { RoleKind.BusinessPartner, RoleKind.Spouse }),
        new Portrait("more_7", Gender.Male, Age.Middle, "Dark", true, new[] { RoleKind.Solicitor, RoleKind.Doctor }),
        new Portrait("more_8", Gender.Male, Age.Old, "Grey", true, new[] { RoleKind.Colonel }),
        new Portrait("more_9", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.Cook, RoleKind.Housekeeper }),

        // The young, the family and the law
        new Portrait("young_0", Gender.Male, Age.Child, "Fair", false, new[] { RoleKind.YoungChild }),
        new Portrait("young_1", Gender.Female, Age.Child, "Black", false, new[] { RoleKind.YoungChild }),
        new Portrait("young_2", Gender.Female, Age.Child, "Auburn", false, new[] { RoleKind.YoungChild }),
        new Portrait("young_3", Gender.Male, Age.Child, "Auburn", false, new[] { RoleKind.YoungChild }),
        new Portrait("young_4", Gender.Male, Age.Middle, "Fair", false, new[] { RoleKind.Child, RoleKind.Heir }),
        new Portrait("young_5", Gender.Female, Age.Young, "Auburn", false, new[] { RoleKind.Child, RoleKind.Heir, RoleKind.Ward }),
        new Portrait("young_6", Gender.Female, Age.Middle, "Black", false, new[] { RoleKind.Spouse, Victim }),
        new Portrait("young_7", Gender.Female, Age.Young, "Black", false, new[] { RoleKind.Maid }),
        new Portrait("young_8", Gender.Male, Age.Middle, "Auburn", false, new[] { RoleKind.RivalDetective }),
        new Portrait("young_9", Gender.Male, Age.Middle, "Grey", false, new[] { RoleKind.Constable }),

        // The final set: clergy, tutors, doctors and staff
        new Portrait("last_0", Gender.Male, Age.Old, "Grey", true, new[] { RoleKind.Reverend }),
        new Portrait("last_1", Gender.Male, Age.Young, "Fair", false, new[] { RoleKind.Reverend }, Label: "the curate"),
        new Portrait("last_2", Gender.Male, Age.Young, "Auburn", false, new[] { RoleKind.Governess, RoleKind.Ward, RoleKind.Child }),
        new Portrait("last_3", Gender.Female, Age.Old, "Grey", false, new[] { RoleKind.Gardener }),
        new Portrait("last_4", Gender.Male, Age.Young, "Auburn", false, new[] { RoleKind.Cook }),
        new Portrait("last_5", Gender.Female, Age.Old, "Grey", true, new[] { RoleKind.Doctor }),
        new Portrait("last_6", Gender.Male, Age.Middle, "Black", false, new[] { RoleKind.Doctor },
            FirstNames: new[] { "Rajendra", "Sarat", "Jagadish", "Prafulla", "Dwarkanath", "Kishori" },
            Surnames: new[] { "Banerjee", "Chatterjee", "Mukherjee", "Bose", "Ghosh", "Naoroji" }),
        new Portrait("last_7", Gender.Female, Age.Young, "Fair", false, new[] { RoleKind.Child, RoleKind.Heir, RoleKind.Ward }),
        new Portrait("last_8", Gender.Male, Age.Young, "Fair", false, new[] { RoleKind.Valet }),
        new Portrait("last_9", Gender.Male, Age.Middle, "Black", true, new[] { RoleKind.Butler }),
    };

    public static Portrait? Get(string? id) => id is null ? null : All.FirstOrDefault(p => p.Id == id);

    /// <summary>Picks (and reserves) an unused portrait that suits a role and sex, or null if none fits.</summary>
    public static Portrait? Pick(Gender gender, RoleKind role, Random rng, HashSet<string> used)
    {
        var fits = All.Where(p => p.Gender == gender && p.Roles.Contains(role) && !used.Contains(p.Id)).ToList();
        if (fits.Count == 0)
        {
            return null;
        }

        Portrait chosen = rng.Pick(fits);
        used.Add(chosen.Id);
        return chosen;
    }

    /// <summary>Makes a character's hair and spectacles match their portrait, if they have one.</summary>
    public static void ApplyLooks(Character c)
    {
        if (Get(c.PortraitId) is { } p)
        {
            c.Traits.Set(TraitCategory.HairColour, p.Hair);
            c.Traits.Set(TraitCategory.Spectacles, p.Spectacles ? "Spectacles" : "None");
        }
    }

    /// <summary>
    /// Gives a character an unused portrait that suits their role and sex, and makes their hair and spectacles match
    /// it. Leaves them without one (to be drawn in ink) if nothing fits.
    /// </summary>
    public static void Assign(Character c, Random rng, HashSet<string> used)
    {
        if (Pick(c.Gender, c.Role, rng, used) is not { } chosen)
        {
            return;
        }

        c.PortraitId = chosen.Id;
        ApplyLooks(c);
    }
}
