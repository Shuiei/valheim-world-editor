using System.Globalization;
using TerrainEditor.App;

namespace TerrainEditor.Save;

// The save's zone grid, as the world file names it, and a live world's keys from the game's own file.
public sealed partial class WorldSave
{
    /// <summary>The starting global key a host that saves on a wider grid keeps it under (zones per side: "ews_gridwidth 1024", Expand World Size's).</summary>
    public const string GridWidthKey = "ews_gridwidth";

    /// <summary>
    /// The zone grid a world's chunk files are laid out on, from its keys (<see cref="GridWidthKey"/>, written by the
    /// host that saved it; the last of two wins): the game's 512 when the world has none, and when the value is not a
    /// width the chunk ids can name (512 to 2048; said in the log).
    /// </summary>
    /// <param name="startingGlobalKeys">The world file's starting global keys.</param>
    /// <param name="world">The world's name, for the log.</param>
    public static int SavedGridWidth(IEnumerable<string> startingGlobalKeys, string world)
    {
        string? text = startingGlobalKeys.Select(k => k.Split(' ', 2)).LastOrDefault(p => p[0].Equals(GridWidthKey, StringComparison.OrdinalIgnoreCase)) is { } parts
            ? parts.Length > 1 ? parts[1] : ""
            : null;
        if (text == null)
        {
            return ChunkMath.VanillaWidth;
        }
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) && width is >= ChunkMath.VanillaWidth and <= ChunkMath.MaxWidth)
        {
            return width;
        }
        Console.WriteLine($"{world}'s world file says its save grid is \"{text}\" zones wide, which no save can be: read as the game's {ChunkMath.VanillaWidth}.");
        return ChunkMath.VanillaWidth;
    }

    /// <summary>
    /// Takes the world's starting global keys from its file on the game's disk (GET /world/meta): the loaded list the
    /// snapshot carries lacks what a mod takes out of it (Expand World Size's). An older bridge, a game without a world
    /// file, or a bridge that cannot serve it (an error status, said in the log) leaves the snapshot's.
    /// </summary>
    /// <param name="live">The game's bridge.</param>
    public async Task TakeLiveKeys(LiveBridge live)
    {
        try
        {
            byte[]? meta = await live.WorldMeta();
            if (meta != null)
            {
                TakeKeys(ReadMetadata(new MemoryStream(meta)));
            }
        }
        catch (Exception e) when (e is InvalidOperationException or EndOfStreamException or InvalidDataException or IOException)
        {
            Log.Caught(e, "reading the world file through the game's bridge (its keys are left as the game loaded them)");
        }
    }
}
