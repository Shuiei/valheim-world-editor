using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// Converting a blueprint's rotations between Unity Euler angles and quaternions.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// Unity's Euler angles (degrees, applied z, then x, then y: R = Ry * Rx * Rz) of a quaternion, normalised first. One
    /// with no length to normalise (all zero, as blank rotation fields read) or none that fits a float is no turn.
    /// </summary>
    public static Vector3 ToEuler(Quaternion q)
    {
        float length = q.Length();
        if (!float.IsFinite(length) || length < 1e-6f)
        {
            return Vector3.Zero;
        }
        q *= 1f / length;
        float m02 = 2f * (q.X * q.Z + q.W * q.Y), m22 = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        float m12 = 2f * (q.Y * q.Z - q.W * q.X), m10 = 2f * (q.X * q.Y + q.W * q.Z), m11 = 1f - 2f * (q.X * q.X + q.Z * q.Z);
        float m00 = 1f - 2f * (q.Y * q.Y + q.Z * q.Z), m20 = 2f * (q.X * q.Z - q.W * q.Y);
        float x, y, z;
        if (MathF.Abs(m12) < 0.99999f)
        {
            x = MathF.Asin(-m12);
            y = MathF.Atan2(m02, m22);
            z = MathF.Atan2(m10, m11);
        }
        else
        {
            // Looking straight up or down: only the sum of y and z is defined.
            x = m12 < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            y = MathF.Atan2(-m20, m00);
            z = 0f;
        }
        return new Vector3(Deg(x), Deg(y), Deg(z));
        static float Deg(float r) { float d = r * 180f / MathF.PI; d %= 360f; return d < 0 ? d + 360f : d; }
    }

    /// <summary>
    /// Unity's Quaternion.Euler: z, then x, then y.
    /// </summary>
    public static Quaternion FromEuler(Vector3 e)
    {
        const float K = MathF.PI / 360f;
        Quaternion qx = new(MathF.Sin(e.X * K), 0, 0, MathF.Cos(e.X * K)), qy = new(0, MathF.Sin(e.Y * K), 0, MathF.Cos(e.Y * K)), qz = new(0, 0, MathF.Sin(e.Z * K), MathF.Cos(e.Z * K));
        // System.Numerics multiplies right to left like Unity: qy * qx * qz applies qz first.
        return qy * qx * qz;
    }
}
