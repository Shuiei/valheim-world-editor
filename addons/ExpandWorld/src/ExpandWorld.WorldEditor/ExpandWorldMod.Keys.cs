namespace ExpandWorld.WorldEditor;

// Expand World Size's settings in the world file.
public static partial class ExpandWorldMod
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
        foreach (string key in startingGlobalKeys.Where(k => k.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            string[] parts = key.Split(' ', 2);
            values[parts[0].ToLowerInvariant()] = parts.Length > 1 ? parts[1] : "";
        }
        return values;
    }

    /// <summary>The world file's key of a setting ("World radius" is "ews_world_radius").</summary>
    public static string KeyOf(string setting) => KeyPrefix + setting.ToLowerInvariant().Replace(' ', '_');
}
