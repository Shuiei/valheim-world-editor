using ValheimGen;

namespace TerrainEditor.App;

public static partial class SeedPreview
{
    /// <summary>
    /// The seed's preview on size x size cells (128: a quick look, 256: a clearer one); landmarks: where the
    /// bosses and traders will be too (a fraction of a second more, a few seconds on one core). Landmarks
    /// stay null when SeedLocations has no location rules.
    /// </summary>
    public static Preview Make(string seedName, int size = 160, bool parallel = false, bool landmarks = false, CancellationToken cancel = default)
    {
        var gen = Generator(seedName);
        float cell = 2 * Radius / size;
        var (biome, height) = Sample(gen, -Radius, -Radius, cell, size, size, parallel, cancel);
        var layout = new SeedLocations.Layout(gen, parallel);
        var placed = layout.Place(landmarks ? SeedLocations.Which.Prioritized : SeedLocations.Which.Start, cancel);
        var marks = new List<Landmark>();
        foreach (var p in placed)
        {
            int at = Array.FindIndex(Landmarks, l => l.Prefab == p.Rule.prefab);
            if (at >= 0)
            {
                marks.Add(new Landmark(Landmarks[at].Name, Landmarks[at].Kind, p.X, p.Z, p.Rule.unique != 0 && p.Rule.quantity > 1));
            }
        }
        var start = marks.FirstOrDefault(m => m.Kind == "start");
        var stats = SeedMeasure.Measure(biome, size, cell, start is null ? null : (start.X, start.Z)) with { TempleStart = start != null };
        landmarks &= SeedLocations.Available;
        if (landmarks)
        {
            stats = stats with
            {
                Nearest = Landmarks.Select(l => l.Name).Distinct().ToDictionary(n => n, n => marks.Where(m => m.Name == n)
                    .Select(m => MathF.Sqrt((m.X - stats.Start.X) * (m.X - stats.Start.X) + (m.Z - stats.Start.Z) * (m.Z - stats.Start.Z)))
                    .DefaultIfEmpty(float.PositiveInfinity).Min()),
            };
        }
        return new Preview(seedName, size, cell, biome, height, stats, landmarks ? marks : null, gen, layout);
    }

    /// <summary>
    /// The generator sampled on w x h cells of cell metres from (x0, z0) (row 0 the south): each cell's
    /// biome (index in Biomes, Ocean for the sea and outside the world) and ground height (Outside past the
    /// world's edge; the deepest sea floor is at 0). parallel: rows on every core but one (a search runs
    /// seeds side by side instead).
    /// </summary>
    public static (byte[] Biome, float[] Height) Sample(WorldGenerator gen, float x0, float z0, float cell, int w, int h, bool parallel, CancellationToken cancel = default)
    {
        byte[] biome = new byte[w * h];
        float[] height = new float[w * h];
        var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = parallel ? Math.Max(1, Environment.ProcessorCount - 1) : 1 };
        Parallel.For(0, h, options, j =>
        {
            for (int i = 0; i < w; i++)
            {
                float x = x0 + (i + 0.5f) * cell, z = z0 + (j + 0.5f) * cell;
                int k = j * w + i;
                if (x * x + z * z > Radius * Radius)
                {
                    biome[k] = Ocean;
                    height[k] = Outside;
                    continue;
                }
                var b = gen.GetBiome(x, z);
                float ground = gen.GetBiomeHeight(b, x, z, out _);
                height[k] = ground;
                int index = Array.IndexOf(Biomes, b);
                // Land: ground a player can stand on or wade through (at most 1 m under water; 3 m in a
                // Swamp, whose pools are part of it). The game names much of the shallow sea after the land
                // biome near it (and the southern sea Ashlands): deeper than that, it is sea here.
                bool swamp = b == Heightmap.Biome.Swamp;
                biome[k] = index < 0 || ground < TerrainService.WaterLevel - (swamp ? 3 : 1) ? Ocean : (byte)index;
            }
        });
        return (biome, height);
    }
}
