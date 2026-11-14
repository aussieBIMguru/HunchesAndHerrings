namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// The physical characteristics of a character that can rule them in or out
/// against evidence found at the scene.
/// </summary>
public sealed class Traits
{
    public SmokeKind Smoke { get; set; }
    public Handedness Hand { get; set; }
    public ShoeSize Shoe { get; set; }
    public Allergen Allergy { get; set; }
    public bool Frail { get; set; }

    /// <summary>Returns the canonical string value of a trait for comparison with facts.</summary>
    public string ValueOf(TraitCategory category) => category switch
    {
        TraitCategory.Smoking => Smoke.ToString(),
        TraitCategory.Handedness => Hand.ToString(),
        TraitCategory.ShoeSize => Shoe.ToString(),
        TraitCategory.Allergy => Allergy.ToString(),
        TraitCategory.Strength => Frail ? TraitRules.Frail : TraitRules.Sound,
        _ => string.Empty,
    };
}

/// <summary>Rules and labels for comparing a person's traits with scene evidence.</summary>
public static class TraitRules
{
    public const string Frail = "Frail";
    public const string Sound = "Sound";
    public const string Heavy = "Heavy";

    /// <summary>
    /// True when a person with <paramref name="personValue"/> could NOT have committed a murder
    /// whose scene shows <paramref name="sceneValue"/> for the given category.
    /// </summary>
    public static bool Excludes(TraitCategory category, string sceneValue, string personValue) => category switch
    {
        // The scene value names an allergen present; anyone allergic to it is ruled out.
        TraitCategory.Allergy => personValue == sceneValue,
        // A heavy blow rules out the frail or injured.
        TraitCategory.Strength => personValue == Frail,
        _ => personValue != sceneValue,
    };

    /// <summary>Short column heading for a category.</summary>
    public static string CategoryLabel(TraitCategory category) => category switch
    {
        TraitCategory.Smoking => "Tobacco",
        TraitCategory.Handedness => "Hand",
        TraitCategory.ShoeSize => "Boots",
        TraitCategory.Allergy => "Allergy",
        TraitCategory.Strength => "Strength",
        _ => category.ToString(),
    };

    /// <summary>Compact description of a person's trait value.</summary>
    public static string ValueLabel(TraitCategory category, string value) => category switch
    {
        TraitCategory.Smoking => value == nameof(SmokeKind.None) ? "Non-smoker" : value,
        TraitCategory.Handedness => $"{value}-handed",
        TraitCategory.ShoeSize => $"{value} boots",
        TraitCategory.Allergy => value == nameof(Allergen.None) ? "No allergies" : $"Allergic to {value.ToLowerInvariant()}",
        TraitCategory.Strength => value == Frail ? "Frail / injured" : "Able-bodied",
        _ => value,
    };

    /// <summary>Compact description of what the scene shows for a category.</summary>
    public static string SceneLabel(TraitCategory category, string value) => category switch
    {
        TraitCategory.Smoking => $"{value} smoker",
        TraitCategory.Handedness => $"{value}-handed blow",
        TraitCategory.ShoeSize => $"{value} boot-print",
        TraitCategory.Allergy => $"{value} present",
        TraitCategory.Strength => "Great strength",
        _ => value,
    };
}
