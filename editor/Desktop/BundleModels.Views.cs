using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>Meshes in the view's space (BundleModels.Views.cs): matrices and vertices, and a mesh by id.</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// Unity's matrix (row-major, column vectors, left-handed) in the view's space (z mirrored on both
    /// sides) as System.Numerics keeps it (row vectors: the transpose).
    /// </summary>
    internal static Matrix4x4 ViewMatrix(float[] u)
    {
        float T(int r, int c) => u[r * 4 + c] * (r == 2 ? -1 : 1) * (c == 2 ? -1 : 1);
        return new Matrix4x4(
            T(0, 0), T(1, 0), T(2, 0), T(3, 0),
            T(0, 1), T(1, 1), T(2, 1), T(3, 1),
            T(0, 2), T(1, 2), T(2, 2), T(3, 2),
            T(0, 3), T(1, 3), T(2, 3), T(3, 3));
    }

    /// <summary>A mesh by its id, in the view's space (see <see cref="ViewMesh"/>); null when it cannot be read.</summary>
    public ModelStore.MeshData? Mesh(string id)
    {
        if (id.StartsWith(Primitive))
        {
            return PrimitiveMesh(id[1..]) is { } own ? ViewMesh(own) with { BothWays = true } : null;
        }
        if (Source(id) is not var (game, key) || GameBundles.Where.FromKey(key) is not { } at || game.LoadMesh(at) is not { } m)
        {
            return null;
        }
        return ViewMesh(m);
    }

    /// <summary>Interleaved position, normal, uv (8 floats a vertex), z mirrored; indices as Unity has them.</summary>
    /// <remarks>A mesh without normals gets straight up ones; without uv, zeros.</remarks>
    internal static ModelStore.MeshData ViewMesh(GameBundles.Mesh m)
    {
        int n = m.VertexCount;
        float[] v = new float[n * 8];
        for (int i = 0; i < n; i++)
        {
            v[i * 8] = m.Positions[i * 3];
            v[i * 8 + 1] = m.Positions[i * 3 + 1];
            v[i * 8 + 2] = -m.Positions[i * 3 + 2];
            if (m.Normals != null)
            {
                v[i * 8 + 3] = m.Normals[i * 3];
                v[i * 8 + 4] = m.Normals[i * 3 + 1];
                v[i * 8 + 5] = -m.Normals[i * 3 + 2];
            }
            else
            {
                v[i * 8 + 4] = 1;
            }
            if (m.Uv != null)
            {
                v[i * 8 + 6] = m.Uv[i * 2];
                v[i * 8 + 7] = m.Uv[i * 2 + 1];
            }
        }
        uint[][] subs = [.. m.Submeshes.Select(s => s.Select(i => (uint)i).ToArray())];
        return new ModelStore.MeshData(v, subs);
    }
}
