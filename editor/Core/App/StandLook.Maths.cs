using static TerrainEditor.App.GameBundles;

namespace TerrainEditor.App;

/// <summary>
/// The quaternion and matrix helpers of placing held items (StandLook.Maths.cs), in Unity's conventions.
/// </summary>
public static partial class StandLook
{
    /// <summary>A transform matrix from a position, a rotation quaternion (x, y, z, w) and a scale.</summary>
    internal static float[] Trs(float[] position, float[] rotation, float[] scale) =>
        Matrix.Trs(position[0], position[1], position[2], rotation[0], rotation[1], rotation[2], rotation[3], scale[0], scale[1], scale[2]);

    /// <summary>The product of two rotation quaternions (x, y, z, w): as Unity has it, a * b (b applied first).</summary>
    internal static float[] Mul(float[] a, float[] b) =>
    [
        a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
        a[3] * b[1] + a[1] * b[3] + a[2] * b[0] - a[0] * b[2],
        a[3] * b[2] + a[2] * b[3] + a[0] * b[1] - a[1] * b[0],
        a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
    ];

    /// <summary>
    /// Unity's Euler angles (degrees; applied z, then x, then y) as a quaternion (x, y, z, w).
    /// </summary>
    internal static float[] Euler(float[] degrees)
    {
        const float D = MathF.PI / 360f;
        float[] x = [MathF.Sin(degrees[0] * D), 0, 0, MathF.Cos(degrees[0] * D)];
        float[] y = [0, MathF.Sin(degrees[1] * D), 0, MathF.Cos(degrees[1] * D)];
        float[] z = [0, 0, MathF.Sin(degrees[2] * D), MathF.Cos(degrees[2] * D)];
        return Mul(y, Mul(x, z));
    }

    /// <summary>
    /// A transform matrix with its scale taken out (each axis made unit length): the position and turn alone, as a
    /// joint gives an item parented to it with its own scale kept.
    /// </summary>
    internal static float[] Unscaled(float[] m)
    {
        float[] r = (float[])m.Clone();
        for (int c = 0; c < 3; c++)
        {
            float len = MathF.Sqrt(m[c] * m[c] + m[4 + c] * m[4 + c] + m[8 + c] * m[8 + c]);
            if (len > 1e-12f)
            {
                r[c] = m[c] / len;
                r[4 + c] = m[4 + c] / len;
                r[8 + c] = m[8 + c] / len;
            }
        }
        return r;
    }
}
