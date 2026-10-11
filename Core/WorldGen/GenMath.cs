namespace ValheimGen;

// Number helpers for the world generator. The game's terrain is matched to the bit, so each helper
// keeps the precision its callers rely on: float inputs worked out in double and rounded back where
// the result is a float.
public static class GenMath
{
    public static float Length(float x, float y) => (float)Math.Sqrt((double)x * x + (double)y * y);

    public static double Length(double x, double y) => Math.Sqrt(x * x + y * y);

    public static double Clamp01(double v) => v > 1.0 ? 1.0 : v < 0.0 ? 0.0 : v;

    // a at t <= 0, b at t >= 1, in between on the straight line.
    public static float Lerp(float a, float b, float t) =>
        t <= 0f ? a : t >= 1f ? b : (float)((double)a * (1.0 - t) + (double)b * t);

    public static double Lerp(double a, double b, double t) =>
        t <= 0.0 ? a : t >= 1.0 ? b : a * (1.0 - t) + b * t;

    // Where v lies from lo (0) to hi (1), held to 0..1.
    public static float Step(float lo, float hi, float v) => (float)Clamp01(((double)v - lo) / ((double)hi - lo));

    public static double Step(double lo, double hi, double v) => Clamp01((v - lo) / (hi - lo));

    // The same, 0 when lo and hi are the same.
    public static double InverseLerp(double lo, double hi, double v) => lo == hi ? 0.0 : Clamp01((v - lo) / (hi - lo));

    // Step eased at both ends (3s² - 2s³).
    public static float Smooth(float lo, float hi, float x)
    {
        float s = (float)Clamp01(((double)x - lo) / ((double)hi - lo));
        return (float)((double)s * s * (3.0 - 2.0 * s));
    }

    // An eased blend from `from` to `to` (t held to 0..1), rounded through float as Unity's is.
    public static double SmoothBlend(double from, double to, double t)
    {
        t = Clamp01(t);
        t = -2.0 * t * t * t + 3.0 * t * t;
        return (float)(to * t + from * (1.0 - t));
    }

    // A value carried from one range to another (held to the new one).
    public static double Remap(double value, double fromLo, double fromHi, double toLo, double toHi) =>
        Lerp(toLo, toHi, InverseLerp(fromLo, fromHi, value));

    // The "overlay" blend of two 0..1 values: darker below a half, lighter above.
    public static double Overlay(double a, double b) => a < 0.5 ? 2.0 * a * b : 1.0 - 2.0 * (1.0 - a) * (1.0 - b);

    public static float Noise(double x, double y) => UnityPerlin.Noise((float)x, (float)y);

    public static float Noise(float x, float y) => UnityPerlin.Noise(x, y);

    // Layers of noise, each `lacunarity` times finer and `gain` times weaker (in float).
    public static float Layers(Vector2 p, int count, float lacunarity, float gain)
    {
        float sum = 0f, weight = 1f;
        for (int i = 0; i < count; i++)
        {
            sum = (float)((double)sum + (double)weight * Noise(p.x, p.y));
            weight = (float)((double)weight * gain);
            p *= lacunarity;
        }
        return sum;
    }

    public static float Layers(Vector3 p, int count, float lacunarity, float gain) => Layers(new Vector2(p.x, p.z), count, lacunarity, gain);

    // The same in double.
    public static double Layers(Vector2 p, int count, double lacunarity, double gain)
    {
        double sum = 0.0, weight = 1.0, x = p.x, y = p.y;
        for (int i = 0; i < count; i++)
        {
            sum += weight * Noise(x, y);
            weight *= gain;
            x *= lacunarity;
            y *= lacunarity;
        }
        return sum;
    }
}
