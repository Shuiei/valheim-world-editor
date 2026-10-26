using System.Text.Json;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// The names of the data mods keep on objects (ValheimRAFT's "MBParentId"...): the save keeps only their hashes, and
/// the game's code (ZdoKeys) does not know them. Like the game's, they are found among the string literals of the
/// mods' code: every DLL in the BepInEx plugins folders the editor knows (LocalGame.BepInExFolders), read as bytes,
/// never loaded. Kept in the data folder (mod-zdo-keys.json) until a DLL changes, and given to ZdoKeys, which still
/// names a key the game's way first.
/// </summary>
public static class ModKeys
{
    /// <summary>Where the names found are kept, with what they were read from.</summary>
    private static string CacheFile => Path.Combine(AppSettings.UserDataDir, "mod-zdo-keys.json");

    /// <summary>A file larger than this is not read (no mod's DLL is near it).</summary>
    private const long MaxFile = 512L << 20;

    /// <summary>Serialises refreshing.</summary>
    private static readonly Lock Lock = new();

    /// <summary>
    /// The kept file: the DLLs read (name:size:time each) and the names found, by hash.
    /// </summary>
    /// <param name="Stamp">What the names were read from.</param>
    /// <param name="Names">The names by hash.</param>
    private sealed record Kept(string Stamp, Dictionary<int, string> Names);

    /// <summary>
    /// The mods' data names in use by ZdoKeys: from the kept file while the DLLs are unchanged, else read again (and
    /// kept). Nothing in use when they cannot be read; never throws.
    /// </summary>
    /// <returns>How many names are in use.</returns>
    public static int Refresh(AppSettings settings)
    {
        lock (Lock)
        {
            try
            {
                var dlls = Dlls(settings);
                string stamp = JsonSerializer.Serialize(dlls.Select(f => $"{f.FullName}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
                var names = Cached(stamp);
                if (names == null)
                {
                    var found = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var dll in dlls)
                    {
                        found.UnionWith(Read(dll.FullName));
                    }
                    names = GameCatalogues.ByHash(found);
                    SafeFile.WriteAllText(CacheFile, JsonSerializer.Serialize(new Kept(stamp, names)));
                    Console.WriteLine($"mod data names: {names.Count} from {dlls.Count} DLLs.");
                }
                ZdoKeys.UseMods(names);
                return names.Count;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log.Caught(e, "reading the mods' data names (none used)");
                ZdoKeys.UseMods(new Dictionary<int, string>());
                return 0;
            }
        }
    }

    /// <summary>
    /// Every DLL in the plugins folders, by full path (each once), smaller than <see cref="MaxFile"/>.
    /// </summary>
    internal static List<FileInfo> Dlls(AppSettings settings)
    {
        var files = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
        foreach (var (dir, _) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            if (!Directory.Exists(plugins))
            {
                continue;
            }
            try
            {
                foreach (string f in Directory.EnumerateFiles(plugins, "*.dll", SearchOption.AllDirectories))
                {
                    var fi = new FileInfo(f);
                    if (fi.Length <= MaxFile)
                    {
                        files.TryAdd(fi.FullName, fi);
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be listed: its DLLs are not read.
                Log.Caught(e, "listing a plugins folder for the mods' data names (its DLLs are not read)");
            }
        }
        return [.. files.Values.OrderBy(f => f.FullName, StringComparer.Ordinal)];
    }

    /// <summary>A DLL's data-name candidates; none for a file that is not a .NET assembly or cannot be read.</summary>
    private static HashSet<string> Read(string dll)
    {
        try
        {
            return GameCatalogues.KeyNamesIn(dll);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            Log.CaughtBrief(e, "reading a DLL's data names (not a .NET assembly, or unreadable: none)");
            return [];
        }
    }

    /// <summary>The kept names when they were read from these DLLs; null otherwise (or when the file cannot be read).</summary>
    private static Dictionary<int, string>? Cached(string stamp)
    {
        try
        {
            return File.Exists(CacheFile) && JsonSerializer.Deserialize<Kept>(File.ReadAllText(CacheFile)) is { } kept && kept.Stamp == stamp ? kept.Names : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Caught(e, "reading the kept mods' data names (read from the DLLs again)");
            return null;
        }
    }
}
