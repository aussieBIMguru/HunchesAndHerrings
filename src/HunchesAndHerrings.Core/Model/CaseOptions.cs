namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// What the player chose on the title screen: a difficulty preset plus optional tweaks.
/// Together with the seed, this fully determines a case, so it's stored in save files.
/// </summary>
public sealed record CaseOptions(Difficulty Difficulty)
{
    public const int MinSuspects = 4;
    public const int MaxSuspects = 8;
    public const int EarliestEndHour = 14;
    public const int LatestEndHour = 20;

    /// <summary>Number of suspects, or null for the preset.</summary>
    public int? SuspectCount { get; init; }

    /// <summary>Hour (24h) the Chief Constable arrives, or null for the preset.</summary>
    public int? EndHour { get; init; }

    /// <summary>Times the sergeant bails you out, or null for the preset.</summary>
    public int? MaxRescues { get; init; }

    /// <summary>Whether the murderer frames people and innocents keep secrets, or null for the preset.</summary>
    public bool? RedHerrings { get; init; }

    /// <summary>When true, the sergeant never mishears.</summary>
    public bool ReliableSergeant { get; init; }

    /// <summary>A rival detective is on the case too: a nuisance who sometimes lets slip a clue.</summary>
    public bool Rival { get; init; }

    /// <summary>The kind of case to build, or null for a surprise.</summary>
    public CaseModel? Model { get; init; }

    /// <summary>Applies the tweaks to the difficulty preset.</summary>
    public DifficultySettings Resolve()
    {
        DifficultySettings s = DifficultySettings.For(Difficulty);
        return s with
        {
            SuspectCount = Math.Clamp(SuspectCount ?? s.SuspectCount, MinSuspects, MaxSuspects),
            EndHour = Math.Clamp(EndHour ?? s.EndHour, EarliestEndHour, LatestEndHour),
            MaxRescues = Math.Clamp(MaxRescues ?? s.MaxRescues, 0, 3),
            FrameLies = RedHerrings ?? s.FrameLies,
            SecretLiars = RedHerrings is { } herrings ? (herrings ? 1 : 0) : s.SecretLiars,
            SergeantErrorChance = ReliableSergeant ? 0 : s.SergeantErrorChance,
        };
    }

    /// <summary>True when anything differs from the plain preset.</summary>
    public bool IsCustom => SuspectCount is not null || EndHour is not null || MaxRescues is not null || RedHerrings is not null || ReliableSergeant || Model is not null;
}
