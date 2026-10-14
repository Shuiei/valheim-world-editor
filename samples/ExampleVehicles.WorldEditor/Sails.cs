using System.Collections.Generic;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The sails (Keys.Sail): the mod weaves their cloth while the game runs, between three or four corners kept in the
/// sail's data (its own frame); drawn here as that flat cloth, in the sail's colour.
/// </summary>
public sealed class Sails : IShapeProvider
{
    /// <summary>The cloth's colour when the sail keeps none: undyed sailcloth.</summary>
    private static readonly Vector4 Cloth = new(0.86f, 0.82f, 0.72f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world)
    {
        if (!obj.TryGetInt(Keys.Corners, out int count) || count is < 3 or > 4)
        {
            yield break;
        }
        var corners = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            if (!obj.TryGetVector3(Keys.Corner + (i + 1), out corners[i]))
            {
                yield break;
            }
        }
        int[] triangles = count == 3 ? [0, 1, 2] : [0, 1, 2, 0, 2, 3];
        yield return new Shape(corners, triangles, ColourOf(obj)) { BothSides = true };
    }

    /// <summary>The sail's colour (four bytes: red, green, blue, alpha); else undyed cloth.</summary>
    private static Vector4 ColourOf(IObjectView obj) =>
        obj.TryGetBytes(Keys.Colour, out byte[] c) && c.Length == 4 ? new Vector4(c[0] / 255f, c[1] / 255f, c[2] / 255f, c[3] / 255f) : Cloth;
}
