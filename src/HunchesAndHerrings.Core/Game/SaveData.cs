using System.Text.Json;
using System.Text.Json.Serialization;
using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Core.Game;

/// <summary>
/// Everything needed to resume a case. The case itself is regenerated from <see cref="Seed"/> and
/// <see cref="Difficulty"/>; only the player's progress is stored.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public int Seed { get; set; }
    public Difficulty Difficulty { get; set; }
    public CaseOptions? Options { get; set; }
    public Detective? Detective { get; set; }
    public List<string> Circled { get; set; } = new();
    public List<string> Focused { get; set; } = new();
    public List<JournalItem> Journal { get; set; } = new();
    public List<ChatLine> Chat { get; set; } = new();
    public List<TrailStep> Trail { get; set; } = new();
    public int Minute { get; set; }
    public string RoomId { get; set; } = RoomIds.Hall;
    public List<string> Discovered { get; set; } = new();
    public List<string> SergeantFound { get; set; } = new();
    public List<string> SearchedRooms { get; set; } = new();
    public List<string> AskedKeys { get; set; } = new();
    public int Deduction { get; set; }
    public int RescuesUsed { get; set; }
    public int RescueCheckpoint { get; set; }
    public int ActionCount { get; set; }
    public SergeantTask? Sergeant { get; set; }
    public List<Clue> ExtraClues { get; set; } = new();
    public Dictionary<string, bool> Scrutiny { get; set; } = new();
    public List<string> Struck { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public Dictionary<string, Sighting> LastSeen { get; set; } = new();
    public List<LogEntry> Log { get; set; } = new();
    public List<Summons> Summons { get; set; } = new();
    public int RivalTipsGiven { get; set; }
    public List<string> RivalReturned { get; set; } = new();
    public int RivalPleas { get; set; }
    public Dictionary<string, List<WordSpan>> Highlights { get; set; } = new();
}

/// <summary>Reads and writes <see cref="SaveData"/> as JSON without throwing at the caller.</summary>
public static class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(SaveData data) => JsonSerializer.Serialize(data, Options);

    /// <summary>Parses save JSON; returns null and an error message if it can't be read.</summary>
    public static SaveData? TryDeserialize(string json, out string? error)
    {
        try
        {
            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, Options);
            if (data is null)
            {
                error = "The file is empty.";
                return null;
            }

            if (data.Version > SaveData.CurrentVersion)
            {
                error = "This case file was saved by a newer version of the game.";
                return null;
            }

            if (data.Version < SaveData.CurrentVersion)
            {
                error = "This case file was saved by an earlier version of the game, which built its mysteries differently. Start it afresh from its case number instead.";
                return null;
            }

            error = null;
            return data;
        }
        catch (JsonException ex)
        {
            error = $"The case file is damaged ({ex.Message}).";
            return null;
        }
        catch (NotSupportedException ex)
        {
            error = $"The case file could not be read ({ex.Message}).";
            return null;
        }
    }
}
