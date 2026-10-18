namespace WorldEditor.Tests.Unity;

/// <summary>
/// The packing of a compressed mesh (Unity's mesh compression: numbers in bits), for <see cref="TestMesh"/>.
/// </summary>
public sealed partial class TestMesh
{
    /// <summary>
    /// Numbers packed as Unity packs them: each a step of their range (lowest to highest) in <c>CompressedBits</c>.
    /// </summary>
    private Fields PackFloats(float[] values)
    {
        float start = values.Length == 0 ? 0 : values.Min(), range = values.Length == 0 ? 0 : values.Max() - start;
        uint max = (1u << CompressedBits) - 1;
        var steps = values.Select(v => range > 0 ? (uint)Math.Round((v - start) / range * max) : 0u).ToList();
        return new Fields { ["m_NumItems"] = (uint)values.Length, ["m_Range"] = range, ["m_Start"] = start, ["m_Data"] = Bits(steps, CompressedBits), ["m_BitSize"] = CompressedBits };
    }

    /// <summary>
    /// Integers packed in as few bits as the largest needs.
    /// </summary>
    private static Fields PackInts(IReadOnlyList<uint> values)
    {
        int bits = Math.Max(1, 32 - System.Numerics.BitOperations.LeadingZeroCount(values.DefaultIfEmpty(0u).Max()));
        return new Fields { ["m_NumItems"] = (uint)values.Count, ["m_Data"] = Bits(values, bits), ["m_BitSize"] = bits };
    }

    /// <summary>
    /// Numbers of so many bits each, one after the other, the low bits first (Unity's PackedBitVector).
    /// </summary>
    private static byte[] Bits(IEnumerable<uint> values, int bits)
    {
        var list = values.ToList();
        byte[] data = new byte[(list.Count * bits + 7) / 8];
        int at = 0;
        foreach (uint v in list)
        {
            for (int b = 0; b < bits; b++, at++)
            {
                if ((v >> b & 1) != 0)
                {
                    data[at >> 3] |= (byte)(1 << (at & 7));
                }
            }
        }
        return data;
    }

    /// <summary>
    /// The compressed mesh: positions, uv, normals' x and y with their z's signs (1: positive), the indices.
    /// </summary>
    private Fields CompressedMesh(IEnumerable<int> indices)
    {
        var c = new Fields { ["m_Vertices"] = PackFloats(Positions), ["m_Triangles"] = PackInts([.. indices.Select(i => (uint)i)]) };
        if (Uv != null)
        {
            c["m_UV"] = PackFloats(Uv);
        }
        if (Normals != null)
        {
            int n = Normals.Length / 3;
            c["m_Normals"] = PackFloats([.. Enumerable.Range(0, n).SelectMany(v => new[] { Normals[v * 3], Normals[v * 3 + 1] })]);
            c["m_NormalSigns"] = PackInts([.. Enumerable.Range(0, n).Select(v => Normals[v * 3 + 2] >= 0 ? 1u : 0u)]);
        }
        return c;
    }
}
