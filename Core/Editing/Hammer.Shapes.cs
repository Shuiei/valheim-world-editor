using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    // ---- Raycasts.
    /// <summary>
    /// A ray against a collider, in the piece's frame: the distance along d (unit length) and the surface's
    /// normal; null when it misses.
    /// </summary>
    internal static (float, Vector3)? RayShape(Vector3 o, Vector3 d, Shape s) => s switch
    {
        Box b => RayBox(o, d, b),
        Sphere sp => RaySphere(o, d, sp.C, sp.R),
        Capsule c => RayCapsule(o, d, c),
        Mesh m => RayMesh(o, d, m),
        _ => null,
    };

    /// <summary>A ray against a turned box (slab test); null when it misses or starts inside.</summary>
    private static (float, Vector3)? RayBox(Vector3 o, Vector3 d, Box b)
    {
        var inv = Quaternion.Inverse(b.Q);
        var lo = Vector3.Transform(o - b.C, inv);
        var ld = Vector3.Transform(d, inv);
        float tmin = float.MinValue, tmax = float.MaxValue;
        Vector3 n = Vector3.Zero;
        for (int k = 0; k < 3; k++)
        {
            float oo = k == 0 ? lo.X : k == 1 ? lo.Y : lo.Z, dd = k == 0 ? ld.X : k == 1 ? ld.Y : ld.Z, h = k == 0 ? b.H.X : k == 1 ? b.H.Y : b.H.Z;
            if (MathF.Abs(dd) < 1e-9f)
            {
                if (oo < -h || oo > h)
                {
                    return null;
                }
                continue;
            }
            float t1 = (-h - oo) / dd, t2 = (h - oo) / dd;
            float sign = -1;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                sign = 1;
            }
            if (t1 > tmin)
            {
                tmin = t1;
                n = k == 0 ? new Vector3(sign, 0, 0) : k == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);
            }
            tmax = MathF.Min(tmax, t2);
            if (tmin > tmax)
            {
                return null;
            }
        }
        return tmin < 0 ? null : (tmin, Vector3.Transform(n, b.Q));
    }

    /// <summary>A ray against a sphere; null when it misses or starts inside.</summary>
    private static (float, Vector3)? RaySphere(Vector3 o, Vector3 d, Vector3 c, float r)
    {
        var m = o - c;
        float b = Vector3.Dot(m, d), cc = Vector3.Dot(m, m) - r * r;
        if (cc > 0 && b > 0)
        {
            return null;
        }
        float disc = b * b - cc;
        if (disc < 0)
        {
            return null;
        }
        float t = -b - MathF.Sqrt(disc);
        if (t < 0)
        {
            return null;
        }
        return (t, Vector3.Normalize(o + d * t - c));
    }

    /// <summary>
    /// A ray against a capsule, marched up to 64 steps or 1000 m; null when it misses, starts inside (as for a sphere
    /// or a box) or reaches a capsule of no width on its axis (no normal there).
    /// </summary>
    private static (float, Vector3)? RayCapsule(Vector3 o, Vector3 d, Capsule c)
    {
        // Marched: close enough for a cursor, and capsules are rare among pieces.
        float t = 0;
        for (int i = 0; i < 64 && t < 1000; i++)
        {
            var p = o + d * t;
            var q = ClosestCapsule(c with { R = 0 }, p);
            float dist = Vector3.Distance(p, q) - c.R;
            if (dist < 1e-3f)
            {
                return i == 0 && dist < 0 || (p - q).LengthSquared() < 1e-12f ? null : (t, Vector3.Normalize(p - q));
            }
            t += dist;
        }
        return null;
    }

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
