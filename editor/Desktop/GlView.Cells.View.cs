using System.Numerics;

namespace TerrainEditor.Desktop;

// The cells against the view: the view's six planes, whether a box shows, and growing a cell's box.
public sealed partial class GlView
{
    /// <summary>The six planes of a view and projection (row vectors), each facing into the view.</summary>
    /// <param name="vp">The view and projection.</param>
    /// <param name="planes">Where the planes go (six).</param>
    internal static void ViewPlanes(Matrix4x4 vp, Span<Vector4> planes)
    {
        // The fourth column plus or minus each of the others.
        planes[0] = new(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
        planes[1] = new(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
        planes[2] = new(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
        planes[3] = new(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
        planes[4] = new(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43);
        planes[5] = new(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);
    }

    /// <summary>
    /// Whether a box may show in a view (<see cref="ViewPlanes"/>): false only when it is wholly behind one of the
    /// planes, so a box partly in view counts as in it. An empty box (min past max) is never in view.
    /// </summary>
    /// <param name="planes">The view's six planes.</param>
    /// <param name="min">The box's low corner.</param>
    /// <param name="max">The box's high corner.</param>
    internal static bool InView(ReadOnlySpan<Vector4> planes, Vector3 min, Vector3 max)
    {
        if (min.X > max.X)
        {
            return false;
        }
        foreach (var pl in planes)
        {
            // The box's corner farthest along the plane's normal: outside when even it is behind.
            var far = new Vector3(pl.X >= 0 ? max.X : min.X, pl.Y >= 0 ? max.Y : min.Y, pl.Z >= 0 ? max.Z : min.Z);
            if (pl.X * far.X + pl.Y * far.Y + pl.Z * far.Z + pl.W < 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The box a model's instance covers: its model's box (<paramref name="box"/>) placed by
    /// <paramref name="placement"/>, or a metre round where it stands while that box is not known.
    /// </summary>
    internal static (Vector3 Min, Vector3 Max) PlacedBox((Vector3 Min, Vector3 Max)? box, Matrix4x4 placement) =>
        box is { } b ? Picking.Transform(b.Min, b.Max, placement) : (placement.Translation - Vector3.One, placement.Translation + Vector3.One);

    /// <summary>Grows a cell's box to hold <paramref name="box"/> (cells' boxes only grow, until they are made again).</summary>
    internal static void Grow((Vector3 Min, Vector3 Max)[] cellBounds, int cell, (Vector3 Min, Vector3 Max) box)
    {
        var (min, max) = cellBounds[cell];
        cellBounds[cell] = (Vector3.Min(min, box.Min), Vector3.Max(max, box.Max));
    }
}
