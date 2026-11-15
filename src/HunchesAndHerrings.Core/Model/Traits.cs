namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// A person's value for every <see cref="TraitCategory"/> (see <see cref="TraitCatalog"/>). Scene evidence
/// tests people against these.
/// </summary>
public sealed class Traits
{
    private readonly Dictionary<TraitCategory, string> _values = new();

    /// <summary>Returns the person's value for a category (the catalogue's first value if never set).</summary>
    public string ValueOf(TraitCategory category) =>
        _values.TryGetValue(category, out string? value) ? value : TraitCatalog.Get(category).Values[0].Id;

    public void Set(TraitCategory category, string value) => _values[category] = value;

    /// <summary>Frail or injured: can't strike a heavy blow.</summary>
    public bool Frail => ValueOf(TraitCategory.Strength) == TraitRules.Frail;

    public bool Wears(TraitCategory category, string value) => ValueOf(category) == value;
}

/// <summary>Rules and labels for comparing a person's traits with scene evidence.</summary>
public static class TraitRules
{
    public const string Frail = "Frail";
    public const string Sound = "Sound";

    /// <summary>
    /// True when a person with <paramref name="personValue"/> could NOT have left a scene that shows
    /// <paramref name="sceneValue"/> for the given category.
    /// </summary>
    public static bool Excludes(TraitCategory category, string sceneValue, string personValue) =>
        TraitCatalog.Get(category).Rule == TraitRule.Avoid
            ? personValue == sceneValue
            : personValue != sceneValue;

    /// <summary>Short column heading for a category.</summary>
    public static string CategoryLabel(TraitCategory category) => TraitCatalog.Get(category).Label;

    /// <summary>Description of a person's value, e.g. "Smokes a pipe".</summary>
    public static string ValueLabel(TraitCategory category, string value) =>
        Capitalise(TraitCatalog.Get(category).Value(value).Desc);

    /// <summary>A very short form for the suspect matrix, e.g. "pipe".</summary>
    public static string ShortLabel(TraitCategory category, string value) => TraitCatalog.Get(category).Value(value).Short;

    /// <summary>What the scene shows for a category, e.g. "Pipe smoker".</summary>
    public static string SceneLabel(TraitCategory category, string value) =>
        Capitalise(TraitCatalog.Get(category).Value(value).SceneTag ?? value);

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
