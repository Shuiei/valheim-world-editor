namespace ValheimGen;

public static partial class GenMath
{
    /// <summary>
    /// Step eased at both ends (3s² - 2s³).
    /// </summary>
    public static float Smooth(float lo, float hi, float x)
    {
        float s = (float)Fraction(lo, hi, x);
        return (float)((double)s * s * (3.0 - 2.0 * s));
    }

    /// <summary>
    /// An eased blend from <paramref name="from"/> to <paramref name="to"/> (t held to 0..1), rounded through
    /// float as Unity's is.
    /// </summary>
    public static double SmoothBlend(double from, double to, double t)
    {
        t = Clamp01(t);
        t = -2.0 * t * t * t + 3.0 * t * t;
        return (float)(to * t + from * (1.0 - t));
    }

    /// <summary>
    /// A value carried from one range to another (held to the new one).
    /// </summary>
    public static double Remap(double value, double fromLo, double fromHi, double toLo, double toHi) =>
        Lerp(toLo, toHi, InverseLerp(fromLo, fromHi, value));

    /// <summary>
    /// The "overlay" blend of two 0..1 values: darker below a half, lighter above.
    /// </summary>
    public static double Overlay(double a, double b) => a < 0.5 ? 2.0 * a * b : 1.0 - 2.0 * (1.0 - a) * (1.0 - b);
}
