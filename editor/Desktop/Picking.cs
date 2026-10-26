using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// Finding what is under the mouse: a ray from the camera through the pointer, against the objects' boxes and the
/// ground. All in view space (see WorldScene).
/// </summary>
public static partial class Picking
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
            float oa = Component(o, a), da = Component(d, a);
            float lo = Component(min, a), hi = Component(max, a);
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

    /// <summary>One axis of a vector: 0 for X, 1 for Y, anything else for Z.</summary>
    internal static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

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
}
