namespace ValheimGen;

public static partial class GenMath
{
    /// <summary>
    /// Unity's Perlin noise (about 0..1) at (x, y), the coordinates rounded to float first.
    /// </summary>
    public static float Noise(double x, double y) => UnityPerlin.Noise((float)x, (float)y);

    /// <summary>
    /// Unity's Perlin noise (about 0..1) at (x, y).
    /// </summary>
    public static float Noise(float x, float y) => UnityPerlin.Noise(x, y);

    /// <summary>
    /// Layers of noise, each <paramref name="lacunarity"/> times finer and <paramref name="gain"/> times weaker
    /// (in float).
    /// </summary>
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

    /// <summary>
    /// The same at a world position's x and z.
    /// </summary>
    public static float Layers(Vector3 p, int count, float lacunarity, float gain) => Layers(new Vector2(p.x, p.z), count, lacunarity, gain);

    /// <summary>
    /// Layers of noise in double, each <paramref name="lacunarity"/> times finer and <paramref name="gain"/>
    /// times weaker.
    /// </summary>
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
