namespace HunchesAndHerrings.Core.Model;

/// <summary>Every tunable that changes with difficulty, for both generation and play.</summary>
/// <param name="SuspectCount">Number of suspects (excluding the victim).</param>
/// <param name="EndHour">Hour (24h) by which a name must be given.</param>
/// <param name="StartingDeduction">Deduction points at the start.</param>
/// <param name="ScrutinyAccuracy">Chance a Scrutinise verdict is correct.</param>
/// <param name="ScrutinyCost">Deduction cost of Scrutinise.</param>
/// <param name="HunchCost">Deduction cost of a Hunch.</param>
/// <param name="MaxRescues">How many times the sergeant volunteers a finding when you're behind.</param>
/// <param name="SergeantSkill">Chance the sergeant finds each item when sent on a task.</param>
/// <param name="SergeantErrorChance">Chance the sergeant misreports something on an interview.</param>
/// <param name="AlibiTwins">Innocents who match the scene traits and can only be cleared by alibi.</param>
/// <param name="GenderSightingChance">Chance someone glimpsed the killer's figure.</param>
/// <param name="TraitSourceCount">How many independent sources reveal each trait that clears an innocent.</param>
/// <param name="MurdererExposure">Chance there's a truthful source exposing the murderer's own trait lie.</param>
/// <param name="FrameLies">Whether the murderer tries to frame someone.</param>
/// <param name="SecretLiars">Innocents who lie about something unrelated (red herrings).</param>
/// <param name="HighlightExclusions">Suspect matrix highlights traits that rule people out.</param>
/// <param name="RevealTimeOfDeath">Briefing states the time of death.</param>
/// <param name="Blurb">Description shown on the title screen.</param>
public sealed record DifficultySettings(
    Difficulty Difficulty,
    int SuspectCount,
    int EndHour,
    int StartingDeduction,
    double ScrutinyAccuracy,
    int ScrutinyCost,
    int HunchCost,
    int MaxRescues,
    double SergeantSkill,
    double SergeantErrorChance,
    int AlibiTwins,
    double GenderSightingChance,
    int TraitSourceCount,
    double MurdererExposure,
    bool FrameLies,
    int SecretLiars,
    bool HighlightExclusions,
    bool RevealTimeOfDeath,
    string Blurb)
{
    public static DifficultySettings For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Novice => new DifficultySettings(Difficulty.Novice, 5, 18, 4, 1.0, 1, 2, 2, 0.85, 0.05, 1, 0.85, 2, 1.0, false, 0, true, true,
            "Five suspects, until six o'clock. Your instincts never lie and your sergeant will bail you out twice."),
        Difficulty.Inspector => new DifficultySettings(Difficulty.Inspector, 6, 17, 2, 0.85, 2, 3, 1, 0.7, 0.12, 2, 0.6, 2, 0.8, true, 1, false, false,
            "Six suspects, until five o'clock. Instincts are usually right; one rescue from the sergeant."),
        _ => new DifficultySettings(Difficulty.Master, 7, 16, 0, 0.65, 2, 4, 0, 0.55, 0.2, 2, 0.4, 1, 0.6, true, 1, false, false,
            "Seven suspects, until four o'clock. Trust nothing, least of all your sergeant. No rescues."),
    };
}
