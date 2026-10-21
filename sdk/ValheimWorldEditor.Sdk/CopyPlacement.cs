using System;
using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Where a pasted copy lands (<see cref="IAreaCopier.Paste"/>): its middle's new place, and how it is turned and
/// mirrored, as its objects are.
/// </summary>
public sealed class CopyPlacement
{
    /// <summary>A copy's landing.</summary>
    /// <param name="middle">Where the copy's middle lands, in world metres.</param>
    /// <param name="turn">The turn, in degrees counter-clockwise seen from above.</param>
    /// <param name="mirror">Mirrored east to west (before turning).</param>
    public CopyPlacement(Vector3 middle, float turn, bool mirror)
    {
        Middle = middle;
        Turn = turn;
        Mirror = mirror;
    }

    /// <summary>
    /// Where the copy's middle (<see cref="CopyRegion.Middle"/>) lands, in world metres; its height is the one the
    /// copy's objects are put down from.
    /// </summary>
    public Vector3 Middle { get; }

    /// <summary>The turn, in degrees counter-clockwise seen from above (0 to 360).</summary>
    public float Turn { get; }

    /// <summary>Whether the copy is mirrored east to west, before it is turned.</summary>
    public bool Mirror { get; }

    /// <summary>Where a place of the copy lands.</summary>
    /// <param name="offset">The place, as an offset from the copy's middle where it was copied.</param>
    /// <returns>The place in the world, in metres.</returns>
    public Vector3 Place(Vector3 offset)
    {
        float x = Mirror ? -offset.X : offset.X;
        double t = Turn * Math.PI / 180;
        float c = (float)Math.Cos(t), s = (float)Math.Sin(t);
        return new Vector3(Middle.X + x * c - offset.Z * s, Middle.Y + offset.Y, Middle.Z + x * s + offset.Z * c);
    }

    /// <summary>
    /// A yaw (Unity's Euler y, degrees clockwise seen from above) of the copy where it lands: mirrored, which
    /// reverses it, then turned, as the copy's objects are.
    /// </summary>
    /// <param name="yaw">The yaw where it was copied.</param>
    public float Yaw(float yaw) => (Mirror ? -yaw : yaw) - Turn;
}
