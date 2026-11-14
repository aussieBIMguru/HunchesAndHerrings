namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// The player: their name and how the household addresses them. Dialogue is written with placeholders
/// ({DET} = "Detective Grey", {SIR} = "sir"/"ma'am", {Sir} = "Sir"/"Ma'am") that <see cref="Apply"/> fills in.
/// </summary>
public sealed record Detective(string Name, Gender Gender)
{
    public const int MaxNameLength = 40;

    /// <summary>The family name, used in "Detective Grey".</summary>
    public string Surname => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? Name;

    /// <summary>How people address you formally: "Detective Grey".</summary>
    public string Title => $"Detective {Surname}";

    public string Sir => Gender == Gender.Male ? "sir" : "ma'am";

    public static Detective Default(Gender gender) => new(gender == Gender.Male ? "Arthur Grey" : "Ada Grey", gender);

    /// <summary>Builds a detective from what was typed, tidying it up; blank means the default name.</summary>
    public static Detective Create(string? name, Gender gender)
    {
        string filtered = new((name ?? string.Empty).Where(ch => !char.IsControl(ch) && ch is not '{' and not '}').ToArray());
        string clean = string.Join(' ', filtered.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length > MaxNameLength)
        {
            clean = clean[..MaxNameLength].Trim();
        }

        return clean.Length == 0 ? Default(gender) : new Detective(clean, gender);
    }

    /// <summary>Fills the address placeholders in a line of dialogue.</summary>
    public string Apply(string text) => text
        .Replace("{DET}", Title, StringComparison.Ordinal)
        .Replace("{SIR}", Sir, StringComparison.Ordinal)
        .Replace("{Sir}", char.ToUpperInvariant(Sir[0]) + Sir[1..], StringComparison.Ordinal)
        .Replace("{NAME}", Name, StringComparison.Ordinal);
}
