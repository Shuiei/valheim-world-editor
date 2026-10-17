
namespace TerrainEditor.Desktop;

public static partial class Stamps
{
    /// <summary>The web editor's value noise (the same numbers, so the built-in stamps look the same).</summary>
    private static double Hash(double x, double y)
    {
        double s = Math.Sin(x * 127.1 + y * 311.7) * 43758.5453;
        return s - Math.Floor(s);
    }

    /// <summary>Smooth value noise, 0..1, between the hashes of the four nearest whole points.</summary>
    private static double VNoise(double x, double y)
    {
        double ix = Math.Floor(x), iy = Math.Floor(y), fx = x - ix, fy = y - iy, sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        double a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        return (a + (b - a) * sx) * (1 - sy) + (c + (d - c) * sx) * sy;
    }

    /// <summary>Four octaves of <c>VNoise</c>, scaled back to about 0..1.</summary>
    private static double Fractal(double x, double y)
    {
        double s = 0, a = 0.5;
        for (int o = 0; o < 4; o++)
        {
            s += VNoise(x, y) * a;
            x *= 2.03;
            y *= 2.03;
            a *= 0.5;
        }
        return s / 0.9375;
    }

    /// <summary>
    /// Fills a stamp from a function of (u, v) in -1..1, north up; values are clamped to 0..1.
    /// </summary>
    private static float[] Render(Func<double, double, double> f)
    {
        float[] data = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                data[y * Size + x] = (float)Math.Clamp(f(x / (double)(Size - 1) * 2 - 1, 1 - y / (double)(Size - 1) * 2), 0, 1);
            }
        }
        return data;
    }

    /// <summary>The weight at (u, v) in -1..1 (north up), between the four nearest pixels; 0 outside.</summary>
    public static float Sample(float[] data, float u, float v)
    {
        if (u <= -1 || u >= 1 || v <= -1 || v >= 1)
        {
            return 0;
        }
        float x = (u + 1) / 2 * (Size - 1), y = (1 - (v + 1) / 2) * (Size - 1);
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float tx = x - x0, ty = y - y0;
        float At(int a, int b) => data[Math.Min(Size - 1, b) * Size + Math.Min(Size - 1, a)];
        return (At(x0, y0) * (1 - tx) + At(x0 + 1, y0) * tx) * (1 - ty) + (At(x0, y0 + 1) * (1 - tx) + At(x0 + 1, y0 + 1) * tx) * ty;
    }
}
