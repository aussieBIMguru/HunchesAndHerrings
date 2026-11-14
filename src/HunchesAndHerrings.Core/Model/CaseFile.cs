namespace HunchesAndHerrings.Core.Model;

/// <summary>Why an innocent suspect can be ruled out, and which clues prove it.</summary>
public sealed record Elimination(string Reason, IReadOnlyList<string> ClueIds);

/// <summary>Personality of the inspector's sergeant, which colours their banter.</summary>
public enum SergeantStyle
{
    Plodding,
    Eager,
    Gloomy,
}

/// <summary>
/// A complete generated mystery: the house, the cast, the hidden truth of what happened,
/// every clue that can be found, and how each innocent can be eliminated.
/// Regenerated deterministically from <see cref="Seed"/> and <see cref="Difficulty"/>.
/// </summary>
public sealed class CaseFile
{
    public required int Seed { get; init; }
    public required CaseOptions Options { get; init; }
    public required DifficultySettings Settings { get; init; }
    public Difficulty Difficulty => Options.Difficulty;
    public required Manor Manor { get; init; }
    public required string Title { get; init; }
    public required Character Victim { get; init; }
    public required IReadOnlyList<Character> Suspects { get; init; }
    public required string MurdererId { get; init; }
    public required string CrimeSceneId { get; init; }
    public required Weapon Weapon { get; init; }

    /// <summary>Night slot (0 = 9pm, 1 = 10pm, 2 = 11pm) when the murder happened.</summary>
    public required int MurderSlot { get; init; }

    public required IReadOnlyList<TraitCategory> SceneCategories { get; init; }
    public required IReadOnlyDictionary<TraitCategory, string> SceneValues { get; init; }

    /// <summary>True whereabouts last night: character id to room id per night slot (victim included).</summary>
    public required IReadOnlyDictionary<string, string[]> Night { get; init; }

    /// <summary>Today's schedule: character id to room id per hour from 8am (index 0) to 5pm (index 9).</summary>
    public required IReadOnlyDictionary<string, string[]> Day { get; init; }

    public required IReadOnlyList<Clue> Clues { get; init; }
    public required IReadOnlyList<string> KeyEvidenceIds { get; init; }
    public required IReadOnlyDictionary<string, Elimination> Eliminations { get; init; }

    /// <summary>Where the murderer falsely claimed to be at the time of death.</summary>
    public required string ClaimRoomId { get; init; }

    public required string Briefing { get; init; }
    public required string Truth { get; init; }
    public required string MotiveDetail { get; init; }
    public required string SergeantName { get; init; }
    public required SergeantStyle SergeantStyle { get; init; }

    public Character Murderer => Suspects.First(s => s.Id == MurdererId);

    public Room CrimeScene => Manor[CrimeSceneId];

    public Character? Person(string id) => id == Victim.Id ? Victim : Suspects.FirstOrDefault(s => s.Id == id);

    public string NameOf(string id) => Person(id)?.ShortName ?? id;
}
