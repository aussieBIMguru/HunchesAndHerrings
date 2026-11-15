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

/// <summary>Personality of the rival detective.</summary>
public enum RivalStyle
{
    /// <summary>Grand, theatrical, certain of everything.</summary>
    Pompous,

    /// <summary>Quiet, watchful, and not above pocketing the evidence.</summary>
    Sly,

    /// <summary>A newspaperman's bluster and a newspaperman's nose.</summary>
    Brash,
}

/// <summary>Why the house is shut up today, and whether anyone from outside can come and go.</summary>
/// <param name="Id">Short identifier.</param>
/// <param name="Text">Sentence for the briefing.</param>
/// <param name="AllowsVisitors">Callers from outside (doctor, constable, vicar) can reach the house today.</param>
/// <param name="AllowsErrands">The household may go out into the village this afternoon.</param>
public sealed record Lockdown(string Id, string Text, bool AllowsVisitors, bool AllowsErrands);

/// <summary>A clue the rival detective pockets before you get to it, and the hour (index) they take it.</summary>
public sealed record RivalPocket(string ClueId, int HourIndex);

/// <summary>Everything about the rival detective: who they are, where they go, and what they know.</summary>
public sealed class RivalPlan
{
    public required Character Detective { get; init; }
    public required RivalStyle Style { get; init; }

    /// <summary>Who engaged them, e.g. "the insurers".</summary>
    public required string EngagedBy { get; init; }

    /// <summary>Hour index to the suspect they have closeted for questioning (unavailable to you).</summary>
    public required IReadOnlyDictionary<int, string> Closeted { get; init; }

    public required IReadOnlyList<RivalPocket> Pockets { get; init; }

    /// <summary>Clue ids of the tips they'll share, in order. Some may be false on harder ranks.</summary>
    public required IReadOnlyList<string> TipIds { get; init; }

    /// <summary>Whom they'll name at the end (empty with <see cref="AccusesAccident"/> for "no murder").</summary>
    public required IReadOnlyList<string> AccusesIds { get; init; }

    public bool AccusesAccident { get; init; }

    /// <summary>Chance (0..1) their theories point at the truth.</summary>
    public required double Insight { get; init; }

    /// <summary>Whether they hand back a pocketed clue when asked (otherwise they make you wait for it).</summary>
    public required bool Generous { get; init; }
}

/// <summary>
/// A complete generated mystery: the house, the cast, the hidden truth of what happened,
/// every clue that can be found, and how each innocent can be eliminated.
/// Regenerated deterministically from <see cref="Seed"/> and <see cref="Options"/>.
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

    /// <summary>The story behind the case.</summary>
    public required CaseModel Model { get; init; }

    /// <summary>Everyone the player must name (empty when the death was an accident).</summary>
    public required IReadOnlyList<string> CulpritIds { get; init; }

    /// <summary>The culprit who struck the blow (or gave the dose); empty when there was no murder.</summary>
    public required string MurdererId { get; init; }

    /// <summary>The motive the player must name.</summary>
    public required MotiveKind CaseMotive { get; init; }

    /// <summary>The person the killer actually meant to kill, when it wasn't the victim.</summary>
    public string? IntendedVictimId { get; init; }

    public bool IsAccident => Model == CaseModel.Accident;

    public required string CrimeSceneId { get; init; }
    public required Weapon Weapon { get; init; }

    /// <summary>Night slot (0 = 9pm, 1 = 10pm, 2 = 11pm) when the victim died.</summary>
    public required int MurderSlot { get; init; }

    /// <summary>Night slot when whoever left the scene evidence was in the room (usually the time of death).</summary>
    public required int CrimeSlot { get; init; }

    public required IReadOnlyList<TraitCategory> SceneCategories { get; init; }
    public required IReadOnlyDictionary<TraitCategory, string> SceneValues { get; init; }

    /// <summary>True whereabouts last night: character id to room id per night slot (victim included).</summary>
    public required IReadOnlyDictionary<string, string[]> Night { get; init; }

    /// <summary>Today's schedule: character id to room id per hour from 8am (index 0) to 5pm (index 9).</summary>
    public required IReadOnlyDictionary<string, string[]> Day { get; init; }

    public required IReadOnlyList<Clue> Clues { get; init; }
    public required IReadOnlyList<string> KeyEvidenceIds { get; init; }
    public required IReadOnlyDictionary<string, Elimination> Eliminations { get; init; }

    /// <summary>Where the culprits falsely claimed to be at the crime slot.</summary>
    public required string ClaimRoomId { get; init; }

    public required Lockdown Lockdown { get; init; }
    public required string Briefing { get; init; }
    public required string Truth { get; init; }
    public required string MotiveDetail { get; init; }
    public required string SergeantName { get; init; }
    public required SergeantStyle SergeantStyle { get; init; }

    /// <summary>Young children of the house: they can be questioned but never accused.</summary>
    public required IReadOnlyList<Character> Children { get; init; }

    /// <summary>The rival detective, when the option is on.</summary>
    public RivalPlan? Rival { get; init; }

    public Character? Murderer => Suspects.FirstOrDefault(s => s.Id == MurdererId);

    public IEnumerable<Character> Culprits => Suspects.Where(s => CulpritIds.Contains(s.Id));

    public IEnumerable<Character> Visitors => Suspects.Where(s => s.IsVisitor);

    public bool IsCulprit(string id) => CulpritIds.Contains(id);

    public Room CrimeScene => Manor[CrimeSceneId];

    public Character? Person(string id) =>
        id == Victim.Id ? Victim
        : Rival is not null && id == Rival.Detective.Id ? Rival.Detective
        : Suspects.FirstOrDefault(s => s.Id == id) ?? Children.FirstOrDefault(s => s.Id == id);

    public string NameOf(string id) => Person(id)?.ShortName ?? id;
}
