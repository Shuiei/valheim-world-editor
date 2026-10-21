using System.Numerics;

namespace TerrainEditor.Desktop;

// The conversions between the area's grid, the view's space and the world: the ground's height at a grid point or a
// world point.
public sealed partial class GlView
{
    /// <summary>The ground's height at a grid point (0 with no area).</summary>
    internal float GridHeight(Vector2 g)
    {
        var s = Scene;
        return s == null ? 0 : OnGround(s, g.X, g.Y, 0).Y;
    }

    /// <summary>A world point (x east, height, z north) in view space: from the area's middle, north as -z.</summary>
    /// <param name="s">The area.</param>
    /// <param name="world">The world point.</param>
    private static Vector3 ToView(WorldScene s, Vector3 world) => new(world.X - s.Cx, world.Y, -(world.Z - s.Cz));

    /// <summary>A grid point of the area (x east, z north) in view space: from the area's middle, north as -z.</summary>
    /// <param name="s">The area.</param>
    /// <param name="gx">The grid x.</param>
    /// <param name="gz">The grid z.</param>
    private static (float X, float Z) GridToView(WorldScene s, float gx, float gz) => (gx - (s.W - 1) / 2f, -(gz - (s.H - 1) / 2f));

    /// <summary>A grid point of the area in view space, on the ground there and lifted this many metres.</summary>
    /// <param name="s">The area.</param>
    /// <param name="gx">The grid x.</param>
    /// <param name="gz">The grid z.</param>
    /// <param name="lift">Metres above the ground.</param>
    private static Vector3 OnGround(WorldScene s, float gx, float gz, float lift)
    {
        var (x, z) = GridToView(s, gx, gz);
        return new Vector3(x, Picking.HeightAt(s, x, z) + lift, z);
    }

    /// <summary>The ground's height at a world point.</summary>
    internal float GroundHeight(float wx, float wz)
    {
        var s = Scene;
        return s == null ? 0 : Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz));
    }
}
