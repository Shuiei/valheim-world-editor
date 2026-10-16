namespace TerrainEditor.Desktop;

/// <summary>
/// Natural-looking detail: seeded gradient noise layered at several scales (fractal noise), the same family of noise
/// Valheim's own terrain is built from. The web editor's noise2 and fbm (editor.html), with the same seeding, so a seed
/// gives the same pattern in both.
/// </summary>
public sealed class Noise
{
    /// <summary>
    /// The permutation table, shuffled from the seed and doubled to 512 so lookups need no wrap.
    /// </summary>
    private readonly byte[] _perm = new byte[512];
    /// <summary>
    /// The seed the permutation table was shuffled from.
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// A noise whose pattern is fixed by <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">Any int; the same seed gives the same pattern here and in the web editor.</param>
    public Noise(int seed)
    {
        Seed = seed;
        uint a = (uint)seed;
        double Rnd()
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
        int[] p = [.. Enumerable.Range(0, 256)];
        for (int i = 255; i > 0; i--)
        {
            int j = (int)Math.Floor(Rnd() * (i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++)
        {
            _perm[i] = (byte)p[i & 255];
        }
    }

    /// <summary>
    /// One octave of gradient noise at (x, y), one lattice cell per unit, scaled by 0.7 as in the web editor. Repeats
    /// every 256 units.
    /// </summary>
    public float Noise2(float x, float y)
    {
        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        static float Lerp(float a, float b, float t) => a + t * (b - a);
        static float Grad(int h, float x, float y)
        {
            h &= 7;
            float u = h < 4 ? x : y, v = h < 4 ? y : x;
            return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -v : v);
        }
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y), xi = x0 & 255, yi = y0 & 255;
        x -= x0;
        y -= y0;
        float u = Fade(x), v = Fade(y);
        int aa = _perm[_perm[xi] + yi], ab = _perm[_perm[xi] + yi + 1], ba = _perm[_perm[xi + 1] + yi], bb = _perm[_perm[xi + 1] + yi + 1];
        return Lerp(Lerp(Grad(aa, x, y), Grad(ba, x - 1, y), u), Lerp(Grad(ab, x, y - 1), Grad(bb, x - 1, y - 1), u), v) * 0.7f;
    }

    /// <summary>
    /// What <see cref="Fbm"/> never goes beyond either way: each octave's gradient term is at most |x| + |y| = 2,
    /// blended (never more than its corners) and scaled by 0.7; the octaves' weighted mean is scaled by 1.6.
    /// </summary>
    public const float FbmBound = 2 * 0.7f * 1.6f;

    /// <summary>
    /// About -1..1, four octaves: each one twice the frequency and half the amplitude of the last, offset so they do
    /// not line up.
    /// </summary>
    public float Fbm(float x, float y)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < 4; o++)
        {
            sum += Noise2(x, y) * amp;
            norm += amp;
            amp *= 0.5f;
            x = x * 2 + 17.3f;
            y = y * 2 - 9.1f;
        }
        return sum / norm * 1.6f;
    }
}
