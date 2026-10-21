namespace TerrainEditor.App;

/// <summary>
/// The triangles of a mesh, from its index buffer, per submesh.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A submesh's triangles, from its <paramref name="count"/> indices (whole triangles only), leaving out those with a
    /// corner that is not one of the mesh's <paramref name="vertices"/>: a broken mod mesh's indices can point anywhere.
    /// </summary>
    /// <param name="count">How many indices the submesh has.</param>
    /// <param name="vertices">How many vertices the mesh has.</param>
    /// <param name="corner">The vertex of the k-th index (base vertex added).</param>
    internal static int[] Triangles(long count, int vertices, Func<long, long> corner)
    {
        int[] tris = new int[count - count % 3];
        int kept = 0;
        for (long k = 0; k < tris.Length; k += 3)
        {
            long a = corner(k), b = corner(k + 1), c = corner(k + 2);
            if (a >= 0 && a < vertices && b >= 0 && b < vertices && c >= 0 && c < vertices)
            {
                (tris[kept], tris[kept + 1], tris[kept + 2]) = ((int)a, (int)b, (int)c);
                kept += 3;
            }
        }
        return kept == tris.Length ? tris : tris[..kept];
    }
}
