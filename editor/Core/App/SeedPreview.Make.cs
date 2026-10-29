using ValheimGen;

namespace TerrainEditor.App;

public static partial class SeedPreview
{
    /// <summary>
    /// The seed's preview on size x size cells (128: a quick look, 256: a clearer one); landmarks: where the
    /// bosses and traders will be too (a fraction of a second more, a few seconds on one core). Landmarks
    /// stay null when SeedLocations has no location rules. With a world generation mod's rules, the world is the mod's
    /// (its radius, biomes and places).
    /// </summary>
    public static Preview Make(string seedName, int size = 160, bool parallel = false, bool landmarks = false, WorldRules? rules = null, CancellationToken cancel = default)
    {
        var gen = Generator(seedName, rules);
        float radius = gen.Rules.Radius;
        float cell = 2 * radius / size;
        var cells = SampleCells(gen, -radius, -radius, cell, size, size, parallel, cancel);
        var (biome, height) = (cells.Biome, cells.Height);
        var layout = new SeedLocations.Layout(gen, parallel);
        var placed = layout.Place(landmarks ? SeedLocations.Which.Prioritized : SeedLocations.Which.Start, cancel);
        var marks = new List<Landmark>();
        foreach (var p in placed)
        {
            int at = Array.FindIndex(Landmarks, l => l.Prefab == p.Rule.Prefab);
            if (at >= 0)
            {
                marks.Add(new Landmark(Landmarks[at].Name, Landmarks[at].Kind, p.X, p.Z, p.Rule.Unique != 0 && p.Rule.Quantity > 1));
            }
        }
        var start = marks.FirstOrDefault(m => m.Kind == "start");
        var stats = SeedMeasure.Measure(biome, size, cell, start is null ? null : (start.X, start.Z), radius) with { TempleStart = start != null };
        landmarks &= SeedLocations.AvailableFor(gen);
        if (landmarks)
        {
            stats = stats with
            {
                Nearest = Landmarks.Select(l => l.Name).Distinct().ToDictionary(n => n, n => marks.Where(m => m.Name == n)
                    .Select(m => MathF.Sqrt((m.X - stats.Start.X) * (m.X - stats.Start.X) + (m.Z - stats.Start.Z) * (m.Z - stats.Start.Z)))
                    .DefaultIfEmpty(float.PositiveInfinity).Min()),
            };
        }
        return new Preview(seedName, size, cell, biome, height, stats, landmarks ? marks : null, gen, layout, radius, cells.Color, gen.Rules.WaterLevel);
    }

    /// <summary>The generator sampled on cells: each cell's biome and height (<see cref="SampleCells"/>).</summary>
    public static (byte[] Biome, float[] Height) Sample(WorldGenerator gen, float x0, float z0, float cell, int w, int h, bool parallel, CancellationToken cancel = default)
    {
        var cells = SampleCells(gen, x0, z0, cell, w, h, parallel, cancel);
        return (cells.Biome, cells.Height);
    }

    /// <summary>Cells sampled from a generator (<see cref="SampleCells"/>).</summary>
    /// <param name="Biome">Each cell's biome: index in <see cref="Biomes"/>, <see cref="Ocean"/> for the sea and outside the world.</param>
    /// <param name="Height">Each cell's ground height (<see cref="Outside"/> past the world's edge).</param>
    /// <param name="Color">
    /// Each land cell's colour on the world's map, three bytes (red, green, blue) per cell, when the rules colour the
    /// map their own way (a biome of Expand World Data's file, a territory); null when the game's own colours apply.
    /// </param>
    public sealed record Cells(byte[] Biome, float[] Height, byte[]? Color);

    /// <summary>
    /// The generator sampled on w x h cells of cell metres from (x0, z0) (row 0 the south): each cell's
    /// biome (index in Biomes, Ocean for the sea and outside the world's radius; a biome the game does not have
    /// counts as the one whose ground it borrows), ground height (Outside past the world's edge; the deepest sea
    /// floor is at 0) and, with a world generation mod's biome or territory colours, its colour on the map. parallel:
    /// rows on every core but one (a search runs seeds side by side instead).
    /// </summary>
    public static Cells SampleCells(WorldGenerator gen, float x0, float z0, float cell, int w, int h, bool parallel, CancellationToken cancel = default)
    {
        byte[] biome = new byte[w * h];
        float[] height = new float[w * h];
        float radius = gen.Rules.Radius;
        var table = gen.Rules.Biomes;
        byte[]? color = gen.Rules.BiomeData || gen.Rules.Territories.Count > 0 ? new byte[w * h * 3] : null;
        var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = parallel ? Math.Max(1, Environment.ProcessorCount - 1) : 1 };
        Parallel.For(0, h, options, j =>
        {
            for (int i = 0; i < w; i++)
            {
                float x = x0 + (i + 0.5f) * cell, z = z0 + (j + 0.5f) * cell;
                int k = j * w + i;
                if (x * x + z * z > radius * radius)
                {
                    biome[k] = Ocean;
                    height[k] = Outside;
                    continue;
                }
                var real = gen.GetBiome(x, z);
                var b = table.Terrain(real);
                float ground = gen.GetBiomeHeight(b, x, z, out _);
                height[k] = ground;
                int index = Array.IndexOf(Biomes, b);
                // Land: ground a player can stand on or wade through (at most 1 m under water; 3 m in a
                // Swamp, whose pools are part of it). The game names much of the shallow sea after the land
                // biome near it (and the southern sea Ashlands): deeper than that, it is sea here.
                bool swamp = b == Heightmap.Biome.Swamp;
                biome[k] = index < 0 || ground < gen.Rules.WaterLevel - (swamp ? 3 : 1) ? Ocean : (byte)index;
                if (color != null && biome[k] != Ocean)
                {
                    (color[3 * k], color[3 * k + 1], color[3 * k + 2]) = MapData.PixelColor(gen, real, x, z);
                }
            }
        });
        return new Cells(biome, height, color);
    }
}
