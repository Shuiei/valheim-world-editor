using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>A piece's scale (0 or less: as made, 1).</summary>
    private static float ScaleOf(Piece p) => p.Scale > 0 ? p.Scale : 1f;

    /// <summary>A piece's centre of mass in the world: its own, scaled and turned as the piece is.</summary>
    private static Vector3 Com(Data d, Piece p) => p.Position + Vector3.Transform(d.Com * ScaleOf(p), p.Rotation);

    /// <summary>A piece's centre of mass in the world; null for a piece with no support rules.</summary>
    internal static Vector3? CentreOfMass(Piece p) => _pieces.GetValueOrDefault(p.Prefab) is { } d ? Com(d, p) : null;

    /// <summary>A piece's collider in the world (its axis-aligned bounds for a non-box), grown by grow metres on every side.</summary>
    private static Obb World(Box b, Piece p, float grow)
    {
        float s = ScaleOf(p);
        var q = p.Rotation * b.Q;
        var c = p.Position + Vector3.Transform(b.C * s, p.Rotation);
        var obb = new Obb(c, Vector3.Transform(Vector3.UnitX, q), Vector3.Transform(Vector3.UnitY, q), Vector3.Transform(Vector3.UnitZ, q), b.H * s);
        if (b.Aabb)
        {
            // Collider.bounds: the turned box's axis-aligned bounds.
            Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
            foreach (var corner in obb.Corners())
            {
                lo = Vector3.Min(lo, corner);
                hi = Vector3.Max(hi, corner);
            }
            obb = new Obb((lo + hi) / 2, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, (hi - lo) / 2);
        }
        return obb with { H = obb.H + new Vector3(grow) };
    }

    /// <summary>Separating axis test of two oriented boxes.</summary>
    private static bool Overlap(in Obb a, in Obb b)
    {
        var d = b.C - a.C;
        if (d.Length() > a.Radius + b.Radius)
        {
            return false;
        }
        Span<Vector3> axes = stackalloc Vector3[15];
        axes[0] = a.X; axes[1] = a.Y; axes[2] = a.Z; axes[3] = b.X; axes[4] = b.Y; axes[5] = b.Z;
        int n = 6;
        foreach (var u in axes[..3])
        {
            foreach (var v in axes[3..6])
            {
                var c = Vector3.Cross(u, v);
                if (c.LengthSquared() > 1e-8f)
                {
                    axes[n++] = Vector3.Normalize(c);
                }
            }
        }
        for (int i = 0; i < n; i++)
        {
            var ax = axes[i];
            float ra = a.H.X * MathF.Abs(Vector3.Dot(a.X, ax)) + a.H.Y * MathF.Abs(Vector3.Dot(a.Y, ax)) + a.H.Z * MathF.Abs(Vector3.Dot(a.Z, ax));
            float rb = b.H.X * MathF.Abs(Vector3.Dot(b.X, ax)) + b.H.Y * MathF.Abs(Vector3.Dot(b.Y, ax)) + b.H.Z * MathF.Abs(Vector3.Dot(b.Z, ax));
            if (MathF.Abs(Vector3.Dot(d, ax)) > ra + rb)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Side of the grid cells pieces are sorted into for the touch test (metres).</summary>
    private const float TouchCell = 4f;

    /// <summary>A piece spanning more grid cells than this is tried against every other piece instead.</summary>
    private const long MaxTouchCells = 4096;
}
