namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// A discoverable piece of evidence: something found in a room, or something a person says
/// when asked about a topic. Clues are generated up front with the case; the session tracks
/// which ones the player has found.
/// </summary>
public sealed class Clue
{
    public string Id { get; set; } = string.Empty;
    public ClueKind Kind { get; set; }

    /// <summary>Short notebook heading.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Notebook summary.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>What the speaker actually says, for statements.</summary>
    public string? Spoken { get; set; }

    public List<Fact> Facts { get; set; } = new();

    /// <summary>Found by searching this room (null for statements).</summary>
    public string? RoomId { get; set; }

    /// <summary>Character who gives this statement (null for physical evidence).</summary>
    public string? SpeakerId { get; set; }

    /// <summary>Interview topic that draws out this statement.</summary>
    public Topic? Topic { get; set; }

    /// <summary>For <see cref="Model.Topic.AboutPerson"/>: who the statement is about.</summary>
    public string? TargetId { get; set; }

    /// <summary>False when the clue contains a lie or a misreport. Hidden from the player.</summary>
    public bool IsTruthful { get; set; } = true;

    /// <summary>True when the clue matters to solving the case (used by hints and the results screen).</summary>
    public bool IsKey { get; set; }

    /// <summary>Characters this clue places away from the scene at the time of death.</summary>
    public List<string> AlibiFor { get; set; } = new();

    public bool IsStatement => SpeakerId is not null;
}
