using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>The angle between two rotations (degrees, 0 to 180).</summary>
    private static float AngleDegrees(Quaternion a, Quaternion b) => MathF.Acos(MathF.Min(1, MathF.Abs(Quaternion.Dot(a, b)))) * 2 * 180 / MathF.PI;

    // ---- Collider.ClosestPoint.
    /// <summary>Collider.ClosestPoint, in the piece's frame (p inside: p itself).</summary>
    internal static Vector3 Closest(Shape s, Vector3 p) => s switch
    {
        Box b => ClosestBox(b, p),
        Sphere sp => Vector3.Distance(p, sp.C) <= sp.R ? p : sp.C + Vector3.Normalize(p - sp.C) * sp.R,
        Capsule c => ClosestCapsule(c, p),
        Mesh m => ClosestHull(m, p),
        _ => p,
    };

    /// <summary>The point of a box nearest p (p itself when inside).</summary>
    private static Vector3 ClosestBox(Box b, Vector3 p)
    {
        var l = Vector3.Transform(p - b.C, Quaternion.Inverse(b.Q));
        var c = Vector3.Clamp(l, -b.H, b.H);
        return b.C + Vector3.Transform(c, b.Q);
    }

    /// <summary>The point of a capsule nearest p (p itself when inside).</summary>
    private static Vector3 ClosestCapsule(Capsule c, Vector3 p)
    {
        var seg = c.B - c.A;
        float t = seg.LengthSquared() < 1e-9f ? 0 : Math.Clamp(Vector3.Dot(p - c.A, seg) / seg.LengthSquared(), 0, 1);
        var axis = c.A + seg * t;
        return Vector3.Distance(p, axis) <= c.R ? p : axis + Vector3.Normalize(p - axis) * c.R;
    }

    /// <summary>A convex mesh: the point itself when inside, else the nearest point of its triangles.</summary>
    private static Vector3 ClosestHull(Mesh m, Vector3 p)
    {
        Vector3 best = p;
        float bd = float.MaxValue;
        bool inside = true;
        var centre = m.V.Aggregate(Vector3.Zero, (a, v) => a + v) / Math.Max(1, m.V.Length);
        for (int i = 0; i + 2 < m.I.Length; i += 3)
        {
            Vector3 a = m.V[m.I[i]], b = m.V[m.I[i + 1]], c = m.V[m.I[i + 2]];
            var q = ClosestTriangle(p, a, b, c);
            float d = Vector3.DistanceSquared(p, q);
            if (d < bd)
            {
                bd = d;
                best = q;
            }
            // Outside when beyond any face (its normal turned away from the middle).
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, centre - a) > 0)
            {
                n = -n;
            }
            if (Vector3.Dot(n, p - a) > 1e-5f)
            {
                inside = false;
            }
        }
        return inside ? p : best;
    }

    /// <summary>The point of triangle a, b, c nearest p (Ericson, Real-Time Collision Detection 5.1.5).</summary>
    internal static Vector3 ClosestTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0)
        {
            return a;
        }
        var bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3)
        {
            return b;
        }
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            return a + ab * (d1 / (d1 - d3));
        }
        var cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6)
        {
            return c;
        }
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            return a + ac * (d2 / (d2 - d6));
        }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            return b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
        }
        float denom = 1 / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }
}
