using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// Where the ground is left undrawn (ValheimWorldEditor.Sdk.GroundCut): outlines marked on the area's grid, one value
/// per grid point (1 inside a cut, else 0), for the terrain shader to leave those points' triangles out.
/// </summary>
public static class GroundCuts
{
    /// <summary>
    /// Marks the grid points inside an outline (even-odd: a point is inside when a ray from it crosses the outline an
    /// odd number of times); points on the outline count as inside.
    /// </summary>
    /// <param name="cut">The grid's values, <paramref name="w"/> by <paramref name="h"/>, row by row.</param>
    /// <param name="w">The grid's width, in points.</param>
    /// <param name="h">The grid's height, in points.</param>
    /// <param name="outline">The outline, in grid coordinates (x across, z down the rows); fewer than three points mark nothing.</param>
    public static void Mark(float[] cut, int w, int h, IReadOnlyList<Vector2> outline)
    {
        if (outline.Count < 3 || cut.Length < w * h)
        {
            return;
        }
        float minX = outline.Min(p => p.X), maxX = outline.Max(p => p.X), minZ = outline.Min(p => p.Y), maxZ = outline.Max(p => p.Y);
        int x0 = Math.Max(0, (int)MathF.Ceiling(minX)), x1 = Math.Min(w - 1, (int)MathF.Floor(maxX));
        int z0 = Math.Max(0, (int)MathF.Ceiling(minZ)), z1 = Math.Min(h - 1, (int)MathF.Floor(maxZ));
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (Inside(outline, gx, gz))
                {
                    cut[gz * w + gx] = 1;
                }
            }
        }
    }

    /// <summary>Whether a point is inside an outline (even-odd), or on it.</summary>
    public static bool Inside(IReadOnlyList<Vector2> outline, float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            var a = outline[i];
            var b = outline[j];
            if (OnSegment(a, b, x, z))
            {
                return true;
            }
            if (a.Y > z != b.Y > z && x < (b.X - a.X) * (z - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Whether a point lies on a segment (within a millimetre).</summary>
    private static bool OnSegment(Vector2 a, Vector2 b, float x, float z)
    {
        const float Eps = 1e-3f;
        if (x < MathF.Min(a.X, b.X) - Eps || x > MathF.Max(a.X, b.X) + Eps || z < MathF.Min(a.Y, b.Y) - Eps || z > MathF.Max(a.Y, b.Y) + Eps)
        {
            return false;
        }
        float cross = (b.X - a.X) * (z - a.Y) - (b.Y - a.Y) * (x - a.X);
        return MathF.Abs(cross) <= Eps * MathF.Max(1, Vector2.Distance(a, b));
    }
}
