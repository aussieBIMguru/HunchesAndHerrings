using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HunchesAndHerrings.Core.Model;

namespace HunchesAndHerrings.Settings;

/// <summary>
/// Remembers the detective's name and gender between sessions, in %LocalAppData%\HunchesAndHerrings\player.json
/// (kept when the game is uninstalled). Failure to read or write just falls back to the default.
/// </summary>
internal static class PlayerProfile
{
    private static readonly string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HunchesAndHerrings");
    private static readonly string FilePath = Path.Combine(FolderPath, "player.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Detective Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<Detective>(File.ReadAllText(FilePath), Options) is { } saved)
            {
                return Detective.Create(saved.Name, saved.Gender);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }

        return Detective.Default(Gender.Male);
    }

    public static void Save(Detective detective)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(detective, Options));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
