#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Vector2: a 2D point or direction in single precision.
/// </summary>
internal partial struct Vector2 : IEquatable<Vector2>
{
    /// <summary>
    /// The x component (world x, for map points).
    /// </summary>
    public float X;

    /// <summary>
    /// The y component (world z, for map points).
    /// </summary>
    public float Y;

    /// <summary>
    /// The vector (x, y).
    /// </summary>
    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// (0, 0).
    /// </summary>
    public static Vector2 Zero => new(0f, 0f);

    /// <summary>
    /// (0, 1).
    /// </summary>
    public static Vector2 Up => new(0f, 1f);

    /// <summary>
    /// (0, -1).
    /// </summary>
    public static Vector2 Down => new(0f, -1f);

    /// <summary>
    /// (-1, 0).
    /// </summary>
    public static Vector2 Left => new(-1f, 0f);

    /// <summary>
    /// (1, 0).
    /// </summary>
    public static Vector2 Right => new(1f, 0f);

    /// <summary>
    /// The length. Squares and sums in double, as the game's Mono runtime does with these floats (the float
    /// result differed now and then: rivers and streams a hair off).
    /// </summary>
    public readonly float Magnitude => (float)Math.Sqrt((double)X * X + (double)Y * Y);

    /// <summary>
    /// The squared length, summed in double like <see cref="Magnitude"/>.
    /// </summary>
    public readonly float SqrLength => (float)((double)X * X + (double)Y * Y);

    /// <summary>
    /// The same direction at length 1; zero for vectors shorter than 1e-5, as Unity's.
    /// </summary>
    public readonly Vector2 Normalized
    {
        get
        {
            float m = Magnitude;
            return m > 1E-05f ? this / m : Zero;
        }
    }

    /// <summary>
    /// The distance between two points, summed in double like <see cref="Magnitude"/>.
    /// </summary>
    public static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
    }

    /// <summary>
    /// The squared length of a, summed in double.
    /// </summary>
    public static float SqrMagnitude(Vector2 a) => (float)((double)a.X * a.X + (double)a.Y * a.Y);

    /// <summary>
    /// The sum.
    /// </summary>
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>
    /// The difference.
    /// </summary>
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>
    /// The opposite direction.
    /// </summary>
    public static Vector2 operator -(Vector2 a) => new(0f - a.X, 0f - a.Y);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector2 operator *(Vector2 a, float d) => new(a.X * d, a.Y * d);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector2 operator *(float d, Vector2 a) => new(a.X * d, a.Y * d);

    /// <summary>
    /// Divided by d.
    /// </summary>
    public static Vector2 operator /(Vector2 a, float d) => new(a.X / d, a.Y / d);
}
