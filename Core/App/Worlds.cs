using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>The worlds on this computer: recent ones, folders added in Settings, and Valheim's own world folders.</summary>
public static class Worlds
{
    /// <summary>One world found.</summary>
    /// <param name="Name">The world's name (its folder's).</param>
    /// <param name="Path">Its folder, or the .db file of an old single-file world.</param>
    /// <param name="Saved">When its latest save was written (local time).</param>
    /// <param name="SaveNumber">The n of its latest _main.&lt;n&gt;.chunks; 0 for an old world.</param>
    /// <param name="Where">"recent", "yours" (a folder added in Settings) or "local" (the game's folders).</param>
    /// <param name="Usable">Whether the editor can open it.</param>
    /// <param name="Problem">Why it cannot, for the player; null when it can.</param>
    public sealed record Info(string Name, string Path, DateTime Saved, int SaveNumber, string Where, bool Usable, string? Problem);

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
                if (!Directory.Exists(full) || !seen.Add(full))
                {
                    return;
                }
                string? problem = Check(full);
                (int n, DateTime t) = LatestSave(full);
                list.Add(new Info(WorldName(full), full, t, n, where, problem == null, problem));
            }
            catch
            {
                // A folder that cannot be read is left out of the list.
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
                continue;
            }
            foreach (string db in dbs)
            {
                string name = Path.GetFileNameWithoutExtension(db);
                if (!Directory.Exists(Path.Combine(root, name)) && seen.Add(db))
                {
                    list.Add(new Info(name, db, File.GetLastWriteTime(db), 0, "local", false, "Old save format: load this world in Valheim once, then it can be edited here."));
                }
            }
        }
        return [.. list.OrderByDescending(w => w.Where == "recent").ThenByDescending(w => w.Saved)];
    }

    /// <summary>Why a folder cannot be opened as a world, or null when it can.</summary>
    public static string? Check(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Enter the world's folder.";
        }
        if (File.Exists(path) && path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return "That is an old single-file world: load it in Valheim once, then open its folder here.";
        }
        if (!Directory.Exists(path))
        {
            return "That folder does not exist.";
        }
        if (Places.HasWorld(path))
        {
            return null;
        }
        try
        {
            // A worlds_local folder with one world in it: point at the world instead.
            string[] inside = [.. Directory.GetDirectories(path).Where(Places.HasWorld)];
            return inside.Length > 0
                ? $"That folder holds worlds; pick one of them ({string.Join(", ", inside.Select(Path.GetFileName).Take(4))})."
                : "No Valheim world in that folder (a world folder holds _main.<n>.chunks and *.chunk files).";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return $"That folder cannot be read: {ex.Message}";
        }
    }

    /// <summary>A world's name: its folder's name.</summary>
    public static string WorldName(string path)
    {
        // The name is in _main.<n>.fwl2; the folder is named after the world anyway.
        return Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    /// <summary>The highest n of the folder's _main.&lt;n&gt;.chunks and its write time; (0, MinValue) when none.</summary>
    private static (int, DateTime) LatestSave(string dir)
    {
        int best = 0;
        DateTime t = DateTime.MinValue;
        foreach (string f in Directory.GetFiles(dir, "_main.*.chunks"))
        {
            Match m = Regex.Match(Path.GetFileName(f), @"_main\.(\d+)\.chunks");
            if (m.Success && int.Parse(m.Groups[1].Value) >= best)
            {
                best = int.Parse(m.Groups[1].Value);
                t = File.GetLastWriteTime(f);
            }
        }
        return (best, t);
    }
}
