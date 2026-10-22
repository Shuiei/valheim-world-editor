#nullable disable

using System;

namespace ValheimGen;

// Unity's Mathf: the angle and power helpers Expand World Data's biome rules call.
public static partial class Mathf
{
    /// <summary>
    /// Unity's PI, a float.
    /// </summary>
    public const float PI = (float)Math.PI;

    /// <summary>
    /// Unity's degrees-to-radians factor, a float constant (PI * 2 / 360).
    /// </summary>
    public const float Deg2Rad = PI * 2f / 360f;

    /// <summary>
    /// The angle of (x, y) from the y axis, radians, worked out in double.
    /// </summary>
    public static float Atan2(float x, float y) => (float)Math.Atan2(x, y);

    /// <summary>
    /// f to the power p, worked out in double.
    /// </summary>
    public static float Pow(float f, float p) => (float)Math.Pow(f, p);

    /// <summary>
    /// 1 for f at or above 0, else -1 (Unity's Sign: zero counts as positive).
    /// </summary>
    public static float Sign(float f) => f >= 0f ? 1f : -1f;

    /// <summary>
    /// Unity's Quaternion.Euler (degrees): z, then x, then y.
    /// </summary>
    /// <param name="e">The angles about x, y and z, degrees.</param>
    public static System.Numerics.Quaternion Euler(System.Numerics.Vector3 e)
    {
        const float K = MathF.PI / 360f;
        System.Numerics.Quaternion qx = new(MathF.Sin(e.X * K), 0, 0, MathF.Cos(e.X * K)), qy = new(0, MathF.Sin(e.Y * K), 0, MathF.Cos(e.Y * K)), qz = new(0, 0, MathF.Sin(e.Z * K), MathF.Cos(e.Z * K));
        // System.Numerics multiplies right to left like Unity: qy * qx * qz applies qz first.
        return qy * qx * qz;
    }
}
