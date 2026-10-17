using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>
    /// A sphere holding every box's own sphere (<see cref="Obb.Radius"/> round its middle), a little larger for
    /// rounding: boxes of two pieces whose spheres do not meet cannot overlap.
    /// </summary>
    internal static (Vector3 C, float R) Sphere(Obb[] boxes)
    {
        var c = Vector3.Zero;
        foreach (var b in boxes)
        {
            c += b.C;
        }
        c /= boxes.Length;
        float r = 0;
        foreach (var b in boxes)
        {
            r = MathF.Max(r, Vector3.Distance(c, b.C) + b.Radius);
        }
        return (c, r * 1.001f + 0.01f);
    }

    /// <summary>Calls <paramref name="each"/> for every grid cell a sphere's bounds reach; false (none called) when they reach too many.</summary>
    internal static bool InCells((Vector3 C, float R) s, Action<(int, int, int)> each)
    {
        static Vector3 Cell(Vector3 p) => new(MathF.Floor(p.X / TouchCell), MathF.Floor(p.Y / TouchCell), MathF.Floor(p.Z / TouchCell));
        var lo = Cell(s.C - new Vector3(s.R));
        var hi = Cell(s.C + new Vector3(s.R));
        var span = hi - lo + Vector3.One;
        if (!(span.X * span.Y * span.Z <= MaxTouchCells) || Vector3.Abs(lo) is not { X: < 1e8f, Y: < 1e8f, Z: < 1e8f } || Vector3.Abs(hi) is not { X: < 1e8f, Y: < 1e8f, Z: < 1e8f })
        {
            return false;
        }
        for (int x = (int)lo.X; x <= (int)hi.X; x++)
        {
            for (int y = (int)lo.Y; y <= (int)hi.Y; y++)
            {
                for (int z = (int)lo.Z; z <= (int)hi.Z; z++)
                {
                    each((x, y, z));
                }
            }
        }
        return true;
    }
}
