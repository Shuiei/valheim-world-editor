using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map's zone filter (index.html's filterZones, like MCA Selector): zones picked across the whole
/// world by biome, buildings, ground edits, whether the game generated them and distance from the middle,
/// out of ZoneStats' rows.
/// </summary>
/// <param name="Biomes">The Heightmap.Biome values to keep; empty keeps every biome.</param>
/// <param name="NoBuildings">Leave out zones within <paramref name="Distance"/> zones of one with buildings.</param>
/// <param name="Distance">How far from buildings, in zones, <paramref name="NoBuildings"/> keeps clear.</param>
/// <param name="NoEdits">Leave out zones with ground edits.</param>
/// <param name="OnlyGenerated">Keep only zones the game has generated.</param>
/// <param name="MinRadius">The least distance from the world's middle, in metres; null for no limit.</param>
/// <param name="MaxRadius">The greatest distance from the world's middle, in metres; null for no limit.</param>
public sealed record ZoneFilter(IReadOnlySet<int> Biomes, bool NoBuildings, int Distance, bool NoEdits, bool OnlyGenerated, float? MinRadius, float? MaxRadius)
{
    /// <summary>
    /// The biomes offered (the map's filter, the Mask), in the web editor's order.
    /// </summary>
    private static readonly ValheimGen.Heightmap.Biome[] Offered =
    [
        ValheimGen.Heightmap.Biome.Meadows, ValheimGen.Heightmap.Biome.BlackForest, ValheimGen.Heightmap.Biome.Swamp, ValheimGen.Heightmap.Biome.Mountain,
        ValheimGen.Heightmap.Biome.Plains, ValheimGen.Heightmap.Biome.Mistlands, ValheimGen.Heightmap.Biome.AshLands, ValheimGen.Heightmap.Biome.DeepNorth,
        ValheimGen.Heightmap.Biome.Ocean,
    ];

    /// <summary>
    /// The biomes offered (the map's filter, the Mask): their Heightmap.Biome values and names as the game shows them,
    /// in the web editor's order.
    /// </summary>
    /// <param name="english">The English names (what Claude is told and names biomes by) rather than the language in
    /// use.</param>
    public static (int Value, string Name)[] BiomeChoices(bool english = false) =>
        [.. Offered.Select(b => ((int)b, english ? BiomeName(b).English : BiomeName(b).Shown))];

    /// <summary>
    /// A biome's name as the game shows it, in both languages (the type's own name for one not offered).
    /// </summary>
    /// <param name="b">The biome.</param>
    public static Said BiomeName(ValheimGen.Heightmap.Biome b) => b switch
    {
        ValheimGen.Heightmap.Biome.Meadows => Said.Of("biome.meadows"),
        ValheimGen.Heightmap.Biome.BlackForest => Said.Of("biome.blackForest"),
        ValheimGen.Heightmap.Biome.Swamp => Said.Of("biome.swamp"),
        ValheimGen.Heightmap.Biome.Mountain => Said.Of("biome.mountain"),
        ValheimGen.Heightmap.Biome.Plains => Said.Of("biome.plains"),
        ValheimGen.Heightmap.Biome.Mistlands => Said.Of("biome.mistlands"),
        ValheimGen.Heightmap.Biome.AshLands => Said.Of("biome.ashlands"),
        ValheimGen.Heightmap.Biome.DeepNorth => Said.Of("biome.deepNorth"),
        ValheimGen.Heightmap.Biome.Ocean => Said.Of("biome.ocean"),
        _ => Said.Raw(b.ToString()),
    };

    /// <summary>
    /// Picks the zones matching the filter out of <c>ZoneStats.Compute</c>'s rows. A zone with pieces never
    /// matches: buildings are never reset from here.
    /// </summary>
    /// <param name="stats">ZoneStats' flat rows, <c>ZoneStats.Stride</c> ints per zone.</param>
    /// <returns>The matching zones' coordinates and how many objects they hold in all.</returns>
    public (List<(int X, int Z)> Zones, int Objects) Match(int[] stats)
    {
        var zones = new List<(int, int)>();
        int objects = 0;
        for (int i = 0; i + ZoneStats.Stride <= stats.Length; i += ZoneStats.Stride)
        {
            int x = stats[i], z = stats[i + 1], biome = stats[i + 2], pieces = stats[i + 3], edited = stats[i + 4], objs = stats[i + 5], d = stats[i + 6], gen = stats[i + 7];
            if (Biomes.Count > 0 && !Biomes.Contains(biome))
            {
                continue;
            }
            // Buildings are never reset from here: a zone with pieces never matches.
            if (pieces > 0 || (NoBuildings && d != -1 && d <= Distance))
            {
                continue;
            }
            if ((NoEdits && edited != 0) || (OnlyGenerated && gen == 0))
            {
                continue;
            }
            float r = MathF.Sqrt(x * x + z * z) * 64;
            if ((MinRadius is float lo && r < lo) || (MaxRadius is float hi && r > hi))
            {
                continue;
            }
            zones.Add((x, z));
            objects += objs;
        }
        return (zones, objects);
    }
}
