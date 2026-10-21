using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Reading a compressed mesh: its vertices and triangles from the packed data.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A compressed mesh (Unity's mesh compression: positions, normals, uv and triangles packed in bits, each value
    /// a step of its range): positions, normals (two parts and the sign of the third), the first uv channel, and the
    /// triangles cut into the submeshes as an uncompressed mesh's index buffer would be.
    /// </summary>
    private static Mesh ReadCompressedMesh(string name, AssetTypeValueField m)
    {
        var c = m["m_CompressedMesh"];
        float[] positions = UnpackFloats(c["m_Vertices"]);
        int n = positions.Length / 3;
        float[]? normals = null;
        float[] packedNormals = UnpackFloats(c["m_Normals"]);
        if (packedNormals.Length >= n * 2 && n > 0)
        {
            int[] signs = UnpackInts(c["m_NormalSigns"]);
            normals = new float[n * 3];
            for (int v = 0; v < n; v++)
            {
                float x = packedNormals[v * 2], y = packedNormals[v * 2 + 1];
                float z = MathF.Sqrt(Math.Max(0, 1 - x * x - y * y));
                if (v < signs.Length && signs[v] == 0)
                {
                    z = -z;
                }
                float len = MathF.Sqrt(x * x + y * y + z * z);
                if (len > 0)
                {
                    (x, y, z) = (x / len, y / len, z / len);
                }
                (normals[v * 3], normals[v * 3 + 1], normals[v * 3 + 2]) = (x, y, z);
            }
        }
        float[] uvs = UnpackFloats(c["m_UV"]);
        // The first channel comes first (m_UVInfo says which channels follow).
        float[]? uv = uvs.Length >= n * 2 ? uvs[..(n * 2)] : null;
        int[] triangles = UnpackInts(c["m_Triangles"]);
        bool wide = m["m_IndexFormat"].AsInt == 1;
        var subs = new List<int[]>();
        foreach (var sm in m["m_SubMeshes.Array"].Children)
        {
            long first = sm["firstByte"].AsUInt / (wide ? 4 : 2), count = sm["indexCount"].AsUInt, baseV = sm["baseVertex"].AsUInt;
            if (sm["topology"].AsInt != 0 || first + count > triangles.Length)
            {
                subs.Add([]);
                continue;
            }
            subs.Add(Triangles(count, n, k => baseV + triangles[first + k]));
        }
        return new Mesh(name, n, positions, normals, uv, [.. subs]);
    }
}
