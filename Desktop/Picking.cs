using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// Finding what is under the mouse: a ray from the camera through the pointer, against the objects' boxes and the
/// ground. All in view space (see WorldScene).
/// </summary>
public static class Picking
{
    /// <summary>
    /// The ray through a point of the view (nx, ny from -1 to 1, y up) for the camera's view × projection.
    /// </summary>
    /// <returns>The ray's start on the near plane and its unit direction.</returns>
    public static (Vector3 Origin, Vector3 Dir) Ray(Matrix4x4 viewProj, float nx, float ny)
    {
        Matrix4x4.Invert(viewProj, out var inv);
        Vector3 Unproject(float z)
        {
            var p = Vector4.Transform(new Vector4(nx, ny, z, 1), inv);
            return new Vector3(p.X, p.Y, p.Z) / p.W;
        }
        var near = Unproject(-1);
        return (near, Vector3.Normalize(Unproject(1) - near));
    }

    /// <summary>Where the ray enters a box (distance along it, 0 when it starts inside), or null.</summary>
    /// <param name="o">The ray's origin.</param>
    /// <param name="d">The ray's direction.</param>
    /// <param name="min">The box's lowest corner.</param>
    /// <param name="max">The box's highest corner.</param>
    /// <param name="maxY">
    /// The height above which the box is cut away, as the Workshop's cut hides what is drawn there: a ray entering
    /// above it does not meet the box (the cut leaves it open, with no face at the cut, and its inside faces are not
    /// drawn).
    /// </param>
    public static float? HitBox(Vector3 o, Vector3 d, Vector3 min, Vector3 max, float maxY = float.MaxValue)
    {
        float t0 = 0, t1 = float.MaxValue;
        for (int a = 0; a < 3; a++)
        {
            float oa = a == 0 ? o.X : a == 1 ? o.Y : o.Z, da = a == 0 ? d.X : a == 1 ? d.Y : d.Z;
            float lo = a == 0 ? min.X : a == 1 ? min.Y : min.Z, hi = a == 0 ? max.X : a == 1 ? max.Y : max.Z;
            if (MathF.Abs(da) < 1e-9f)
            {
                if (oa < lo || oa > hi)
                {
                    return null;
                }
                continue;
            }
            float ta = (lo - oa) / da, tb = (hi - oa) / da;
            if (ta > tb)
            {
                (ta, tb) = (tb, ta);
            }
            t0 = MathF.Max(t0, ta);
            t1 = MathF.Min(t1, tb);
            if (t0 > t1)
            {
                return null;
            }
        }
        return o.Y + d.Y * t0 > maxY ? null : t0;
    }

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

    /// <summary>The box around a box moved by a matrix (its eight corners).</summary>
    public static (Vector3 Min, Vector3 Max) Transform(Vector3 min, Vector3 max, Matrix4x4 m)
    {
        Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var c = Vector3.Transform(new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z), m);
            lo = Vector3.Min(lo, c);
            hi = Vector3.Max(hi, c);
        }
        return (lo, hi);
    }

    /// <summary>
    /// Where the ray meets the ground (distance), stepping half a metre then narrowing down, or null. Only the area's
    /// ground: outside it, where HeightAt is very low, the ray never hits (it hit 1000 m down past the edge, a point
    /// the tools then took).
    /// </summary>
    /// <param name="s">The scene whose ground to test.</param>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's unit direction.</param>
    /// <param name="maxDist">How far along the ray to look, in metres.</param>
    public static float? HitGround(WorldScene s, Vector3 o, Vector3 d, float maxDist = 4000)
    {
        // The height map is crossed wherever it is; where a mod's extension cuts the ground away, the crossing is
        // no hit (the ray goes on, into the hole), and the next crossing counts.
        float Below(float t)
        {
            var p = o + d * t;
            return Inside(s, p.X, p.Z) ? p.Y - HeightAt(s, p.X, p.Z) : float.PositiveInfinity;
        }
        float prev = 0;
        float above = Below(0);
        for (float t = 0.5f; t <= maxDist; t += 0.5f)
        {
            float b = Below(t);
            if (b <= 0 && above > 0)
            {
                float lo = prev, hi = t;
                for (int i = 0; i < 20; i++)
                {
                    float mid = (lo + hi) / 2;
                    if (Below(mid) > 0)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid;
                    }
                }
                float hit = (lo + hi) / 2;
                var at = o + d * hit;
                if (!CutAt(s, at.X, at.Z))
                {
                    return hit;
                }
            }
            prev = t;
            above = b;
        }
        return null;
    }

    /// <summary>
    /// Whether the ground is cut away (left undrawn by a mod's extension) under a view-space point (x, z): the cut
    /// values blended over the terrain's triangle there (each grid cell is two, split from its east to its south
    /// point, as BuildTerrain makes them), past a half, as the terrain shader leaves them out.
    /// </summary>
    public static bool CutAt(WorldScene s, float x, float z)
    {
        if (s.Cut.Length != s.W * s.H)
        {
            return false;
        }
        if (CellAt(s, x, z) is not var (x0, z0, tx, tz))
        {
            return false;
        }
        float C(int a, int b) => s.Cut[b * s.W + a];
        float value = tx + tz <= 1
            ? C(x0, z0) * (1 - tx - tz) + C(x0 + 1, z0) * tx + C(x0, z0 + 1) * tz
            : C(x0 + 1, z0 + 1) * (tx + tz - 1) + C(x0 + 1, z0) * (1 - tz) + C(x0, z0 + 1) * (1 - tx);
        return value > 0.5f;
    }

    /// <summary>
    /// The grid cell under a view-space point: its corner (X0, Z0) and the point's place inside it (Tx, Tz from
    /// 0 to 1), or null when the point is outside the area's ground.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="x">The view-space x.</param>
    /// <param name="z">The view-space z.</param>
    private static (int X0, int Z0, float Tx, float Tz)? CellAt(WorldScene s, float x, float z)
    {
        float gx = x + (s.W - 1) / 2f, gz = -z + (s.H - 1) / 2f;
        if (gx < 0 || gz < 0 || gx > s.W - 1 || gz > s.H - 1)
        {
            return null;
        }
        int x0 = Math.Min((int)gx, s.W - 2), z0 = Math.Min((int)gz, s.H - 2);
        return (x0, z0, gx - x0, gz - z0);
    }

    /// <summary>Whether a view-space point (x, z) is over the area's ground.</summary>
    public static bool Inside(WorldScene s, float x, float z)
    {
        float gx = x + (s.W - 1) / 2f, gz = -z + (s.H - 1) / 2f;
        return gx >= 0 && gz >= 0 && gx <= s.W - 1 && gz <= s.H - 1;
    }

    /// <summary>
    /// The ground's height under a view-space point (between the grid points), or -1000 outside the area.
    /// </summary>
    public static float HeightAt(WorldScene s, float x, float z)
    {
        if (CellAt(s, x, z) is not var (x0, z0, tx, tz))
        {
            return -1000;
        }
        float H(int a, int b) => s.Heights[b * s.W + a];
        return (H(x0, z0) * (1 - tx) + H(x0 + 1, z0) * tx) * (1 - tz) + (H(x0, z0 + 1) * (1 - tx) + H(x0 + 1, z0 + 1) * tx) * tz;
    }
}
