using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Core.Game;

/// <summary>
/// Compares the facts in the player's discovered clues and reports any that clash: someone placed in
/// two rooms at once, someone claiming a room another source says was empty or held only others,
/// or two different accounts of the same physical trait.
/// </summary>
public static class ContradictionFinder
{
    public static List<Contradiction> Find(CaseFile caseFile, IReadOnlyList<Clue> clues)
    {
        var result = new List<Contradiction>();
        var seen = new HashSet<string>();

        var locations = clues.SelectMany(c => c.Facts.OfType<LocationFact>().Select(f => (Clue: c, Fact: f))).ToList();
        var occupancies = clues.SelectMany(c => c.Facts.OfType<OccupancyFact>().Select(f => (Clue: c, Fact: f))).ToList();
        var presences = clues.SelectMany(c => c.Facts.OfType<PresenceFact>().Select(f => (Clue: c, Fact: f))).ToList();
        var traits = clues.SelectMany(c => c.Facts.OfType<TraitFact>().Select(f => (Clue: c, Fact: f))).ToList();

        void Add(string kind, Clue a, Clue b, string subject, string description)
        {
            string first = string.CompareOrdinal(a.Id, b.Id) < 0 ? a.Id : b.Id;
            string second = first == a.Id ? b.Id : a.Id;
            string key = $"{kind}|{subject}|{first}|{second}";
            if (seen.Add(key))
            {
                result.Add(new Contradiction(key, a.Id, b.Id, description));
            }
        }

        // Same person, same time, different rooms.
        for (int i = 0; i < locations.Count; i++)
        {
            for (int j = i + 1; j < locations.Count; j++)
            {
                var (ca, fa) = locations[i];
                var (cb, fb) = locations[j];
                if (ca == cb || fa.CharacterId != fb.CharacterId || fa.Slot != fb.Slot || fa.RoomId == fb.RoomId)
                {
                    continue;
                }

                string name = caseFile.NameOf(fa.CharacterId);
                Add("loc", ca, cb, $"{fa.CharacterId}:{fa.Slot}",
                    $"{name} is placed {In(caseFile, fa.RoomId)} ({Source(caseFile, ca)}) and {In(caseFile, fb.RoomId)} ({Source(caseFile, cb)}) at {NightSlots.Short(fa.Slot)}.");
            }
        }

        // Someone claims a room that another source says held only others, or nobody.
        foreach (var (oc, of) in occupancies)
        {
            foreach (var (lc, lf) in locations)
            {
                if (lc == oc || lf.Slot != of.Slot || lf.RoomId != of.RoomId || of.Occupants.Contains(lf.CharacterId))
                {
                    continue;
                }

                string who = of.Occupants.Count == 0
                    ? "nobody was there"
                    : $"only {string.Join(" and ", of.Occupants.Select(caseFile.NameOf))} was there";
                Add("occ", lc, oc, $"{lf.CharacterId}:{lf.Slot}",
                    $"{caseFile.NameOf(lf.CharacterId)} is placed in the {Room(caseFile, lf.RoomId)} at {NightSlots.Short(lf.Slot)} ({Source(caseFile, lc)}), but {Source(caseFile, oc)} shows {who}.");
            }
        }

        // Someone placed in (or out of) the house when a record says otherwise.
        foreach (var (pc, pf) in presences)
        {
            foreach (var (lc, lf) in locations)
            {
                bool away = lf.RoomId == RoomIds.Away;
                if (lc == pc || lf.CharacterId != pf.CharacterId || lf.Slot != pf.Slot || away != pf.InHouse)
                {
                    continue;
                }

                string where = away ? "away from the house" : $"in the {Room(caseFile, lf.RoomId)}";
                string record = pf.InHouse ? "still in the house" : "gone from the house";
                Add("presence", lc, pc, $"{lf.CharacterId}:{lf.Slot}",
                    $"{caseFile.NameOf(lf.CharacterId)} is placed {where} at {NightSlots.Short(lf.Slot)} ({Source(caseFile, lc)}), but {Source(caseFile, pc)} shows {caseFile.NameOf(lf.CharacterId)} {record}.");
            }
        }

        // Two accounts of the same trait.
        for (int i = 0; i < traits.Count; i++)
        {
            for (int j = i + 1; j < traits.Count; j++)
            {
                var (ca, fa) = traits[i];
                var (cb, fb) = traits[j];
                if (ca == cb || fa.CharacterId != fb.CharacterId || fa.Category != fb.Category || fa.Value == fb.Value)
                {
                    continue;
                }

                Add("trait", ca, cb, $"{fa.CharacterId}:{fa.Category}",
                    $"{caseFile.NameOf(fa.CharacterId)}: \"{TraitRules.ValueLabel(fa.Category, fa.Value)}\" ({Source(caseFile, ca)}) versus \"{TraitRules.ValueLabel(fb.Category, fb.Value)}\" ({Source(caseFile, cb)}).");
            }
        }

        return result;
    }

    private static string Room(CaseFile caseFile, string roomId) => caseFile.Manor.NameOf(roomId);

    private static string In(CaseFile caseFile, string roomId) =>
        roomId == RoomIds.Away ? "away from the house" : $"in the {Room(caseFile, roomId)}";

    private static string Source(CaseFile caseFile, Clue clue) =>
        clue.SpeakerId is not null ? $"{caseFile.NameOf(clue.SpeakerId)}'s word" : clue.Title.ToLowerInvariant();
}
