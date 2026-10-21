using TerrainEditor.Save;

namespace ValheimGen;

public sealed partial class TerrainService
{
    /// <summary>
    /// Renders the overview PNG: biome colours, water by depth, hill shading on land.
    /// </summary>
    private byte[] RenderOverview(int size)
    {
        float[] heights = new float[size * size];
        var biomes = new Heightmap.Biome[size * size];
        float step = 2f * MapRadius / size;
        // Row 0 is the north edge so the PNG has north up.
        Parallel.For(0, size, row =>
        {
            float z = MapRadius - (row + 0.5f) * step;
            for (int col = 0; col < size; col++)
            {
                float x = -MapRadius + (col + 0.5f) * step;
                int i = row * size + col;
                if (x * x + z * z > MapRadius * MapRadius)
                {
                    heights[i] = -400f;
                    biomes[i] = Heightmap.Biome.Ocean;
                    continue;
                }
                Heightmap.Biome biome = Generator.GetBiome(x, z);
                biomes[i] = biome;
                heights[i] = Generator.GetBiomeHeight(biome, x, z, out _);
            }
        });
        byte[] rgba = new byte[size * size * 4];
        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                int i = row * size + col;
                float h = heights[i];
                (float r, float g, float b) = BiomeColor(biomes[i], h, WaterLevel, Generator.Rules.Biomes);
                if (h >= WaterLevel)
                {
                    // Simple hill shading from the north-west.
                    float dx = heights[row * size + Math.Min(col + 1, size - 1)] - heights[row * size + Math.Max(col - 1, 0)];
                    float dz = heights[Math.Min(row + 1, size - 1) * size + col] - heights[Math.Max(row - 1, 0) * size + col];
                    float shade = Math.Clamp(1f - (dx - dz) * 0.012f, 0.55f, 1.35f);
                    r *= shade;
                    g *= shade;
                    b *= shade;
                }
                rgba[i * 4] = (byte)Math.Clamp(r, 0f, 255f);
                rgba[i * 4 + 1] = (byte)Math.Clamp(g, 0f, 255f);
                rgba[i * 4 + 2] = (byte)Math.Clamp(b, 0f, 255f);
                rgba[i * 4 + 3] = (byte)(heights[i] <= -400f ? 0 : 255);
            }
        }
        return Png.WriteRgba8(size, size, rgba);
    }

    /// <summary>
    /// The overview's colour (0..255 per channel) for one of the game's biomes at a height: blue, darker with depth,
    /// below the water level given.
    /// </summary>
    internal static (float, float, float) BiomeColor(Heightmap.Biome biome, float h, float water) => BiomeColor(biome, h, water, BiomeTable.Vanilla);

    /// <summary>
    /// The same for a world's biomes: a biome the biome file gives a map colour (with other settings, as the mod reads
    /// it) takes that colour; a biome the game does not have takes the colour of the biome whose ground it borrows.
    /// </summary>
    /// <param name="biome">The biome.</param>
    /// <param name="h">The height, metres.</param>
    /// <param name="water">The water level, metres.</param>
    /// <param name="table">The world's biomes.</param>
    internal static (float, float, float) BiomeColor(Heightmap.Biome biome, float h, float water, BiomeTable table)
    {
        if (h < water)
        {
            float depth = Math.Clamp((water - h) / 60f, 0f, 1f);
            return (40 - 25 * depth, 95 - 55 * depth, 140 - 50 * depth);
        }
        if (table.Of(biome) is { } info)
        {
            if (info.HasData && info.MapColor is { A: > 0f } c)
            {
                return (c.R * 255f, c.G * 255f, c.B * 255f);
            }
            if (!BiomeTable.GamesBiomes.Any(g => g.Value == biome))
            {
                biome = info.Terrain;
            }
        }
        return biome switch
        {
            Heightmap.Biome.Meadows => (112, 158, 72),
            Heightmap.Biome.BlackForest => (58, 92, 54),
            Heightmap.Biome.Swamp => (110, 94, 68),
            Heightmap.Biome.Mountain => (205, 210, 216),
            Heightmap.Biome.Plains => (196, 178, 96),
            Heightmap.Biome.Mistlands => (102, 98, 116),
            Heightmap.Biome.AshLands => (150, 64, 48),
            Heightmap.Biome.DeepNorth => (220, 232, 240),
            _ => (150, 150, 150)
        };
    }
}
