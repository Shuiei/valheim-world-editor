namespace TerrainEditor.App;

/// <summary>The worlds on this computer: recent ones, folders added in Settings, and Valheim's own world folders.</summary>
public static partial class Worlds
{
    /// <summary>One world found.</summary>
    /// <param name="Name">The world's name (its folder's).</param>
    /// <param name="Path">Its folder, or the .db file of an old single-file world.</param>
    /// <param name="Saved">When its latest save was written (local time).</param>
    /// <param name="SaveNumber">The n of its latest _main.&lt;n&gt;.chunks; 0 for an old world.</param>
    /// <param name="Where">"recent", "yours" (a folder added in Settings) or "local" (the game's folders).</param>
    /// <param name="Usable">Whether the editor can open it.</param>
    /// <param name="Problem">Why it cannot, for the player; null when it can.</param>
    /// <param name="ExpandWorld">Whether the world was made with Expand World Size (its world file keeps the mod's settings).</param>
    public sealed record Info(string Name, string Path, DateTime Saved, int SaveNumber, string Where, bool Usable, string? Problem, bool ExpandWorld = false);

    /// <summary>Every world found, each once: recent ones first, then the newest saves first.</summary>
    public static List<Info> Find(AppSettings settings)
    {
        var list = new List<Info>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string dir, string where)
        {
            try
            {
                string full = Path.GetFullPath(dir);
                // Once, however it is reached (a link to it, a separator at the end, another letter case on Windows).
                if (!Directory.Exists(full) || !seen.Add(Places.FolderKey(full)))
                {
                    return;
                }
                string? problem = Check(full);
                (int n, DateTime t) = LatestSave(full);
                list.Add(new Info(WorldName(full), full, t, n, where, problem == null, problem, problem == null && MadeWithExpandWorld(full, n)));
            }
            // A folder that cannot be read, or a stored path that is no path: left out, and said in the log.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Log.Caught(ex, "listing a world folder (left out of the list)");
            }
        }
        foreach (var r in settings.Recent)
        {
            Add(r.Path, "recent");
        }
        // Folders added in Settings: a world, or a folder of worlds.
        // (A drive's root has folders that cannot be read: those are skipped.)
        foreach (string folder in settings.WorldFolders.Where(Directory.Exists))
        {
            if (Places.HasWorld(folder))
            {
                Add(folder, "yours");
                continue;
            }
            foreach (string d in Places.Subfolders(folder).Where(Places.HasWorld))
            {
                Add(d, "yours");
            }
        }
        foreach (string root in Places.WorldRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }
            foreach (string d in Places.Subfolders(root).Where(Places.HasWorld))
            {
                Add(d, "local");
            }
            // Old single-file worlds (<World>.db): the game converts them when it next loads them.
            string[] dbs;
            try
            {
                dbs = Directory.GetFiles(root, "*.db");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Log.CaughtBrief(ex, "looking for old single-file worlds in a worlds folder (passed over)");
                continue;
            }
            foreach (string db in dbs)
            {
                string name = Path.GetFileNameWithoutExtension(db);
                if (!Directory.Exists(Path.Combine(root, name)) && seen.Add(Places.FolderKey(db)))
                {
                    list.Add(new Info(name, db, File.GetLastWriteTime(db), 0, "local", false, "Old save format: load this world in Valheim once, then it can be edited here."));
                }
            }
        }
        return [.. list.OrderByDescending(w => w.Where == "recent").ThenByDescending(w => w.Saved)];
    }

    /// <summary>
    /// Whether a world's file keeps Expand World Size's settings (the world was made with the mod); false when the
    /// file cannot be read (said in the log; the world opens, or not, as Check said).
    /// </summary>
    /// <param name="folder">The world's folder.</param>
    /// <param name="saveNumber">The save to look at.</param>
    private static bool MadeWithExpandWorld(string folder, int saveNumber)
    {
        try
        {
            return ExpandWorld.Keys(Save.WorldSave.ReadMetadata(Path.Combine(folder, $"_main.{saveNumber}.fwl2")).StartingGlobalKeys).Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or EndOfStreamException)
        {
            Log.CaughtBrief(ex, "reading a world file's head for Expand World's keys (taken as none)");
            return false;
        }
    }

    /// <summary>A world's name: its folder's name.</summary>
    public static string WorldName(string path)
    {
        // The name is in _main.<n>.fwl2; the folder is named after the world anyway.
        return Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
