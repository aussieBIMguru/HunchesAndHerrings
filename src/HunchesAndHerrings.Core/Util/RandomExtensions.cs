namespace HunchesAndHerrings.Core.Util;

/// <summary>Convenience helpers over <see cref="Random"/>. Everything here is deterministic for a given seed.</summary>
public static class RandomExtensions
{
    public static bool Chance(this Random rng, double probability) => rng.NextDouble() < probability;

    public static T Pick<T>(this Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    public static T? PickOrDefault<T>(this Random rng, IReadOnlyList<T> items) =>
        items.Count == 0 ? default : items[rng.Next(items.Count)];

    public static void Shuffle<T>(this Random rng, IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static List<T> Shuffled<T>(this Random rng, IEnumerable<T> items)
    {
        var list = items.ToList();
        rng.Shuffle(list);
        return list;
    }
}

/// <summary>A process-independent string hash (string.GetHashCode is randomised per process).</summary>
public static class StableHash
{
    public static int Of(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char ch in text)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
