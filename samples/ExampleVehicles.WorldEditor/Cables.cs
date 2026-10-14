using System.Collections.Generic;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The cables (Keys.Post): each post names the post its cable goes to (Keys.Target, that post's Keys.Id); the mod
/// strings the cable while the game runs. Drawn here as a thin ribbon from the top of this post to the top of the
/// other, in world space (a shape reaching another object), found through the world.
/// </summary>
public sealed class Cables : IShapeProvider
{
    /// <summary>How high on a post a cable is tied, in the post's frame.</summary>
    private static readonly Vector3 Tie = new(0, 2, 0);

    /// <summary>The ribbon's half width, in metres.</summary>
    private const float HalfWidth = 0.03f;

    /// <summary>Tarred rope.</summary>
    private static readonly Vector4 Colour = new(0.2f, 0.16f, 0.12f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world)
    {
        if (!obj.TryGetInt(Keys.Target, out int target) || !world.TryFind(Keys.Id, target, out var other))
        {
            yield break;
        }
        var from = obj.Position + Vector3.Transform(Tie * obj.Scale, obj.Orientation);
        var to = other.Position + Vector3.Transform(Tie * other.Scale, other.Orientation);
        var along = to - from;
        if (along.LengthSquared() < 1e-6f)
        {
            yield break;
        }
        // A ribbon lying flat: across the cable, on the level.
        var across = Vector3.Normalize(Vector3.Cross(along, Vector3.UnitY));
        if (float.IsNaN(across.X))
        {
            across = Vector3.UnitX;
        }
        across *= HalfWidth;
        yield return new Shape([from - across, from + across, to + across, to - across], [0, 1, 2, 0, 2, 3], Colour) { BothSides = true, WorldSpace = true };
    }
}
