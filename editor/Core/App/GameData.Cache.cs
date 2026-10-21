using System.Text.Json;

namespace TerrainEditor.App;

// The game data caches: the file of each kind, what a cache is out of date by, and reading and keeping them (see GameData).
public static partial class GameData
{
    /// <summary>
    /// A kind's cache file in the user's data folder: game-&lt;kind&gt;.json.
    /// </summary>
    public static string CacheFile(string kind) => Path.Combine(AppSettings.UserDataDir, $"game-{kind}.json");

    /// <summary>
    /// Valheim's folder from its bundles folder (&lt;data folder&gt;/StreamingAssets/SoftRef/Bundles, the data folder
    /// being valheim_Data, or valheim.app/Contents/Resources/Data on a Mac).
    /// </summary>
    internal static string ValheimOf(string bundles)
    {
        string? d = Path.TrimEndingDirectorySeparator(bundles);
        for (int i = 0; i < 3 && d != null; i++)
        {
            d = Path.GetDirectoryName(d);
        }
        return d == null ? bundles : GameLook.GameOfData(d);
    }

    /// <summary>
    /// The game's files a kind is read from, by name, size and time: what makes a cache out of date.
    /// </summary>
    internal static string Stamp(string valheim, string bundles) =>
        CacheVersion + " " + JsonSerializer.Serialize(Directory.GetFiles(bundles).Order(StringComparer.Ordinal)
            .Concat(new[] { GameLook.DataDir(valheim) }.OfType<string>().Select(d => Path.Combine(d, "Managed", "assembly_valheim.dll")).Where(File.Exists))
            .Select(f => new FileInfo(f)).Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));

    /// <summary>
    /// Keeps a cache, written whole or not at all (a crash half way leaves the old file, or none). One that cannot be
    /// written (a full disk, a read-only folder, another editor writing it too) is only logged: what was read is used
    /// all the same, and read again next time.
    /// </summary>
    /// <returns>False when it could not be written.</returns>
    internal static bool KeepCache(string file, string text)
    {
        try
        {
            // Flushed to disk before it replaces the old one; the temporary file is gone when this fails.
            SafeFile.WriteAllText(file, text);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, $"keeping the cache {Path.GetFileName(file)} (read again next time)");
            return false;
        }
    }

    /// <summary>
    /// A cache when it is for this game and editor; else null (read again).
    /// </summary>
    internal static string? Cached(string file, string stamp)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            string[] cached = File.ReadAllText(file).Split('\n', 2);
            return cached.Length == 2 && cached[0] == stamp ? cached[1] : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, $"reading the cache {Path.GetFileName(file)} (read from the game again)");
            return null;
        }
    }
}
