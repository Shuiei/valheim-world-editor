using System.Text.RegularExpressions;

namespace TerrainEditor.App;

public static partial class Worlds
{
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
            Log.Caught(ex, "checking a chosen world folder");
            return $"That folder cannot be read: {ex.Message}";
        }
    }

    /// <summary>
    /// The highest n of the folder's _main.&lt;n&gt;.chunks and its write time; (0, MinValue) when none. A number too
    /// large to be a save's is passed over.
    /// </summary>
    internal static (int, DateTime) LatestSave(string dir)
    {
        int best = 0;
        DateTime t = DateTime.MinValue;
        foreach (string f in Directory.GetFiles(dir, "_main.*.chunks"))
        {
            Match m = Regex.Match(Path.GetFileName(f), @"_main\.(\d+)\.chunks");
            if (m.Success && int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) && n >= best)
            {
                best = n;
                t = File.GetLastWriteTime(f);
            }
        }
        return (best, t);
    }
}
