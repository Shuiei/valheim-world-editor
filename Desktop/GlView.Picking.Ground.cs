using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// What the ground is under a point of the view: the screen point of a grid point, the grid point under the pointer,
// and the world point.
public sealed partial class GlView
{
    /// <summary>Where a grid point (on the ground, lifted a little) is on the view, or null behind the camera.</summary>
    internal Point? ScreenOfGrid(Vector2 g, float lift = 0.6f)
    {
        var s = Scene;
        var size = _surfaceSize.Width > 0 ? _surfaceSize : Bounds.Size;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        return ScreenOf(OnGround(s, g.X, g.Y, lift), size);
    }

    /// <summary>The grid point of the ground under a point of the view (null: none).</summary>
    internal Vector2? GridAt(Point at, Size size) => Scene is { } s && GroundAt(s, _lastViewProj, at, size) is { } g ? new Vector2(g.X, g.Z) : null;

    /// <summary>The world point (x east, height, z north) of the ground under a point of the view, or null.</summary>
    internal Vector3? WorldAt(Point at, Size size)
    {
        var s = Scene;
        if (s == null || GroundAt(s, _lastViewProj, at, size) is not { } g)
        {
            return null;
        }
        float wx = s.OriginX + g.X, wz = s.OriginZ + g.Z;
        return new Vector3(wx, OnGround(s, g.X, g.Z, 0).Y, wz);
    }

    /// <summary>The grid point (fractional) of the ground under a point of the view, or null.</summary>
    private static (float X, float Z)? GroundAt(WorldScene s, Matrix4x4 vp, Point at, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(vp, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        if (Picking.HitGround(s, o, d) is not float t)
        {
            return null;
        }
        var p = o + d * t;
        return (p.X + (s.W - 1) / 2f, -p.Z + (s.H - 1) / 2f);
    }
}
