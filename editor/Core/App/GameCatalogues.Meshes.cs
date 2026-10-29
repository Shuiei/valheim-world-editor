using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// The meshes of the game data: vertices and triangles, as UnityPy reads them.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// A mesh's vertices (x, y, z each) and each submesh's triangles, as UnityPy reads them (the
    /// submesh's base vertex not added); null when it has no vertex data, or one not read here.
    /// </summary>
    private static (float[] Vertices, List<int[]> Triangles)? MeshData(Bundle b, AssetTypeValueField m)
    {
        var vd = m["m_VertexData"];
        var layout = GameBundles.VertexLayout.Of(vd);
        int n = layout.Count;
        byte[] data = vd["m_DataSize"].AsByteArray;
        if (Has(m, "m_StreamData") && m["m_StreamData.path"].AsString.Length > 0)
        {
            data = b.StreamBytes(m["m_StreamData"]);
        }
        if (n == 0 || data.Length == 0)
        {
            return null;
        }
        // Positions as floats or halves only; a channel of a format not read here and the layout would be a guess.
        var channels = layout.Channels;
        if (channels.Count == 0 || channels[0].Dim < 3 || channels[0].Format > 1 || channels.Any(c => c.Dim > 0 && !GameBundles.VertexLayout.Known(c.Format)))
        {
            return null;
        }
        // The renderer's layout and bounds check (GameBundles): a vertex past the data stays at 0, nothing throws.
        float[] vertices = layout.Read(data, 0, 3)!;
        byte[] ib = m["m_IndexBuffer.Array"].AsByteArray;
        bool wide = m["m_IndexFormat"].AsInt == 1;
        int count = ib.Length / (wide ? 4 : 2);
        int[] indices = new int[count];
        for (int k = 0; k < count; k++)
        {
            indices[k] = wide ? (int)BitConverter.ToUInt32(ib, 4 * k) : BitConverter.ToUInt16(ib, 2 * k);
        }
        var triangles = new List<int[]>();
        foreach (var sm in m["m_SubMeshes.Array"].Children)
        {
            // Triangles only (topology 0): the game's colliders and rocks have nothing else.
            if (sm["topology"].AsInt != 0)
            {
                return null;
            }
            int first = (int)sm["firstByte"].AsUInt / (wide ? 4 : 2), end = Math.Min(first + (int)sm["indexCount"].AsUInt, count);
            triangles.Add(first < end ? indices[first..end] : []);
        }
        return (vertices, triangles);
    }
}
