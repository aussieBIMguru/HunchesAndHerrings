using HunchesAndHerrings.Core.Generation;
using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Core.Game;

/// <summary>A trait value someone has been said to have, and whether it rules them out of the scene evidence.</summary>
/// <param name="Values">Every distinct value reported (more than one means somebody is lying).</param>
/// <param name="Excludes">The single reported value clashes with a scene indicator you've found.</param>
/// <param name="AtScene">This category is one of the scene indicators you've found.</param>
public sealed record KnownTrait(TraitCategory Category, IReadOnlyList<string> Values, bool Excludes, bool AtScene)
{
    public string Label => TraitRules.CategoryLabel(Category);

    public string ValueText => string.Join(" / ", Values.Select(v => TraitRules.ShortLabel(Category, v)));
}

/// <summary>A scene indicator you've found, and whether something has explained it away.</summary>
public sealed record KnownIndicator(TraitCategory Category, string Value, string? ExplainedBy)
{
    public string Label => TraitRules.SceneLabel(Category, Value);
}

/// <summary>Everything the inspector has learned about one person, gathered from the discovered clues.</summary>
public sealed class SuspectSummary
{
    public required Character Person { get; init; }

    /// <summary>Per night slot: each room they've been placed in, with how many separate sources say so.</summary>
    public required IReadOnlyList<IReadOnlyList<(string RoomId, int Sources)>> Night { get; init; }

    public required IReadOnlyList<KnownTrait> Traits { get; init; }
    public required IReadOnlyList<string> Motives { get; init; }
    public required IReadOnlyList<string> Secrets { get; init; }
    public required int Clashes { get; init; }
    public required int Statements { get; init; }

    /// <summary>Any scene indicator their known traits rule them out of.</summary>
    public bool RuledOutByTrait => Traits.Any(t => t.Excludes);

    /// <summary>The scene indicators found so far (shared by everyone's summary).</summary>
    public static IReadOnlyList<KnownIndicator> Indicators(GameSession game)
    {
        var discovered = game.DiscoveredClues.ToList();
        var explained = discovered.SelectMany(c => c.Facts.OfType<ExplainedFact>())
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.First().Explanation);
        return discovered.SelectMany(c => c.Facts.OfType<SceneFact>())
            .GroupBy(f => f.Category)
            .Select(g => new KnownIndicator(g.Key, g.First().Value, explained.GetValueOrDefault(g.Key)))
            .ToList();
    }

    public static SuspectSummary For(GameSession game, Character s)
    {
        var discovered = game.DiscoveredClues.ToList();
        var indicators = Indicators(game).Where(i => i.ExplainedBy is null).ToDictionary(i => i.Category, i => i.Value);

        var night = new List<IReadOnlyList<(string, int)>>();
        for (int slot = 0; slot < NightSlots.Count; slot++)
        {
            int captured = slot;
            night.Add(discovered
                .SelectMany(x => x.Facts.OfType<LocationFact>().Select(f => (Clue: x, Fact: f)))
                .Where(p => p.Fact.CharacterId == s.Id && p.Fact.Slot == captured)
                .GroupBy(p => p.Fact.RoomId)
                .Select(g => (g.Key, g.Select(p => p.Clue.Id).Distinct().Count()))
                .ToList());
        }

        var traits = discovered
            .SelectMany(x => x.Facts.OfType<TraitFact>())
            .Where(f => f.CharacterId == s.Id)
            .GroupBy(f => f.Category)
            .Select(g =>
            {
                var values = g.Select(f => f.Value).Distinct().ToList();
                bool atScene = indicators.TryGetValue(g.Key, out string? sceneValue);
                bool excludes = atScene && values.Count == 1 && TraitRules.Excludes(g.Key, sceneValue!, values[0]);
                return new KnownTrait(g.Key, values, excludes, atScene);
            })
            .OrderByDescending(t => t.AtScene)
            .ThenBy(t => t.Label)
            .ToList();

        var motives = discovered
            .SelectMany(x => x.Facts.OfType<MotiveFact>())
            .Where(f => f.CharacterId == s.Id)
            .Select(f => TextBank.MotiveNote(f.Motive))
            .Distinct()
            .ToList();

        var secrets = discovered
            .Where(x => x.SecretOf == s.Id || (x.Topic == Topic.Confront && x.SpeakerId == s.Id))
            .Select(x => x.Topic == Topic.Confront ? x.Text : $"found: {x.Title.ToLowerInvariant()}")
            .Distinct()
            .ToList();

        var clueIds = discovered.Where(x => x.SpeakerId == s.Id || x.TargetId == s.Id || x.Facts.Any(f => Mentions(f, s.Id))).Select(x => x.Id).ToHashSet();
        int clashes = game.Contradictions.Count(c => clueIds.Contains(c.ClueA) || clueIds.Contains(c.ClueB));

        return new SuspectSummary
        {
            Person = s,
            Night = night,
            Traits = traits,
            Motives = motives,
            Secrets = secrets,
            Clashes = clashes,
            Statements = discovered.Count(x => x.SpeakerId == s.Id),
        };
    }

    private static bool Mentions(Fact fact, string id) => fact switch
    {
        LocationFact f => f.CharacterId == id,
        TraitFact f => f.CharacterId == id,
        MotiveFact f => f.CharacterId == id,
        PresenceFact f => f.CharacterId == id,
        OccupancyFact f => f.Occupants.Contains(id),
        _ => false,
    };
}
