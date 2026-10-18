using System.Globalization;

namespace TerrainEditor.App;

// Reading what the mods write: the world file's keys and BepInEx's settings files.
public static partial class ExpandWorld
{
    /// <summary>
    /// Expand World Size's settings among a world's starting global keys: "ews_&lt;setting&gt; &lt;value&gt;", the
    /// setting's name lower-cased with underscores for spaces ("ews_world_radius 20000"), the value its text (empty
    /// when the key has none). By key, as the mod reads them (the last of two the same wins).
    /// </summary>
    /// <param name="startingGlobalKeys">The world file's starting global keys.</param>
    public static Dictionary<string, string> Keys(IEnumerable<string> startingGlobalKeys)
    {
        Dictionary<string, string> values = [];
        foreach (string key in startingGlobalKeys)
        {
            if (!key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            int space = key.IndexOf(' ');
            if (space < 0)
            {
                values[key.ToLowerInvariant()] = "";
            }
            else
            {
                values[key[..space].ToLowerInvariant()] = key[(space + 1)..];
            }
        }
        return values;
    }

    /// <summary>The world file's key of a setting ("World radius" is "ews_world_radius").</summary>
    public static string KeyOf(string setting) => KeyPrefix + setting.ToLowerInvariant().Replace(' ', '_');

    /// <summary>
    /// A BepInEx settings file's values by setting name: its "Key = value" lines, whatever section they are under
    /// (comments and headings passed over; the last of two the same wins).
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static Dictionary<string, string> ReadCfg(string text)
    {
        Dictionary<string, string> values = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or '[')
            {
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            values[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return values;
    }

    /// <summary>
    /// <see cref="ReadCfg"/> of a file; null when it cannot be read (said in the log: its settings are then the
    /// defaults).
    /// </summary>
    private static Dictionary<string, string>? ReadCfgFile(string path)
    {
        try
        {
            return ReadCfg(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"reading {Path.GetFileName(path)} (its settings are taken as the defaults)");
            return null;
        }
    }

    /// <summary>A setting's number, as the mod reads it (invariant culture); the default when missing or not a number.</summary>
    internal static float Float(string? value, float fallback) => Float(value) ?? fallback;

    /// <summary>A setting's number, or null when missing, empty or not a number (an unset optional setting).</summary>
    internal static float? Float(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f) ? f : null;

    /// <summary>A setting's whole number, or null when missing or not one.</summary>
    internal static int? Int(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;

    /// <summary>A setting's true or false; the default when missing or neither.</summary>
    internal static bool Bool(string? value, bool fallback) => bool.TryParse(value, out bool b) ? b : fallback;
}
