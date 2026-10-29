// Finding the game folder from a folder the user chose near it (see GameLook).

namespace TerrainEditor.App;

public static partial class GameLook
{
    /// <summary>
    /// The game folder from a folder chosen near it: the game folder itself, its valheim_Data, on a Mac the app
    /// bundle or a folder inside it, or a folder above it in a Steam library (Steam, steamapps, common). Null when
    /// none of these.
    /// </summary>
    public static string? GameFolder(string chosen)
    {
        try
        {
            string full = Path.GetFullPath(chosen.Trim().Trim('"'));
            // Without a trailing separator (kept on a drive's or the file system's root).
            full = Path.TrimEndingDirectorySeparator(full);
            string? parent = Path.GetDirectoryName(full);
            var tries = new List<string> { full };
            if (parent != null && Path.GetFileName(full).Equals("valheim_Data", StringComparison.OrdinalIgnoreCase))
            {
                tries.Add(parent);
            }
            // On a Mac: valheim.app, or a folder inside it (the folder picker can open the bundle).
            for (string? d = full; d != null; d = Path.GetDirectoryName(d))
            {
                if (d.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(d) is string holder)
                {
                    tries.Add(holder);
                    break;
                }
            }
            tries.Add(Path.Combine(full, "Valheim"));
            tries.Add(Path.Combine(full, "common", "Valheim"));
            tries.Add(Path.Combine(full, "steamapps", "common", "Valheim"));
            return tries.FirstOrDefault(t => BundlesDir(t) != null);
        }
        catch (Exception e)
        {
            Log.CaughtBrief(e, "finding the game folder from a chosen folder (none)");
            return null;
        }
    }
}
