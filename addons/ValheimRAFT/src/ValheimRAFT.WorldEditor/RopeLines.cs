using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// The ropes of rope anchors (<see cref="Keys.RopeAnchor"/>, and the dock's tie points): the mod strings each, while the
/// game runs, from the anchor's tie point to what it names (its <see cref="Keys.Ropes"/>), found here through the world
/// by its id. Drawn as a straight rope, as the mod draws it, in the world's frame (it reaches another object). A rope
/// to something not in the area is not drawn. The tie points were measured on the mod's prefabs (ValheimRAFT 5.0.9).
/// </summary>
internal sealed class RopeLines : IShapeProvider
{
    /// <summary>Where on a rope anchor its ropes are tied, in its frame: its "attachpoint", as the mod's prefab has it.</summary>
    public static readonly Vector3 AnchorTie = new(0, 0.1435f, 0);

    /// <summary>Where on the dock's tie point its ropes are tied, in its frame.</summary>
    public static readonly Vector3 DockTie = new(1.018f, 0.1875f, 1);

    /// <summary>The rope's half thickness, in metres.</summary>
    private const float Half = 0.04f;

    /// <summary>Hemp rope.</summary>
    private static readonly Vector4 Hemp = new(0.45f, 0.36f, 0.24f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world)
    {
        if (!obj.TryGetBytes(Keys.Ropes, out byte[] bytes) || RopeList.Read(bytes) is not { Count: > 0 } ropes)
        {
            yield break;
        }
        var from = obj.Position + Vector3.Transform((obj.Prefab == Keys.DockAnchor ? DockTie : AnchorTie) * obj.Scale, obj.Orientation);
        foreach (var rope in ropes)
        {
            if (!world.TryFind(Keys.Id, rope.Target, out var other))
            {
                continue;
            }
            // The mod ties a rope to the dock at its tie point, to anything else where it stands (another rope
            // anchor too; its tie points on a vanilla ship or an animal are their bones, which the editor does not
            // have).
            var to = other.Prefab == Keys.DockAnchor ? other.Position + Vector3.Transform(DockTie * other.Scale, other.Orientation) : other.Position;
            if (Line(from, to) is { } shape)
            {
                yield return shape;
            }
        }
    }

    /// <summary>
    /// A rope from one point to another: two crossed ribbons, so that it shows from every side; null when the points
    /// are one.
    /// </summary>
    public static Shape? Line(Vector3 from, Vector3 to)
    {
        var along = to - from;
        if (along.LengthSquared() < 1e-6f)
        {
            return null;
        }
        var dir = Vector3.Normalize(along);
        // Across the rope, on the level (or along x for a rope hanging straight down), and the other way across.
        var side = Vector3.Cross(dir, Vector3.UnitY);
        side = side.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(side, dir));
        side *= Half;
        up *= Half;
        Vector3[] corners = [from - side, from + side, to + side, to - side, from - up, from + up, to + up, to - up];
        return new Shape(corners, [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7], Hemp) { BothSides = true, WorldSpace = true };
    }
}
