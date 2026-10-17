using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Unpacking packed values of a mesh or shader: floats, integers and their bit streams.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A PackedBitVector of numbers: each m_BitSize bits (little end first) a step of m_Range from m_Start.
    /// </summary>
    private static float[] UnpackFloats(AssetTypeValueField v)
    {
        int bits = v["m_BitSize"].AsByte;
        float range = v["m_Range"].AsFloat, start = v["m_Start"].AsFloat;
        int[] raw = Unpack(v, bits);
        float max = bits >= 32 ? uint.MaxValue : (1u << bits) - 1;
        float[] res = new float[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            res[i] = max > 0 ? start + range * ((uint)raw[i] / max) : start;
        }
        return res;
    }

    /// <summary>A PackedBitVector of integers (each m_BitSize bits, little end first).</summary>
    private static int[] UnpackInts(AssetTypeValueField v) => Unpack(v, v["m_BitSize"].AsByte);

    /// <summary>The m_NumItems values of <paramref name="bits"/> bits each packed in m_Data, as Unity packs them.</summary>
    private static int[] Unpack(AssetTypeValueField v, int bits)
    {
        int count = (int)v["m_NumItems"].AsUInt;
        byte[] data = v["m_Data.Array"].AsByteArray;
        if (count <= 0 || bits is <= 0 or > 32 || (long)count * bits > (long)data.Length * 8)
        {
            return [];
        }
        int[] res = new int[count];
        int index = 0, bit = 0;
        for (int i = 0; i < count; i++)
        {
            uint x = 0;
            int got = 0;
            while (got < bits)
            {
                x |= (uint)(data[index] >> bit) << got;
                int take = Math.Min(bits - got, 8 - bit);
                bit += take;
                got += take;
                if (bit == 8)
                {
                    index++;
                    bit = 0;
                }
            }
            res[i] = (int)(bits == 32 ? x : x & ((1u << bits) - 1));
        }
        return res;
    }

    /// <summary>
    /// The numbers of a field: itself, or those of its array (nested arrays flattened).
    /// </summary>
    private static IEnumerable<uint> Numbers(AssetTypeValueField f)
    {
        if (!f["Array"].IsDummy)
        {
            foreach (var c in f["Array"].Children)
            {
                foreach (uint n in Numbers(c))
                {
                    yield return n;
                }
            }
        }
        else if (f.TemplateField.IsArray)
        {
            foreach (var c in f.Children)
            {
                foreach (uint n in Numbers(c))
                {
                    yield return n;
                }
            }
        }
        else
        {
            yield return f.AsUInt;
        }
    }
}
