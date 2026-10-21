namespace TerrainEditor.App;

// Expand World Size's settings in the world file.
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
        foreach (string key in startingGlobalKeys.Where(k => k.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            string[] parts = key.Split(' ', 2);
            values[parts[0].ToLowerInvariant()] = parts.Length > 1 ? parts[1] : "";
        }
        return values;
    }

    /// <summary>The world file's key of a setting ("World radius" is "ews_world_radius").</summary>
    public static string KeyOf(string setting) => KeyPrefix + setting.ToLowerInvariant().Replace(' ', '_');

    /// <summary>
    /// The zone grid a world's chunk files are laid out on, from its keys ("ews_gridwidth", written by the host that
    /// saved it): the game's 512 when the world has none, and when the value is not a width the chunk ids can name
    /// (512 to 2048; said in the log).
    /// </summary>
    /// <param name="startingGlobalKeys">The world file's starting global keys.</param>
    /// <param name="world">The world's name, for the log.</param>
    public static int SavedGridWidth(IEnumerable<string> startingGlobalKeys, string world)
    {
        string? text = Keys(startingGlobalKeys).GetValueOrDefault(GridWidthKey);
        int? width = Int(text);
        if (width is >= Save.ChunkMath.VanillaWidth and <= Save.ChunkMath.MaxWidth)
        {
            return width.Value;
        }
        if (text != null)
        {
            Console.WriteLine($"Expand World: {world}'s world file says its save grid is \"{text}\" zones wide, which no save can be: read as the game's {Save.ChunkMath.VanillaWidth}.");
        }
        return Save.ChunkMath.VanillaWidth;
    }
}
