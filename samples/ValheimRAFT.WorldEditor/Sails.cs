using System.Collections.Generic;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// The custom sails ("MBSail"): the mod weaves their cloth while the game runs, between three or four corners kept in
/// the sail's data (its own frame); drawn here as that flat cloth, in its main colour.
/// </summary>
public sealed class Sails : IShapeProvider
{
    /// <summary>The custom sail's prefab.</summary>
    public const string Prefab = "MBSail";

    /// <summary>The cloth's colour when the sail keeps none: undyed sailcloth.</summary>
    private static readonly Vector4 Cloth = new(0.86f, 0.82f, 0.72f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj)
    {
        if (!obj.TryGetInt("m_sailCornersCountHash", out int count) || count is < 3 or > 4)
        {
            yield break;
        }
        var corners = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            if (!obj.TryGetVector3($"m_sailCorner{i + 1}Hash", out corners[i]))
            {
                yield break;
            }
        }
        int[] triangles = count == 3 ? [0, 1, 2] : [0, 1, 2, 0, 2, 3];
        yield return new Shape(corners, triangles, ColourOf(obj)) { BothSides = true };
    }

    /// <summary>The sail's main colour (four bytes: red, green, blue, alpha), opaque; else undyed cloth.</summary>
    private static Vector4 ColourOf(IObjectView obj) =>
        obj.TryGetBytes("m_mainColor", out byte[] c) && c.Length == 4 ? new Vector4(c[0] / 255f, c[1] / 255f, c[2] / 255f, 1) : Cloth;
}
