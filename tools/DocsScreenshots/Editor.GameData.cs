using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The game data the stand-in home is given: this computer's Valheim data (read only) or the copies kept by earlier scenes.
/// </summary>
public sealed partial class Editor
{
    /// <summary>
    /// Copies the index of the game's files and the game data read from them into the stand-in home: the ones the
    /// last scene kept (GameDataCache), else this computer's. The game data is stamped with the build that read it, so a
    /// new build reads the game again, once: the next scenes get it.
    /// </summary>
    private void CopyGameData()
    {
        string mineDir = Path.Combine(UserHome, ".local", "share", "ValheimWorldEditor");
        string from = Directory.Exists(GameDataCache) && Directory.GetFiles(GameDataCache, "game-*.json").Length > 0 ? GameDataCache : mineDir;
        foreach (string mine in Directory.Exists(from) ? Directory.GetFiles(from, "game-*.json") : [])
        {
            File.Copy(mine, Path.Combine(_data, Path.GetFileName(mine)));
        }
    }

    /// <summary>
    /// Makes the stand-in Valheim's valheim_Data folder: a link to this computer's game data when it is found, else an
    /// empty bundles folder.
    /// </summary>
    /// <param name="valheim">The stand-in Valheim folder.</param>
    private static void StandInValheim(string valheim)
    {
        string gameData = Path.Combine(valheim, "valheim_Data");
        Directory.CreateDirectory(valheim);
        if (RealGameData() is string real)
        {
            Directory.CreateSymbolicLink(gameData, real);
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(gameData, "StreamingAssets", "SoftRef", "Bundles"));
        }
    }

    /// <summary>
    /// This computer's Valheim valheim_Data folder (the app's settings first, then the usual Steam places), links
    /// resolved; null when none is found.
    /// </summary>
    private static string? RealGameData()
    {
        var places = new List<string>();
        try
        {
            string settings = Path.Combine(UserHome, ".local", "share", "ValheimWorldEditor", "settings.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(settings));
            if (doc.RootElement.TryGetProperty("ValheimPath", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } path)
            {
                places.Add(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        foreach (string steam in new[] { Path.Combine(UserHome, ".local", "share", "Steam"), Path.Combine(UserHome, ".steam", "steam"), "/opt/Steam" })
        {
            places.Add(Path.Combine(steam, "steamapps", "common", "Valheim"));
        }
        foreach (string v in places)
        {
            var b = new DirectoryInfo(Path.Combine(v, "valheim_Data"));
            if (b.Exists && Directory.Exists(Path.Combine(b.FullName, "StreamingAssets", "SoftRef", "Bundles")))
            {
                return b.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? b.FullName;
            }
        }
        return null;
    }
}
