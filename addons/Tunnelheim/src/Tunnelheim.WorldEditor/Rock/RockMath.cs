using System.Numerics;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// Unity's float helpers as Tunnelheim's code runs them in the game: the game's Mono works a float expression out in
/// double and rounds it to float only where it is stored, passed or returned, so each helper takes floats, computes in
/// double and rounds its result once. Vectors are System.Numerics ones; only their storage is used.
/// </summary>
internal static class RockMath
{
    /// <summary>Unity's Mathf.Clamp01.</summary>
    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Unity's Mathf.Lerp: a + (b - a) t, t held to 0..1.</summary>
    public static float Lerp(float a, float b, float t) => (float)(a + ((double)b - a) * Clamp01(t));

    /// <summary>Unity's Mathf.InverseLerp: where v lies from a to b, held to 0..1; 0 when a and b are the same.</summary>
#pragma warning disable S1244 // Unity tests the ends for exact equality.
    public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((float)(((double)v - a) / ((double)b - a))) : 0f;
#pragma warning restore S1244

    /// <summary>Unity's Mathf.SmoothStep from 0 to 1: t held to 0..1, then 3t² - 2t³.</summary>
    public static float SmoothStep01(float t)
    {
        double c = Clamp01(t);
        float s = (float)(-2.0 * c * c * c + 3.0 * c * c);
        return (float)(1.0 * s + 0.0 * (1.0 - s));
    }

    /// <summary>Unity's Mathf.Min (the second when either is NaN).</summary>
    public static float Min(float a, float b) => a < b ? a : b;

    /// <summary>Unity's Mathf.Max (the second when either is NaN).</summary>
    public static float Max(float a, float b) => a > b ? a : b;

    /// <summary>Unity's Mathf.Clamp.</summary>
    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>Unity's Mathf.FloorToInt.</summary>
    public static int Floor(float v) => (int)Math.Floor(v);

    /// <summary>Unity's Mathf.CeilToInt.</summary>
    public static int Ceil(float v) => (int)Math.Ceiling(v);

    /// <summary>Unity's Mathf.RoundToInt (halves to even).</summary>
    public static int Round(float v) => (int)Math.Round(v);

    /// <summary>A grid index from metres: v / 0.5 rounded down.</summary>
    public static int FloorStep(float v) => (int)Math.Floor(v / (double)RockGrid.Step);

    /// <summary>A grid index from metres: v / 0.5 rounded up.</summary>
    public static int CeilStep(float v) => (int)Math.Ceiling(v / (double)RockGrid.Step);

    /// <summary>Unity's Vector3.Dot, summed in double.</summary>
    public static float Dot(Vector3 a, Vector3 b) => (float)((double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z);

    /// <summary>Unity's Vector3.magnitude, squares summed in double.</summary>
    public static float Length(Vector3 v) => (float)Math.Sqrt((double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z);

    /// <summary>Unity's Vector3.sqrMagnitude, summed in double.</summary>
    public static float LengthSquared(Vector3 v) => (float)((double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z);

    /// <summary>Unity's Vector3.Normalize: zero for vectors shorter than 1e-5.</summary>
    public static Vector3 Normalize(Vector3 v)
    {
        float m = Length(v);
        return m > 1E-05f ? new Vector3(v.X / m, v.Y / m, v.Z / m) : Vector3.Zero;
    }

    /// <summary>Unity's Vector3.Cross, each component worked out in double.</summary>
    public static Vector3 Cross(Vector3 a, Vector3 b) => new(
        (float)((double)a.Y * b.Z - (double)a.Z * b.Y),
        (float)((double)a.Z * b.X - (double)a.X * b.Z),
        (float)((double)a.X * b.Y - (double)a.Y * b.X));

    /// <summary>Each component's absolute value less <paramref name="e"/>.</summary>
    public static Vector3 AbsLess(Vector3 v, Vector3 e) => new(FloatMath.Abs(v.X) - e.X, FloatMath.Abs(v.Y) - e.Y, FloatMath.Abs(v.Z) - e.Z);

    /// <summary>Unity's Vector3.Max with zero, component by component.</summary>
    public static Vector3 PositivePart(Vector3 v) => new(Max(v.X, 0f), Max(v.Y, 0f), Max(v.Z, 0f));
}
