namespace ValheimGen;

/// <summary>
/// Number helpers for the world generator. The game's terrain is matched to the bit (but for a few Deep
/// North heights, less than a millimetre off), so each helper keeps the precision its callers rely on:
/// float inputs worked out in double and rounded back where the result is a float.
/// </summary>
public static partial class GenMath
{
    /// <summary>
    /// The length of (x, y), worked out in double and rounded to float.
    /// </summary>
    public static float Length(float x, float y) => (float)Math.Sqrt((double)x * x + (double)y * y);

    /// <summary>
    /// The length of (x, y) in double.
    /// </summary>
    public static double Length(double x, double y) => Math.Sqrt(x * x + y * y);

    /// <summary>
    /// v held to 0..1.
    /// </summary>
    public static double Clamp01(double v) => v > 1.0 ? 1.0 : v < 0.0 ? 0.0 : v;

    /// <summary>
    /// a at t &lt;= 0, b at t &gt;= 1, in between on the straight line (worked out in double).
    /// </summary>
    public static float Lerp(float a, float b, float t) =>
        t <= 0f ? a : t >= 1f ? b : (float)((double)a * (1.0 - t) + (double)b * t);

    /// <summary>
    /// The same in double.
    /// </summary>
    public static double Lerp(double a, double b, double t) =>
        t <= 0.0 ? a : t >= 1.0 ? b : a * (1.0 - t) + b * t;

    /// <summary>
    /// Where v lies from lo (0) to hi (1), held to 0..1; when lo and hi are the same, a step there (0 below, 1 from it
    /// on) instead of 0 / 0.
    /// </summary>
#pragma warning disable S1244 // Exactly equal ends only: they alone make 0 / 0; close ends divide as they should.
    private static double Fraction(double lo, double hi, double v) => lo == hi ? (v < lo ? 0.0 : 1.0) : Clamp01((v - lo) / (hi - lo));
#pragma warning restore S1244

    /// <summary>
    /// Where v lies from lo (0) to hi (1), held to 0..1 (a step at lo when lo and hi are the same).
    /// </summary>
    public static float Step(float lo, float hi, float v) => (float)Fraction(lo, hi, v);

    /// <summary>
    /// The same in double.
    /// </summary>
    public static double Step(double lo, double hi, double v) => Fraction(lo, hi, v);

    /// <summary>
    /// Where v lies from lo (0) to hi (1), held to 0..1; 0 when lo and hi are the same.
    /// </summary>
    public static double InverseLerp(double lo, double hi, double v) => lo == hi ? 0.0 : Clamp01((v - lo) / (hi - lo));
}
