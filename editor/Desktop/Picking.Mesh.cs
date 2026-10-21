using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>The triangle and mesh hits of the picking: where a ray meets a triangle, and a whole mesh.</summary>
public static partial class Picking
{
    /// <summary>
    /// Where the ray meets a triangle (distance along it, at least 0.1 mm), or null; Möller–Trumbore, both sides
    /// (meshes are picked from whichever side they are seen, and the game's rooms from inside and out).
    /// </summary>
    public static float? HitTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-9f)
        {
            return null;
        }
        float inv = 1f / det;
        Vector3 s = o - a;
        float u = Vector3.Dot(s, p) * inv;
        if (u is < 0 or > 1)
        {
            return null;
        }
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, q) * inv;
        if (v < 0 || u + v > 1)
        {
            return null;
        }
        float t = Vector3.Dot(e2, q) * inv;
        return t > 1e-4f ? t : null;
    }

    /// <summary>
    /// How far along the ray (d normalised) a mesh placed by a matrix is first met, or null: the ray is taken into
    /// the mesh's own frame, tried against its box, then against every triangle.
    /// </summary>
    /// <param name="o">The ray's origin.</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="positions">The mesh's vertex positions, three floats each.</param>
    /// <param name="indices">The triangles to try, three vertex indices each.</param>
    /// <param name="bounds">The mesh's box, in its own frame.</param>
    /// <param name="m">Where the mesh is placed (its frame into the ray's).</param>
    /// <param name="maxY">
    /// The height (y in the ray's frame) above which the mesh is cut away, as the Workshop's cut hides what is drawn
    /// there: a triangle met above it does not count, and the nearest one met below it is the hit.
    /// </param>
    public static float? HitMesh(Vector3 o, Vector3 d, float[] positions, uint[] indices, (Vector3 Min, Vector3 Max) bounds, Matrix4x4 m, float maxY = float.MaxValue)
    {
        if (!Matrix4x4.Invert(m, out var inv))
        {
            return null;
        }
        var lo = Vector3.Transform(o, inv);
        var ld = Vector3.TransformNormal(d, inv);
        if (HitBox(lo, ld, bounds.Min - new Vector3(0.01f), bounds.Max + new Vector3(0.01f)) == null)
        {
            return null;
        }
        // The distances are in the ray's frame (the mesh's frame may be scaled). Null until a triangle is met.
        float? best = null;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            if (HitTriangle(lo, ld, At(indices[i]), At(indices[i + 1]), At(indices[i + 2])) is not float t)
            {
                continue;
            }
            var hit = Vector3.Transform(lo + ld * t, m);
            if (hit.Y > maxY)
            {
                continue;
            }
            float distance = Vector3.Distance(o, hit);
            if (best is not { } b || distance < b)
            {
                best = distance;
            }
        }
        return best;

        Vector3 At(uint i) => new(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
    }
}
