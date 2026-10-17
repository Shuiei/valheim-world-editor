#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Vector3: a 3D point or direction in single precision (y up).
/// </summary>
public partial struct Vector3 : IEquatable<Vector3>
{
    /// <summary>
    /// The x component (east).
    /// </summary>
    public float x;

    /// <summary>
    /// The y component (height).
    /// </summary>
    public float y;

    /// <summary>
    /// The z component (north).
    /// </summary>
    public float z;

    /// <summary>
    /// The vector (x, y, z).
    /// </summary>
    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    /// <summary>
    /// (0, 0, 0).
    /// </summary>
    public static Vector3 zero => new(0f, 0f, 0f);

    /// <summary>
    /// The length (summed in float).
    /// </summary>
    public readonly float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

    /// <summary>
    /// The same direction at length 1; zero for vectors shorter than 1e-5.
    /// </summary>
    public readonly Vector3 normalized => Normalize(this);

    /// <summary>
    /// v at length 1; zero for vectors shorter than 1e-5, as Unity's.
    /// </summary>
    public static Vector3 Normalize(Vector3 v)
    {
        float m = v.magnitude;
        return m > 1E-05f ? v / m : zero;
    }

    /// <summary>
    /// The sum.
    /// </summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);

    /// <summary>
    /// The difference.
    /// </summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);

    /// <summary>
    /// Divided by d.
    /// </summary>
    public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
}
