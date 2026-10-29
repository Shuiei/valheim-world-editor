using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>Möller–Trumbore over the triangles, both sides (Unity's mesh colliders are hit from outside).</summary>
    private static (float, Vector3)? RayMesh(Vector3 o, Vector3 d, Mesh m)
    {
        (float, Vector3)? best = null;
        for (int i = 0; i + 2 < m.I.Length; i += 3)
        {
            Vector3 a = m.V[m.I[i]], b = m.V[m.I[i + 1]], c = m.V[m.I[i + 2]];
            Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (MathF.Abs(det) < 1e-9f)
            {
                continue;
            }
            float inv = 1 / det;
            var tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv;
            if (u is < 0 or > 1)
            {
                continue;
            }
            var qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv;
            if (v < 0 || u + v > 1)
            {
                continue;
            }
            float t = Vector3.Dot(e2, qv) * inv;
            if (t >= 0 && (best == null || t < best.Value.Item1))
            {
                var n = Vector3.Normalize(Vector3.Cross(e1, e2));
                best = (t, Vector3.Dot(n, d) > 0 ? -n : n);
            }
        }
        return best;
    }
}
