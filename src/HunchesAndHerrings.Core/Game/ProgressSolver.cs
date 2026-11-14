using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Core.Game;

/// <summary>
/// Judges how far the player has really got, using only the truthful clues they have found.
/// Drives the sergeant's rescues and the hint system; never shown to the player directly.
/// </summary>
public static class ProgressSolver
{
    /// <summary>Innocents the player could already rule out with what they hold.</summary>
    public static HashSet<string> Eliminated(CaseFile caseFile, IEnumerable<Clue> discovered)
    {
        var truthful = discovered.Where(c => c.IsTruthful).ToList();
        var sceneKnown = truthful.SelectMany(c => c.Facts.OfType<SceneFact>()).Select(f => f.Category).ToHashSet();
        var result = new HashSet<string>();

        foreach (Character suspect in caseFile.Suspects.Where(s => s.Id != caseFile.MurdererId))
        {
            bool alibi = truthful.Any(c => c.AlibiFor.Contains(suspect.Id));
            bool trait = truthful.SelectMany(c => c.Facts.OfType<TraitFact>())
                .Any(f => f.CharacterId == suspect.Id
                          && sceneKnown.Contains(f.Category)
                          && TraitRules.Excludes(f.Category, caseFile.SceneValues[f.Category], f.Value));
            if (alibi || trait)
            {
                result.Add(suspect.Id);
            }
        }

        return result;
    }

    /// <summary>Fraction (0..1) of innocents the player could rule out.</summary>
    public static double Progress(CaseFile caseFile, IEnumerable<Clue> discovered)
    {
        int innocents = caseFile.Suspects.Count - 1;
        return innocents <= 0 ? 1 : Eliminated(caseFile, discovered).Count / (double)innocents;
    }
}
