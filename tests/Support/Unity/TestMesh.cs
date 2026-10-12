namespace WorldEditor.Tests.Unity;

/// <summary>
/// A made-up mesh as Unity stores it: vertex channels (position, normal, first UV) in one or two streams, each stream
/// 16-byte aligned after the one before, the channels of a vertex side by side; an index buffer of 16- or 32-bit
/// indices; submeshes as ranges of it. Written into a bundle by <c>Add</c>.
/// </summary>
public sealed class TestMesh
{

    /// <summary>
    /// The mesh's name.
    /// </summary>
    public string Name { get; init; } = "mesh";

    /// <summary>
    /// Three floats per vertex.
    /// </summary>
    public required float[] Positions { get; init; }

    /// <summary>
    /// Three floats per vertex, or none.
    /// </summary>
    public float[]? Normals { get; init; }

    /// <summary>
    /// Two floats per vertex, or none.
    /// </summary>
    public float[]? Uv { get; init; }

    /// <summary>
    /// The positions' format (Float or Half).
    /// </summary>
    public VertexFormat PositionFormat { get; init; } = VertexFormat.Ieee32;

    /// <summary>
    /// The normals' format.
    /// </summary>
    public VertexFormat NormalFormat { get; init; } = VertexFormat.Ieee32;

    /// <summary>
    /// The UVs' format.
    /// </summary>
    public VertexFormat UvFormat { get; init; } = VertexFormat.Ieee32;

    /// <summary>
    /// Put the UVs in a second stream (else all in the first).
    /// </summary>
    public bool UvStream { get; init; }

    /// <summary>
    /// Each submesh's indices (as stored, before the base vertex).
    /// </summary>
    public int[][] Submeshes { get; init; } = [];

    /// <summary>
    /// Each submesh's base vertex (none: 0).
    /// </summary>
    public int[]? BaseVertex { get; init; }

    /// <summary>
    /// Each submesh's topology (none: triangles, 0).
    /// </summary>
    public int[]? Topology { get; init; }

    /// <summary>
    /// 32-bit indices (else 16-bit).
    /// </summary>
    public bool Wide { get; init; }

    /// <summary>
    /// The vertex data kept in the bundle's resource file (else in the object).
    /// </summary>
    public bool Streamed { get; init; }

    /// <summary>
    /// The mesh's bounds (centre, extent).
    /// </summary>
    public ((double X, double Y, double Z) Center, (double X, double Y, double Z) Extent) Bounds { get; init; }

    /// <summary>
    /// How many compressed vertices it says it has (more than 0: a compressed mesh).
    /// </summary>
    public uint Compressed { get; init; }

    /// <summary>
    /// The bytes per component of a format.
    /// </summary>
    private static int SizeOf(VertexFormat format) => format switch
    {
        VertexFormat.Ieee32 => 4,
        VertexFormat.Ieee16 or VertexFormat.UNorm16 or VertexFormat.SNorm16 => 2,
        _ => 1,
    };

    /// <summary>
    /// A value in a format, as bytes.
    /// </summary>
    private static byte[] Encode(float v, VertexFormat format) => format switch
    {
        VertexFormat.Ieee32 => BitConverter.GetBytes(v),
        VertexFormat.Ieee16 => BitConverter.GetBytes((Half)v),
        VertexFormat.UNorm8 => [(byte)Math.Round(v * 255)],
        VertexFormat.SNorm8 => [(byte)(sbyte)Math.Round(v * 127)],
        VertexFormat.UNorm16 => BitConverter.GetBytes((ushort)Math.Round(v * 65535)),
        VertexFormat.SNorm16 => BitConverter.GetBytes((short)Math.Round(v * 32767)),
        _ => throw new ArgumentException($"no vertex format {format}"),
    };

    /// <summary>
    /// Writes the mesh into a bundle; returns its path id.
    /// </summary>
    public long Add(TestBundle b, long? pathId = null)
    {
        int n = Positions.Length / 3;
        // The channels used: (index, stream, format, dimension, values).
        var used = new List<(int Channel, int Stream, VertexFormat Format, int Dim, float[] Values)> { (0, 0, PositionFormat, 3, Positions) };
        if (Normals != null)
        {
            used.Add((1, 0, NormalFormat, 3, Normals));
        }
        if (Uv != null)
        {
            used.Add((4, UvStream ? 1 : 0, UvFormat, 2, Uv));
        }
        int streams = used.Max(c => c.Stream) + 1;
        int[] stride = new int[streams];
        var offsets = new Dictionary<int, int>();
        foreach (var c in used)
        {
            offsets[c.Channel] = stride[c.Stream];
            stride[c.Stream] += c.Dim * SizeOf(c.Format);
        }
        var data = new List<byte>();
        for (int s = 0; s < streams; s++)
        {
            while (data.Count % 16 != 0)
            {
                data.Add(0);
            }
            for (int v = 0; v < n; v++)
            {
                foreach (var c in used.Where(c => c.Stream == s))
                {
                    for (int d = 0; d < c.Dim; d++)
                    {
                        data.AddRange(Encode(c.Values[v * c.Dim + d], c.Format));
                    }
                }
            }
        }
        // Unity lists 14 channels; those not used have dimension 0.
        var channels = Enumerable.Range(0, 14).Select(i => used.FirstOrDefault(c => c.Channel == i) is { Dim: > 0 } c
            ? new Fields { ["stream"] = c.Stream, ["offset"] = offsets[i], ["format"] = (int)c.Format, ["dimension"] = c.Dim }
            : []).ToList();
        var indices = new List<byte>();
        var subs = new List<Fields>();
        for (int i = 0; i < Submeshes.Length; i++)
        {
            subs.Add(new Fields
            {
                ["firstByte"] = (uint)indices.Count,
                ["indexCount"] = (uint)Submeshes[i].Length,
                ["topology"] = Topology?[i] ?? 0,
                ["baseVertex"] = (uint)(BaseVertex?[i] ?? 0),
            });
            foreach (int k in Submeshes[i])
            {
                indices.AddRange(Wide ? BitConverter.GetBytes((uint)k) : BitConverter.GetBytes((ushort)k));
            }
        }
        var values = new Fields
        {
            ["m_Name"] = Name,
            ["m_SubMeshes"] = subs,
            ["m_IndexFormat"] = Wide ? 1 : 0,
            ["m_IndexBuffer"] = indices.ToArray(),
            ["m_VertexData"] = new Fields { ["m_VertexCount"] = (uint)n, ["m_Channels"] = channels, ["m_DataSize"] = Streamed ? [] : data.ToArray() },
            ["m_CompressedMesh"] = new Fields { ["m_Vertices"] = new Fields { ["m_NumItems"] = Compressed } },
            ["m_LocalAABB"] = new Fields { ["m_Center"] = Fields.Vector3(Bounds.Center.X, Bounds.Center.Y, Bounds.Center.Z), ["m_Extent"] = Fields.Vector3(Bounds.Extent.X, Bounds.Extent.Y, Bounds.Extent.Z) },
        };
        if (Streamed)
        {
            values["m_StreamData"] = b.StreamData(b.Resource([.. data]), data.Count);
        }
        return b.Assets.Add(UnityClass.Mesh, UnityTypes.Mesh, values, pathId: pathId);
    }
}
