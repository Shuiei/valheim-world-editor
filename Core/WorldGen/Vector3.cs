#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Vector3: a 3D point or direction in single precision (y up).
/// </summary>
internal partial struct Vector3 : IEquatable<Vector3>
{
    /// <summary>
    /// The x component (east).
    /// </summary>
    public float X;

    /// <summary>
    /// The y component (height).
    /// </summary>
    public float Y;

    /// <summary>
    /// The z component (north).
    /// </summary>
    public float Z;

    /// <summary>
    /// The vector (x, y, z).
    /// </summary>
    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// (0, 0, 0).
    /// </summary>
    public static Vector3 Zero => new(0f, 0f, 0f);

    /// <summary>
    /// The length (summed in float).
    /// </summary>
    public readonly float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>
    /// The same direction at length 1; zero for vectors shorter than 1e-5.
    /// </summary>
    public readonly Vector3 Normalized => Normalize(this);

    /// <summary>
    /// v at length 1; zero for vectors shorter than 1e-5, as Unity's.
    /// </summary>
    public static Vector3 Normalize(Vector3 v)
    {
        float m = v.Magnitude;
        return m > 1E-05f ? v / m : Zero;
    }

    /// <summary>
    /// The sum.
    /// </summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>
    /// The difference.
    /// </summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector3 operator *(Vector3 a, float d) => new(a.X * d, a.Y * d, a.Z * d);

    /// <summary>
    /// Divided by d.
    /// </summary>
    public static Vector3 operator /(Vector3 a, float d) => new(a.X / d, a.Y / d, a.Z / d);
}
