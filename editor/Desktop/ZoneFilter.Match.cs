using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial record ZoneFilter
{
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
