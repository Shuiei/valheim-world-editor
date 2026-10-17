using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Loading a mesh: its vertices, triangles and submesh counts, read from the mesh object.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// How many submeshes a mesh has (0 when it cannot be read).
    /// </summary>
    private int SubmeshCount(Where mesh) => Read(mesh.Bundle, mesh.PathId)?["m_SubMeshes.Array"].Children.Count ?? 0;

    /// <summary>
    /// A mesh's vertices and triangles, null when there is no such object (a compressed mesh too: Unity's mesh
    /// compression, which some mods' bundles use).
    /// </summary>
    public Mesh? LoadMesh(Where at)
    {
        lock (_sync)
        {
            try
            {
                return Read(at.Bundle, at.PathId) is { } m ? ReadMesh(at, m) : null;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// Reads a mesh's vertex streams (positions, normals, first UV channel) and its 16- or 32-bit index buffer.
    /// </summary>
    private Mesh ReadMesh(Where at, AssetTypeValueField m)
    {
        string name = m["m_Name"].AsString;
        if (Has(m, "m_CompressedMesh") && m["m_CompressedMesh.m_Vertices.m_NumItems"].AsUInt > 0)
        {
            return ReadCompressedMesh(name, m);
        }
        var layout = VertexLayout.Of(m["m_VertexData"]);
        int n = layout.Count;
        byte[] data = m["m_VertexData.m_DataSize"].AsByteArray;
        if (data.Length == 0 && m["m_StreamData.size"].AsUInt > 0)
        {
            data = StreamBytes(at.Bundle, m["m_StreamData"]);
        }
        byte[] ib = m["m_IndexBuffer.Array"].AsByteArray;
        bool wide = m["m_IndexFormat"].AsInt == 1;
        var subs = new List<int[]>();
        foreach (var sm in m["m_SubMeshes.Array"].Children)
        {
            // In long: sizes from a broken file can pass int's range.
            long first = sm["firstByte"].AsUInt, count = sm["indexCount"].AsUInt, baseV = sm["baseVertex"].AsUInt;
            // Triangles only (0); other topologies (lines, points) draw nothing here.
            if (sm["topology"].AsInt != 0 || first + count * (wide ? 4 : 2) > ib.Length)
            {
                subs.Add([]);
                continue;
            }
            subs.Add(Triangles(count, n, k => baseV + (wide ? BitConverter.ToUInt32(ib, (int)(first + 4 * k)) : BitConverter.ToUInt16(ib, (int)(first + 2 * k)))));
        }
        return new Mesh(name, n, layout.Read(data, 0, 3) ?? new float[n * 3], layout.Read(data, 1, 3), layout.Read(data, 4, 2), [.. subs]);
    }
}
