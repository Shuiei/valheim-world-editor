#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Mathf: the few helpers the generator calls, with sine and cosine worked out in double.
/// </summary>
public static partial class Mathf
{
    /// <summary>
    /// |f|.
    /// </summary>
    public static float Abs(float f) => Math.Abs(f);

    /// <summary>
    /// The sine of f (radians), worked out in double.
    /// </summary>
    public static float Sin(float f) => (float)Math.Sin(f);

    /// <summary>
    /// The cosine of f (radians), worked out in double.
    /// </summary>
    public static float Cos(float f) => (float)Math.Cos(f);

    /// <summary>
    /// The smaller of a and b.
    /// </summary>
    public static float Min(float a, float b) => a < b ? a : b;

    /// <summary>
    /// The smaller of a and b.
    /// </summary>
    public static int Min(int a, int b) => a < b ? a : b;

    /// <summary>
    /// The larger of a and b.
    /// </summary>
    public static float Max(float a, float b) => a > b ? a : b;

    /// <summary>
    /// The larger of a and b.
    /// </summary>
    public static int Max(int a, int b) => a > b ? a : b;

    /// <summary>
    /// f rounded up.
    /// </summary>
    public static float Ceil(float f) => (float)Math.Ceiling(f);

    /// <summary>
    /// f rounded up, as an int.
    /// </summary>
    public static int CeilToInt(float f) => (int)Math.Ceiling(f);

    /// <summary>
    /// f rounded down, as an int.
    /// </summary>
    public static int FloorToInt(float f) => (int)Math.Floor(f);

}
