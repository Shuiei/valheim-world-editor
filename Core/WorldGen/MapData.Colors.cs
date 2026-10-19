
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
    /// The mask texel at a world point (r forest, g mist, b ashlands lava/ocean). Mirrors Minimap.GetMaskColor: the
    /// water is its literal 30 whatever the rules say, and the point goes in stretched (Expand World Size's prefix).
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
        switch (biome)
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
