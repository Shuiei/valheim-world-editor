#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Vector2: a 2D point or direction in single precision.
/// </summary>
public partial struct Vector2 : IEquatable<Vector2>
{
    /// <summary>
    /// The x component (world x, for map points).
    /// </summary>
    public float x;

    /// <summary>
    /// The y component (world z, for map points).
    /// </summary>
    public float y;

    /// <summary>
    /// The vector (x, y).
    /// </summary>
    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// (0, 0).
    /// </summary>
    public static Vector2 zero => new(0f, 0f);

    /// <summary>
    /// (0, 1).
    /// </summary>
    public static Vector2 up => new(0f, 1f);

    /// <summary>
    /// (0, -1).
    /// </summary>
    public static Vector2 down => new(0f, -1f);

    /// <summary>
    /// (-1, 0).
    /// </summary>
    public static Vector2 left => new(-1f, 0f);

    /// <summary>
    /// (1, 0).
    /// </summary>
    public static Vector2 right => new(1f, 0f);

    /// <summary>
    /// The length. Squares and sums in double, as the game's Mono runtime does with these floats (the float
    /// result differed now and then: rivers and streams a hair off).
    /// </summary>
    public readonly float magnitude => (float)Math.Sqrt((double)x * x + (double)y * y);

    /// <summary>
    /// The squared length, summed in double like <see cref="magnitude"/>.
    /// </summary>
    public readonly float sqrMagnitude => (float)((double)x * x + (double)y * y);

    /// <summary>
    /// The same direction at length 1; zero for vectors shorter than 1e-5, as Unity's.
    /// </summary>
    public readonly Vector2 normalized
    {
        get
        {
            float m = magnitude;
            return m > 1E-05f ? this / m : zero;
        }
    }

    /// <summary>
    /// The distance between two points, summed in double like <see cref="magnitude"/>.
    /// </summary>
    public static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
    }

    /// <summary>
    /// The squared length of a, summed in double.
    /// </summary>
    public static float SqrMagnitude(Vector2 a) => (float)((double)a.x * a.x + (double)a.y * a.y);

    /// <summary>
    /// The sum.
    /// </summary>
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);

    /// <summary>
    /// The difference.
    /// </summary>
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);

    /// <summary>
    /// The opposite direction.
    /// </summary>
    public static Vector2 operator -(Vector2 a) => new(0f - a.x, 0f - a.y);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector2 operator *(float d, Vector2 a) => new(a.x * d, a.y * d);

    /// <summary>
    /// Divided by d.
    /// </summary>
    public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);
}
