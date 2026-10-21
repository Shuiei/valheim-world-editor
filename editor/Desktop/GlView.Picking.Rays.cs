using System.Numerics;
using Avalonia;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The rays of the view: the ray through a point, in view space or in world space, and how far the ground is on it.
public sealed partial class GlView
{
    /// <summary>The ray from the camera through a point of the view (view space).</summary>
    internal (Vector3 O, Vector3 D) RayAt(Point at, Size size) =>
        Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));

    /// <summary>
    /// The scene and the view's ray through a point (in the view's own space), or null when there is no scene
    /// or the view has no width yet.
    /// </summary>
    /// <param name="at">The point on the view.</param>
    /// <param name="size">The view's size.</param>
    private (WorldScene S, Vector3 O, Vector3 D)? ViewRay(Point at, Size size)
    {
        var s = Scene;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        return (s, o, d);
    }

    /// <summary>
    /// The view's ray through a point, in world space (x east, up, z north), and how far along it the
    /// ground is (null: it does not meet it).
    /// </summary>
    internal (Vector3 O, Vector3 D, float? GroundT)? WorldRay(Point at, Size size)
    {
        if (ViewRay(at, size) is not { } ray)
        {
            return null;
        }
        var (s, o, d) = ray;
        d = Vector3.Normalize(d);
        float? g = Picking.HitGround(s, o, d);
        var wo = new Vector3(o.X + s.Cx, o.Y, -o.Z + s.Cz);
        var wd = new Vector3(d.X, d.Y, -d.Z);
        // A dungeon's rooms are the ground there (their floors and walls).
        if (_shown[(int)ObjectKind.Dungeons] && DungeonRooms.Shown(s, Wake) is { Count: > 0 } dungeons && RoomSurfaces.Hit(dungeons, Models, wo, wd, CutY ?? float.MaxValue) is float rt && (g == null || rt < g))
        {
            g = rt;
        }
        return (wo, wd, g);
    }
}
