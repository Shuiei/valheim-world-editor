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
    /// <summary>The biomes offered (Heightmap.Biome values), in the web editor's order.</summary>
    public static readonly (int Value, string Name)[] BiomeChoices =
    [
        (1, "Meadows"), (8, "Black Forest"), (2, "Swamp"), (4, "Mountain"), (16, "Plains"), (512, "Mistlands"), (32, "Ashlands"), (64, "Deep North"), (256, "Ocean"),
    ];

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
