
namespace ValheimGen;

public sealed partial class MapData
{
    /// <summary>
    /// A 0..1 value as a byte, rounded and held to 0..255.
    /// </summary>
    internal static byte Byte(float v) => (byte)Math.Clamp((int)Math.Round(v * 255f), 0, 255);

    /// <summary>
    /// A biome's colour on the map; white for any biome not in the game's list (and for combined flags).
    /// </summary>
    public static (byte, byte, byte) PixelColor(Heightmap.Biome biome)
    {
        foreach (var (kind, r, g, b) in BiomeColors)
        {
            if (kind == biome)
            {
                return (r, g, b);
            }
        }
        return (255, 255, 255);
    }

    /// <summary>
    /// A biome's colour on this world's map (Minimap.GetPixelColor with Expand World Data's prefix): the territory's
    /// map colour at the point when it has one, else the biome file's for a biome it gives settings to, else the
    /// game's (<see cref="PixelColor"/>; a biome the file gives a transparent colour, or none, shows the game's colour
    /// here rather than nothing).
    /// </summary>
    /// <param name="biome">The biome.</param>
    /// <param name="wx">World x.</param>
    /// <param name="wy">World z.</param>
    internal (byte, byte, byte) Pixel(Heightmap.Biome biome, float wx, float wy)
    {
        var rules = Generator.Rules;
        if (rules.Territories.Count > 0 && Generator.TerritoryAt(Generator.Stretched(wx), Generator.Stretched(wy))?.MapColor is { } territory)
        {
            return territory.Bytes();
        }
        return rules.Biomes.Of(biome) is { HasData: true, MapColor: { A: > 0f } colour } ? colour.Bytes() : PixelColor(biome);
    }

    /// <summary>
    /// A height as the map's height texture keeps it: the biome's map colour multiplier applied above the water (Expand
    /// World Data's patch on Minimap.GenerateWorldMap, for a biome its file gives settings to); the height itself
    /// otherwise.
    /// </summary>
    /// <param name="h">The height, metres.</param>
    /// <param name="biome">The biome.</param>
    internal float MapHeight(float h, Heightmap.Biome biome)
    {
        var rules = Generator.Rules;
        if (!rules.BiomeData || rules.Biomes.Of(biome) is not { HasData: true } info || h < rules.WaterLevel)
        {
            return h;
        }
        return (float)(((double)h - rules.WaterLevel) * info.MapColorMultiplier + rules.WaterLevel);
    }

    /// <summary>
    /// The mask texel at a world point (r forest, g mist, b ashlands lava/ocean). Mirrors Minimap.GetMaskColor: the
    /// water is its literal 30 whatever the rules say, the point goes in stretched (Expand World Size's prefix) and a
    /// biome of the biome file is masked as its terrain biome (Expand World Data's prefix).
    /// </summary>
    internal (byte, byte, byte) MaskColor(float wx, float wy, float height, Heightmap.Biome biome)
    {
        wx = Generator.Stretched(wx);
        wy = Generator.Stretched(wy);
        if (height < WorldGenerator.LiteralWaterLevel)
        {
            return (0, 0, Byte(Math.Clamp(Generator.GetAshlandsOceanGradient(wx, wy), 0f, 1f)));
        }
        Vector3 p = new(wx, 0f, wy);
        var rules = Generator.Rules;
        switch (rules.BiomeData ? rules.Biomes.Terrain(biome) : biome)
        {
            case Heightmap.Biome.Meadows:
                return Generator.InForest(p) ? ((byte)255, (byte)0, (byte)0) : ((byte)0, (byte)0, (byte)0);
            case Heightmap.Biome.Plains:
                return Generator.GetForestFactor(p) < 0.8f ? ((byte)255, (byte)0, (byte)0) : ((byte)0, (byte)0, (byte)0);
            case Heightmap.Biome.BlackForest:
                return (255, 0, 0);
            case Heightmap.Biome.Mistlands:
            {
                float f = Generator.GetForestFactor(p);
                float t = Math.Clamp((f - 1.1f) / 0.2f, 0f, 1f);
                return (0, Byte(1f - t * t * (3f - 2f * t)), 0);
            }
            case Heightmap.Biome.AshLands:
                Generator.GetAshlandsHeight(wx, wy, out Color mask, cheap: true);
                return (0, 0, Byte(mask.A));
            default:
                return (0, 0, 0);
        }
    }
}
