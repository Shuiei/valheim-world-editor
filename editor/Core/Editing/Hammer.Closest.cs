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
}
