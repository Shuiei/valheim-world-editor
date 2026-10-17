using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// The dungeon generator's test setup shared by the DungeonGen test classes.
/// </summary>
public static class DungeonGenSupport
{
    /// <summary>
    /// Makes a dungeon with these settings.
    /// </summary>
    public static DungeonGen.Result Make(string style, string biome, int size, int levels, int seed) =>
        DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: style, Size: size, Levels: levels, Seed: seed));
}
