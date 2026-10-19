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
public sealed partial record ZoneFilter(IReadOnlySet<int> Biomes, bool NoBuildings, int Distance, bool NoEdits, bool OnlyGenerated, float? MinRadius, float? MaxRadius)
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
    /// A biome's name as the game shows it, in both languages; for a biome Expand World Data's file adds, the name the
    /// file gives it (its "name", else its id) when the world's biomes are given, else the type's own name.
    /// </summary>
    /// <param name="b">The biome.</param>
    /// <param name="biomes">The world's biomes; null for the game's.</param>
    public static Said BiomeName(ValheimGen.Heightmap.Biome b, ValheimGen.BiomeTable? biomes = null) => b switch
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
        _ => Said.Raw(biomes?.Of(b) is { } info ? (info.Name != "" ? info.Name : info.Id) : b.ToString()),
    };
}
