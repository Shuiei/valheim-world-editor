using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// A dresser made through its private constructor, for the tests of its own steps: the room's kit and settings, with a
/// new output that the steps write into.
/// </summary>
internal static class DungeonDresserInstance
{
    /// <summary>
    /// A dresser of a room as the hall step names it, and the output it writes into.
    /// </summary>
    /// <param name="room">The room: its material, biome, style and settings.</param>
    /// <param name="seed">The random numbers' seed.</param>
    /// <returns>The dresser (an instance of the private class) and its output.</returns>
    public static (object Dresser, DungeonOut Out) Of(DressingRoom room, int seed = 1)
    {
        var o = new DungeonOut();
        object dresser = PrivateCall.Create(
            typeof(DungeonDresser),
            [typeof(DungeonKit.Material), typeof(DungeonKit.Biome), typeof(DungeonKit.Style), typeof(DungeonGen.Settings), typeof(Random), typeof(DungeonOut), typeof(string)],
            room.Material, room.Biome, room.Style, room.Settings, new Random(seed), o, "Hall");
        return (dresser, o);
    }
}
