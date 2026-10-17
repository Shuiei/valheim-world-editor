using System.Numerics;

namespace TerrainEditor.Desktop;

// One object at a grid point: whether it can go there, and where and how it stands.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Zone and grid shapes can be turned as a whole, by this many degrees (-180 to 180); their objects turn with them.
    /// </summary>
    public float ShapeTurn { get; private set; }

    /// <summary>The point the shape turns about: its bounding box's middle when first turned.</summary>
    private Vector2? _pivot;

    /// <summary>The mode's shape can be turned (Zone and Grid).</summary>
    private bool Turnable => Mode is Modes.Zone or Modes.Grid;

    /// <summary>The rotation plus the shape's turn where it turns.</summary>
    private float RotationNow => Rotation + (Turnable ? ShapeTurn : 0);

    /// <summary>
    /// One object at a grid point, or null where it cannot go: off the block's edge, masked, thinned out by clumping,
    /// too close to another (or its grow room), or under water.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="g">The grid point.</param>
    /// <param name="d">The object's random draws.</param>
    /// <param name="names">The kinds to pick from.</param>
    /// <param name="hash">What stands already, for the spacing.</param>
    /// <param name="mask">The mask, or null.</param>
    /// <param name="minDist">The distance to keep from others instead of the mode's own.</param>
    /// <param name="yaw">The facing to use instead of a random one (degrees, before the rotation).</param>
    private Placement? PlacementAt(WorldScene s, Vector2 g, Draw d, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, float? minDist = null, float? yaw = null)
    {
        if (!OnBlockAndMask(s, g, mask))
        {
            return null;
        }
        if (Mode is Modes.Brush or Modes.Zone && !ClumpKeeps(s, g, d.K))
        {
            return null;
        }
        string name = PickName(names, d.T);
        if (!hash.Free(g, MathF.Max(minDist ?? SpacingFor(name), GrowNeed(name))))
        {
            return null;
        }
        float y = HeightAt(s, g);
        if (Elevation == Elevations.Ground && y < s.Water - 0.3f && !UnderwaterOk(names))
        {
            return null;
        }
        float smin = SizeMin / 100, smax = MathF.Max(smin, SizeMax / 100);
        float ry = (yaw ?? (RandomYaw ? d.Ry * 360 : 0)) + RotationNow;
        float ox = s.OriginX, oz = s.OriginZ;
        return new Placement(name, new Vector3(ox + g.X, Elevated(name, y), oz + g.Y), new Vector3(d.Rx * Tilt, ry, d.Rz * Tilt), smin + d.S * (smax - smin), g);
    }
}
