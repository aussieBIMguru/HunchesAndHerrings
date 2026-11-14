using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

/// <summary>Period-appropriate names for people and houses.</summary>
public static class NameBank
{
    private static readonly string[] MaleFirst =
    {
        "Albert", "Arthur", "Augustus", "Bartholomew", "Cecil", "Charles", "Cuthbert", "Edmund", "Edward", "Ernest",
        "Frederick", "George", "Gilbert", "Harold", "Henry", "Horace", "Hugh", "Jasper", "Josiah", "Lionel",
        "Montague", "Mortimer", "Nathaniel", "Oswald", "Percival", "Reginald", "Rupert", "Septimus", "Silas", "Thaddeus",
        "Theodore", "Walter", "William", "Ambrose", "Basil", "Clement", "Desmond", "Ezra", "Felix", "Ignatius",
    };

    private static readonly string[] FemaleFirst =
    {
        "Ada", "Agatha", "Beatrice", "Charlotte", "Clara", "Constance", "Cordelia", "Dorothea", "Edith", "Eleanor",
        "Eliza", "Emmeline", "Esther", "Euphemia", "Florence", "Georgiana", "Harriet", "Henrietta", "Imogen", "Isadora",
        "Lavinia", "Letitia", "Lucinda", "Mabel", "Margaret", "Matilda", "Millicent", "Ophelia", "Philippa", "Prudence",
        "Rosalind", "Sophronia", "Theodora", "Ursula", "Victoria", "Violet", "Winifred", "Hester", "Annie", "Martha",
    };

    private static readonly string[] Surnames =
    {
        "Ashcombe", "Blackwood", "Pembroke", "Fairfax", "Hollis", "Whitcombe", "Thorne", "Marchbanks", "Crowley", "Pike",
        "Hobbs", "Grimsby", "Lockwood", "Ravenscroft", "Danvers", "Featherstone", "Gage", "Harrow", "Ingram", "Jessop",
        "Kettering", "Larkin", "Mallory", "Nettleship", "Ormsby", "Prowse", "Quill", "Rook", "Sallow", "Tallis",
        "Underhill", "Vane", "Wren", "Yardley", "Bramble", "Cobb", "Dimmock", "Fenwick", "Gilchrist", "Hatch",
        "Inchbald", "Jarrow", "Kemble", "Ludlow", "Meeks", "Norris", "Oakes", "Pargeter", "Ratchett", "Spratt",
        "Treadwell", "Upjohn", "Voss", "Whitlow", "Crabtree", "Dunmore", "Edgecombe", "Fogg", "Grantham", "Hargreaves",
    };

    private static readonly string[] Houses =
    {
        "Ravensmere Hall", "Blackthorn Manor", "Greyfriars House", "Hollowmere Court", "Wexley Grange", "Ashgrove Park",
        "Thornbury Hall", "Coldharbour House", "Larkspur Manor", "Gallowmoor Hall", "Sedgewick Abbey", "Mistlecombe Manor",
    };

    public static string TakeFirst(Random rng, Gender gender, HashSet<string> used) =>
        Take(rng, gender == Gender.Male ? MaleFirst : FemaleFirst, used);

    public static string TakeSurname(Random rng, HashSet<string> used) => Take(rng, Surnames, used);

    public static string HouseName(Random rng) => rng.Pick(Houses);

    private static string Take(Random rng, IReadOnlyList<string> pool, HashSet<string> used)
    {
        var available = pool.Where(n => !used.Contains(n)).ToList();
        string name = available.Count > 0 ? rng.Pick(available) : rng.Pick(pool);
        used.Add(name);
        return name;
    }
}
