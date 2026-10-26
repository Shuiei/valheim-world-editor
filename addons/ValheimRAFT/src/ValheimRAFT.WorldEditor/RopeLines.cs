using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// The ropes of rope anchors (<see cref="Keys.RopeAnchor"/>, and the dock's tie points): the mod strings each, while the
/// game runs, from the anchor's tie point to what it names (its <see cref="Keys.Ropes"/>), found here through the world
/// by its id. Drawn as a straight rope, as the mod draws it, in the world's frame (it reaches another object). A rope
/// to something not in the area is not drawn. The tie points were measured on the mod's and the game's prefabs
/// (ValheimRAFT 5.0.9 and 5.1.1, Valheim 1.0.20).
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
            var to = other.Position + Vector3.Transform(TieOn(other.Prefab, rope.Point) * other.Scale, other.Orientation);
            if (Line(from, to) is { } shape)
            {
                yield return shape;
            }
        }
    }

    /// <summary>
    /// The tie points the mod knows on what a rope is tied to, in its frame, by prefab and the rope's tie point: the
    /// dock's, a Karve's and a longship's ropes (their prefabs' own, measured on the game's), and the neck of a deer, a
    /// boar, a piglet or a neck as their prefabs have it at rest (the game moves it as the animal walks).
    /// </summary>
    private static readonly Dictionary<string, Vector3[]> Ties = new()
    {
        [Keys.DockAnchor] = [DockTie],
        ["Karve"] = [new(0, 7.88f, 0), new(-0.972f, 0.805f, 2.273f), new(-0.95f, 0.805f, -2.452f), new(0.972f, 0.805f, 2.273f), new(0.95f, 0.805f, -2.452f)],
        ["VikingShip"] =
        [
            new(0, 2.416f, 9.959f), new(0, 0.802f, 0.275f), new(2.4128f, 0.5304f, -0.9136f), new(-2.4128f, 0.5304f, -0.9136f),
            new(-2.32512f, 1.36864f, 3.57056f), new(-2.2288f, 1.305f, -3.808f), new(2.32512f, 1.36864f, 3.57056f), new(2.2288f, 1.305f, -3.808f),
        ],
        ["Deer"] = [new(0, 1.17609f, 0.48907f)],
        ["Boar"] = [new(0, 0.67175f, 0.41237f)],
        ["Boar_piggy"] = [new(0, 0.33587f, 0.20618f)],
        ["Neck"] = [new(0, 0.78225f, 0.18676f)],
    };

    /// <summary>
    /// Where on an object of a prefab the mod ties a rope with that tie point, in its frame: one of its known tie points,
    /// else where it stands (another rope anchor too).
    /// </summary>
    public static Vector3 TieOn(string prefab, byte point) =>
        Ties.TryGetValue(prefab, out var ties) && point < ties.Length ? ties[point] : Vector3.Zero;

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
