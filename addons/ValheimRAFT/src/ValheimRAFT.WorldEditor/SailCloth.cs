using System.Globalization;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// The custom sails (<see cref="Keys.Sail"/>): the mod weaves their cloth while the game runs, between the three or
/// four corners the sail keeps in its own frame, so the game's files have no model of it. Drawn here as that cloth,
/// flat, seen from both sides, in the sail's colour. Their colours are shown in words in the inspector.
/// </summary>
internal sealed class SailCloth : IShapeProvider, IDataDecoder
{
    /// <summary>The cloth's own colour, which the sail's colour tints: undyed sailcloth.</summary>
    private static readonly Vector4 Cloth = new(0.86f, 0.82f, 0.72f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world)
    {
        if (Corners(obj) is not { } corners)
        {
            yield break;
        }
        int[] triangles = corners.Length == 3 ? [0, 1, 2] : [0, 1, 2, 0, 2, 3];
        yield return new Shape(corners, triangles, ColourOf(obj)) { BothSides = true };
    }

    /// <summary>
    /// The sail's corners, in its frame; null when it has none to weave (not made yet, or a corner missing or not a
    /// number). A sail of an older version kept four corners without their count: the mod takes it as four, so does
    /// this.
    /// </summary>
    public static Vector3[]? Corners(IObjectView obj)
    {
        int count = obj.TryGetInt(Keys.SailCorners, out int c) ? c : 0;
        if (count == 0 && Enumerable.Range(1, 4).All(i => obj.TryGetVector3(Keys.SailCornerKey(i), out _)))
        {
            count = 4;
        }
        if (count is not (3 or 4))
        {
            return null;
        }
        var corners = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            if (!obj.TryGetVector3(Keys.SailCornerKey(i + 1), out corners[i]) || !IsNumber(corners[i]))
            {
                return null;
            }
        }
        return corners;
    }

    /// <summary>Whether every coordinate is a number (not NaN nor infinite).</summary>
    private static bool IsNumber(Vector3 v) => !float.IsNaN(v.X + v.Y + v.Z) && !float.IsInfinity(v.X + v.Y + v.Z);

    /// <summary>The cloth tinted by the sail's colour, opaque; undyed cloth when it keeps none.</summary>
    private static Vector4 ColourOf(IObjectView obj) =>
        obj.TryGetBytes(Keys.SailColour, out byte[] c) && Colour(c) is { } tint ? new Vector4(Cloth.X * tint.X, Cloth.Y * tint.Y, Cloth.Z * tint.Z, 1) : Cloth;

    /// <summary>A colour as the mod keeps it (red, green, blue, alpha as four bytes); null for other bytes.</summary>
    public static Vector4? Colour(byte[] bytes) =>
        bytes.Length == 4 ? new Vector4(bytes[0] / 255f, bytes[1] / 255f, bytes[2] / 255f, bytes[3] / 255f) : null;

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value) => value.Length == 4
        ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}, {3}% opaque", value[0], value[1], value[2], (int)Math.Round(value[3] * 100 / 255.0))
        : null;
}
