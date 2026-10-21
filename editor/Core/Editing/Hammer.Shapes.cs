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
}
