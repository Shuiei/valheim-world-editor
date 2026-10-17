namespace WorldEditor.Tests.Unity;

/// <summary>
/// A made-up mesh as Unity stores it: vertex channels (position, normal, first UV) in one or two streams, each stream
/// 16-byte aligned after the one before, the channels of a vertex side by side; an index buffer of 16- or 32-bit
/// indices; submeshes as ranges of it; or all of it packed in bits (Unity's mesh compression). Written into a bundle by
/// <c>Add</c>. The packing is in TestMesh.Packing.cs, the vertex bytes in TestMesh.Vertices.cs.
/// </summary>
public sealed partial class TestMesh
{
    /// <summary>
    /// The mesh's name.
    /// </summary>
    public string Name { get; init; } = "mesh";

    /// <summary>
    /// Written with another Unity version's layout (<see cref="UnityTypes.MeshOfAnotherVersion"/>).
    /// </summary>
    public bool AnotherVersion { get; init; }

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
    /// Written with Unity's mesh compression: positions, normals (x and y, and the sign of z), uv and the indices packed
    /// in bits in m_CompressedMesh, no vertex bytes nor index buffer (the submeshes' firstByte as an index buffer's).
    /// </summary>
    public bool Compressed { get; init; }

    /// <summary>
    /// The bits per number of a compressed mesh's positions, normals and uv.
    /// </summary>
    public int CompressedBits { get; init; } = 10;

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
        var data = VertexBytes(used, streams, n);
        // Unity lists 14 channels; those not used have dimension 0.
        var channels = Enumerable.Range(0, 14).Select(i => used.FirstOrDefault(c => c.Channel == i) is { Dim: > 0 } c
            ? new Fields { ["stream"] = c.Stream, ["offset"] = offsets[i], ["format"] = (int)c.Format, ["dimension"] = c.Dim }
            : []).ToList();
        var (indices, subs) = SubmeshData();
        var values = new Fields
        {
            ["m_Name"] = Name,
            ["m_SubMeshes"] = subs,
            ["m_IndexFormat"] = Wide ? 1 : 0,
            ["m_IndexBuffer"] = Compressed ? [] : indices.ToArray(),
            ["m_VertexData"] = Compressed
                ? new Fields { ["m_VertexCount"] = (uint)n, ["m_DataSize"] = Array.Empty<byte>() }
                : new Fields { ["m_VertexCount"] = (uint)n, ["m_Channels"] = channels, ["m_DataSize"] = Streamed ? [] : data.ToArray() },
            ["m_CompressedMesh"] = Compressed ? CompressedMesh(Submeshes.SelectMany(i => i)) : null,
            ["m_LocalAABB"] = new Fields { ["m_Center"] = Fields.Vector3(Bounds.Center.X, Bounds.Center.Y, Bounds.Center.Z), ["m_Extent"] = Fields.Vector3(Bounds.Extent.X, Bounds.Extent.Y, Bounds.Extent.Z) },
        };
        if (Streamed)
        {
            values["m_StreamData"] = b.StreamData(b.Resource([.. data]), data.Count);
        }
        if (AnotherVersion)
        {
            values["m_AnotherVersionsField"] = 7;
        }
        return b.Assets.Add(UnityClass.Mesh, AnotherVersion ? UnityTypes.MeshOfAnotherVersion : UnityTypes.Mesh, values, pathId: pathId);
    }
}
