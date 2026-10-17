using System.Numerics;

namespace TerrainEditor.Desktop;

// A piece's two ends (where it starts and ends along its length) and the rotation of a vector.
public sealed partial class PlaceTool
{
    /// <summary>
    /// End to end: a piece's two ends are the middles of the faces along its longer side, from its snap points or, for
    /// kinds without any, from its model's box.
    /// </summary>
    /// <param name="A">The start end, in the piece's own frame, at its lowest height.</param>
    /// <param name="B">The far end, likewise.</param>
    /// <param name="Len">The distance between the ends along the ground, in metres.</param>
    /// <param name="H">How tall it is (highest less lowest point), for stacking layers.</param>
    /// <param name="Heading">The direction from A to B in its own frame, in radians (Unity yaw: clockwise from
    /// +z).</param>
    public sealed record Ends(Vector3 A, Vector3 B, float Len, float H, float Heading);

    /// <summary>
    /// The ends of a set of points (snap points or box corners) given as separate x, y and z lists; null when shorter
    /// than 5 cm.
    /// </summary>
    private static Ends? EndsFrom(float[] xs, float[] ys, float[] zs)
    {
        float x0 = xs.Min(), x1 = xs.Max(), z0 = zs.Min(), z1 = zs.Max(), y = ys.Min();
        bool alongX = x1 - x0 >= z1 - z0 - 1e-3f;
        float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        var a = alongX ? new Vector3(x0, y, cz) : new Vector3(cx, y, z0);
        var b = alongX ? new Vector3(x1, y, cz) : new Vector3(cx, y, z1);
        float len = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        return len > 0.05f ? new Ends(a, b, len, ys.Max() - y, MathF.Atan2(b.X - a.X, b.Z - a.Z)) : null;
    }

    /// <summary>A kind's ends, from its snap points or else its model box; null while neither is known.</summary>
    public Ends? EndsOf(string name)
    {
        float[][] sp = SnapsOf(name);
        if (sp.Length >= 2 && EndsFrom([.. sp.Select(p => p[0])], [.. sp.Select(p => p[1])], [.. sp.Select(p => p[2])]) is { } e)
        {
            return e;
        }
        return ModelBox(name) is var (lo, hi) ? EndsFrom([lo.X, hi.X], [lo.Y, hi.Y], [lo.Z, hi.Z]) : null;
    }

    /// <summary>Unity's rotation (Euler degrees: z, then x, then y) of a local point.</summary>
    public static Vector3 RotU(Vector3 p, Vector3 r)
    {
        const float D = MathF.PI / 180;
        float cz = MathF.Cos(r.Z * D), sz = MathF.Sin(r.Z * D), cx = MathF.Cos(r.X * D), sx = MathF.Sin(r.X * D), cy = MathF.Cos(r.Y * D), sy = MathF.Sin(r.Y * D);
        float x = p.X, y = p.Y, z = p.Z;
        (x, y) = (x * cz - y * sz, x * sz + y * cz);
        (y, z) = (y * cx - z * sx, y * sx + z * cx);
        return new Vector3(x * cy + z * sy, y, -x * sy + z * cy);
    }
}
