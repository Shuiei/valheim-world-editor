using System.Globalization;

namespace ValheimGen;

public static partial class DumpVerifier
{
    /// <summary>
    /// Compares one point's biome and base height with the seed's generator.
    /// </summary>
    internal static void CompareHeight(string[] p, Tally t, string seedName)
    {
        WorldGenerator gen = t.Generator ??= Init(seedName);
        float x = F(p[1]), z = F(p[2]), want = F(p[4]);
        var biome = gen.GetBiome(x, z);
        float got = gen.GetBiomeHeight(biome, x, z, out _);
        if ((int)biome != int.Parse(p[3], CultureInfo.InvariantCulture))
        {
            t.BiomesWrong++;
            t.Error($"Biome at ({x}, {z}): expected {(Heightmap.Biome)int.Parse(p[3], CultureInfo.InvariantCulture)} got {biome}");
        }
        if (BitConverter.SingleToInt32Bits(got) == BitConverter.SingleToInt32Bits(want))
        {
            t.HeightsOk++;
        }
        else
        {
            t.HeightsWrong++;
            t.HeightMax = Math.Max(t.HeightMax, Math.Abs(got - want));
            t.Error($"Height at ({x}, {z}): expected {want:R} got {got:R}");
        }
    }

    /// <summary>
    /// Compares a zone's base heights, vertex by vertex, with the seed's generator.
    /// </summary>
    internal static void CompareZone(string[] p, Tally t, string seedName)
    {
        WorldGenerator gen = t.Generator ??= Init(seedName);
        int zx = int.Parse(p[1], CultureInfo.InvariantCulture), zz = int.Parse(p[2], CultureInfo.InvariantCulture);
        float[] got = BaseTerrain.BuildZone(gen, zx, zz);
        int bad = 0;
        for (int i = 0; i < got.Length; i++)
        {
            float want = F(p[3 + i]);
            if (BitConverter.SingleToInt32Bits(got[i]) != BitConverter.SingleToInt32Bits(want))
            {
                bad++;
                t.ZoneMax = Math.Max(t.ZoneMax, Math.Abs(got[i] - want));
            }
        }
        if (bad == 0)
        {
            t.ZonesOk++;
        }
        else
        {
            t.ZonesWrong++;
            t.Error($"Zone ({zx}, {zz}): {bad} of {got.Length} heights differ");
        }
    }
}
