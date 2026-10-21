namespace TerrainEditor.App;

// BepInEx's settings files.
public static partial class ExpandWorld
{
    /// <summary>
    /// A BepInEx settings file's values by setting name: its "Key = value" lines, whatever section they are under
    /// (comments and headings passed over; the last of two the same wins).
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static Dictionary<string, string> ReadCfg(string text)
    {
        Dictionary<string, string> values = [];
        foreach (var (_, key, value) in ModSettings.Entries(text))
        {
            values[key] = value;
        }
        return values;
    }

    /// <summary>
    /// <see cref="ReadCfg"/> of a file; nothing when there is no file or it cannot be read (said in the log: its settings
    /// are then the defaults).
    /// </summary>
    /// <param name="path">The settings file; null for none.</param>
    private static Dictionary<string, string> CfgOf(string? path) => ReadCfg(ModSettings.TextOf(path) ?? "");
}
