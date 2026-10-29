// Finding a Valheim install's data folders: its valheim_Data, the Mac app bundle, and the bundles folder in them (see GameLook).

namespace TerrainEditor.App;

public static partial class GameLook
{
    /// <summary>
    /// The data folder of a Valheim game folder, the one holding StreamingAssets and Managed: valheim_Data (either
    /// spelling) on Windows and Linux; on a Mac, the app bundle's Contents/Resources/Data (valheim.app in the folder
    /// Steam installed the game into). Null when the folder holds neither, with the game's bundles in it.
    /// </summary>
    public static string? DataDir(string valheim)
    {
        foreach (string sub in new[] { "valheim_Data", "Valheim_Data" })
        {
            string d = Path.Combine(valheim, sub);
            if (Directory.Exists(BundlesIn(d)))
            {
                return d;
            }
        }
        try
        {
            foreach (string app in Directory.EnumerateDirectories(valheim, "*.app").Order(StringComparer.Ordinal))
            {
                string d = Path.Combine(app, "Contents", "Resources", "Data");
                if (Directory.Exists(BundlesIn(d)))
                {
                    return d;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.CaughtBrief(e, "looking for the game's data folder in a Mac app (none)");
        }
        return null;
    }

    /// <summary>
    /// The bundles folder of a Valheim game folder (StreamingAssets/SoftRef/Bundles in its data folder, see
    /// <see cref="DataDir"/>), or null.
    /// </summary>
    public static string? BundlesDir(string valheim) => DataDir(valheim) is string d ? BundlesIn(d) : null;

    /// <summary>The bundles folder inside a game's data folder.</summary>
    private static string BundlesIn(string dataDir) => Path.Combine(dataDir, "StreamingAssets", "SoftRef", "Bundles");

    /// <summary>
    /// The game folder a data folder (see <see cref="DataDir"/>) belongs to: the folder above valheim_Data, or the
    /// folder holding the app bundle on a Mac.
    /// </summary>
    public static string GameOfData(string dataDir)
    {
        string d = Path.TrimEndingDirectorySeparator(dataDir);
        string? resources = Path.GetDirectoryName(d), contents = resources == null ? null : Path.GetDirectoryName(resources);
        string? app = contents == null ? null : Path.GetDirectoryName(contents);
        if (app != null && app.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(contents) == "Contents"
            && Path.GetFileName(resources) == "Resources" && Path.GetDirectoryName(app) is string folder)
        {
            return folder;
        }
        return Path.GetDirectoryName(d) ?? d;
    }
}
