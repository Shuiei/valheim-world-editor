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
        foreach (string[] pair in text.Split('\n').Select(l => l.Trim()).Where(IsSetting).Select(l => l.Split('=', 2)))
        {
            values[pair[0].Trim()] = pair[1].Trim();
        }
        return values;
    }

    /// <summary>Whether a trimmed line is a "Key = value" setting (not blank, a comment or a section heading).</summary>
    private static bool IsSetting(string line) => line.Length > 0 && line[0] is not ('#' or '[') && line.IndexOf('=') > 0;

    /// <summary>
    /// <see cref="ReadCfg"/> of a file; nothing when there is no file or it cannot be read (said in the log: its settings
    /// are then the defaults).
    /// </summary>
    /// <param name="path">The settings file; null for none.</param>
    private static Dictionary<string, string> CfgOf(string? path)
    {
        if (path == null)
        {
            return [];
        }
        try
        {
            return ReadCfg(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"reading {Path.GetFileName(path)} (its settings are taken as the defaults)");
            return [];
        }
    }
}
