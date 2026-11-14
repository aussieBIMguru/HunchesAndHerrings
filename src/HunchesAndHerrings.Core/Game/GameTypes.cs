using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Core.Game;

/// <summary>Category of a message reported back to the player.</summary>
public enum MessageKind
{
    Info,
    Clue,
    Sergeant,
    Hint,
    Contradiction,
    Warning,
    Time,
}

/// <summary>Something that happened as a result of an action.</summary>
public sealed record GameMessage(MessageKind Kind, string Text);

/// <summary>A message as recorded in the case log, stamped with the time it happened.</summary>
public sealed record LogEntry(int Minute, MessageKind Kind, string Text);

/// <summary>Where and when the player last laid eyes on someone.</summary>
public sealed record Sighting(string RoomId, int Minute);

/// <summary>What the sergeant has been sent to do.</summary>
public enum SergeantTaskKind
{
    Search,
    Interview,
}

/// <summary>An errand the sergeant is running, due back at <see cref="DueMinute"/>.</summary>
public sealed record SergeantTask(SergeantTaskKind Kind, string TargetId, int DueMinute);

/// <summary>Two discovered clues that cannot both be true.</summary>
public sealed record Contradiction(string Key, string ClueA, string ClueB, string Description);

/// <summary>The answer to an interview question.</summary>
/// <param name="Speech">What the person said, ready to display.</param>
/// <param name="Messages">Side effects (clues noted, contradictions, the clock).</param>
/// <param name="AlreadyAsked">True when the question was asked before and cost no time.</param>
public sealed record InterviewResult(string Speech, IReadOnlyList<GameMessage> Messages, bool AlreadyAsked);

/// <summary>
/// One entry in the inspector's journal, in the order it was written. <see cref="Key"/> is
/// "c:{clueId}" for a clue or "x:{contradictionKey}" for a contradiction.
/// </summary>
public sealed record JournalItem(string Key, int Minute)
{
    public const string CluePrefix = "c:";
    public const string ClashPrefix = "x:";

    public bool IsClue => Key.StartsWith(CluePrefix, StringComparison.Ordinal);

    public string RefId => Key[2..];
}

/// <summary>How the inspector knows where someone is.</summary>
public enum Certainty
{
    /// <summary>Seen with your own eyes.</summary>
    Seen,

    /// <summary>Told by a member of the household.</summary>
    Told,

    /// <summary>Only a rough idea, from the household's routine.</summary>
    Rough,
}

/// <summary>Where a person is (or is expected to be) at a given hour, and how sure we are.</summary>
/// <param name="Precise">For rough whereabouts: whether the room (not just the floor) may be shown.</param>
public sealed record Whereabout(Character Person, string RoomId, Certainty Certainty, bool Precise);

/// <summary>A room passed through (or stopped in) on the inspector's walk around the house.</summary>
public sealed record TrailStep(string RoomId, int Minute, bool IsStop);

/// <summary>A line in the conversation with the sergeant.</summary>
public sealed record ChatLine(bool FromPlayer, string Text, int Minute);

/// <summary>The outcome of an accusation.</summary>
public sealed record Verdict(
    bool CorrectMurderer,
    bool CorrectMotive,
    bool StrongEvidence,
    string Grade,
    string Headline,
    string AccusedId,
    MotiveKind Motive,
    string? EvidenceId);
