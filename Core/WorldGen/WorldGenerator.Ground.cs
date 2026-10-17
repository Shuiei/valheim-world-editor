
namespace ValheimGen;

// The ground's shape, in the generator's units (HeightScale metres each; the sea's surface at 0.15).
// Each biome starts from the continents' height and adds its own hills and detail; rivers then cut
// through. The constants are the game's floats (0.01f...), widened to double where the game works in
// double, so every rounding falls where it falls in the game.
public sealed partial class WorldGenerator
{
    // ---- The continents.
    /// <summary>
    /// The continents' height: products of wide noise fields, flattened in rings round the middle (no
    /// mountains near the start) and sunk past the world's edge. A point's biome and then its height
    /// both need it: the last one is kept on the thread (by generator and the point's exact bits).
    /// </summary>
    private float Continent(float x, float z)
    {
        int bx = BitConverter.SingleToInt32Bits(x), bz = BitConverter.SingleToInt32Bits(z);
        if (_continentOf == _number && _continentX == bx && _continentZ == bz)
        {
            return _continent;
        }
        float h = WorkOutContinent(x, z);
        (_continentOf, _continentX, _continentZ, _continent) = (_number, bx, bz, h);
        return h;
    }

    /// <summary>
    /// This thread's last continent height.
    /// </summary>
    [ThreadStatic]
    private static float _continent;

    /// <summary>
    /// The generator number and point bits of this thread's last continent height.
    /// </summary>
    [ThreadStatic]
    private static int _continentOf, _continentX, _continentZ;

    /// <summary>
    /// Generators made so far, to number them (a thread holding a reference would keep one alive).
    /// </summary>
    private static int _generators;

    /// <summary>
    /// This generator's number, for the thread's last continent height.
    /// </summary>
    private readonly int _number = Interlocked.Increment(ref _generators);

    /// <summary>
    /// The continents' height, worked out (Continent keeps the last one).
    /// </summary>
    private float WorkOutContinent(float x, float z)
    {
        float fromMiddle = GenMath.Length(x, z);
        double cx = x, cz = z;
        cx += 100000.0 + _offsetA;
        cz += 100000.0 + _offsetB;
        float h = Swells(cx, cz);
        // Ridges where two fields cross, kept out of the middle 744 to 1000 m.
        float r1 = GenMath.Noise(cx * (double)0.002f * 0.25 + (double)0.123f, cz * (double)0.002f * 0.25 + (double)0.15123f);
        float r2 = GenMath.Noise(cx * (double)0.002f * 0.25 + (double)0.321f, cz * (double)0.002f * 0.25 + (double)0.231f);
        float flat = (float)(1.0 - (double)GenMath.Step(0.02f, 0.12f, Mathf.Abs((float)((double)r1 - r2))));
        flat = (float)((double)flat * GenMath.Smooth(744f, 1000f, fromMiddle));
        h = (float)((double)h * (1.0 - flat));
        if (fromMiddle > WorldSize)
        {
            // Past the world: down to the sea floor, then the edge.
            h = GenMath.Lerp(h, -0.2f, GenMath.Step(WorldSize, EdgeOfWorld, fromMiddle));
            if (fromMiddle > 10490f)
            {
                h = GenMath.Lerp(h, -2f, Utils.LerpStep(10490f, EdgeOfWorld, fromMiddle));
            }
            return h * 1f + 0f;
        }
        if (fromMiddle < _mountainFreeRadius && h > 0.28f)
        {
            // No mountains near the start: tops held to 0.28..0.38, eased in over 400 m.
            float t = (float)GenMath.Clamp01(((double)h - (double)0.28f) / 0.09999999403953552);
            h = GenMath.Lerp(GenMath.Lerp(0.28f, 0.38f, t), h, GenMath.Step((float)(_mountainFreeRadius - 400.0), _mountainFreeRadius, fromMiddle));
        }
        return h * 1f + 0f;
    }

    /// <summary>
    /// The main menu's land: the same swells, nothing else.
    /// </summary>
    private float MenuContinent(float x, float z)
    {
        double cx = x, cz = z;
        cx += 100000.0 + _offsetA;
        cz += 100000.0 + _offsetB;
        return Swells(cx, cz) * 1f + 0f;
    }

    /// <summary>
    /// Three products of noise fields, each finer one weighed by what is there already.
    /// </summary>
    private static float Swells(double cx, double cz)
    {
        float h = 0f;
        h = (float)((double)h + (double)GenMath.Noise(cx * (double)0.002f * 0.5, cz * (double)0.002f * 0.5) * (double)GenMath.Noise(cx * (double)0.003f * 0.5, cz * (double)0.003f * 0.5) * 1.0);
        h = (float)((double)h + (double)GenMath.Noise(cx * (double)0.002f, cz * (double)0.002f) * (double)GenMath.Noise(cx * (double)0.003f, cz * (double)0.003f) * (double)h * (double)0.9f);
        h = (float)((double)h + (double)GenMath.Noise(cx * (double)0.005f, cz * (double)0.005f) * (double)GenMath.Noise(cx * (double)0.01f, cz * (double)0.01f) * 0.5 * (double)h);
        return (float)((double)h - (double)0.07f);
    }

    /// <summary>
    /// The continents' slope at a point: how much they change a metre to each side.
    /// </summary>
    private float ContinentTilt(float x, float z)
    {
        float west = Continent((float)((double)x - 1.0), z), east = Continent((float)((double)x + 1.0), z);
        float south = Continent(x, (float)((double)z - 1.0)), north = Continent(x, (float)((double)z + 1.0));
        return (float)((double)Mathf.Abs((float)((double)east - west)) + (double)Mathf.Abs((float)((double)south - north)));
    }

    // ---- What the biomes add.
    /// <summary>
    /// Where the small detail is read: the point moved by the seed (and far from the noise's origin).
    /// </summary>
    private float DetailPoint(float v) => (float)((double)v + 100000.0 + _offsetDetail);

    /// <summary>
    /// Rolling hills: two products of noise, the finer weighed by the coarser.
    /// </summary>
    private static float Hills(double dx, double dz)
    {
        float r = (float)((double)GenMath.Noise(dx * (double)0.01f, dz * (double)0.01f) * GenMath.Noise(dx * (double)0.02f, dz * (double)0.02f));
        return (float)((double)r + (double)GenMath.Noise(dx * (double)0.05f, dz * (double)0.05f) * GenMath.Noise(dx * (double)0.1f, dz * (double)0.1f) * r * 0.5);
    }

    /// <summary>
    /// The ground's grain: two fine noises.
    /// </summary>
    private static float Grain(float h, double dx, double dz)
    {
        h = (float)((double)h + (double)GenMath.Noise(dx * (double)0.1f, dz * (double)0.1f) * (double)0.01f);
        return (float)((double)h + (double)GenMath.Noise(dx * (double)0.4f, dz * (double)0.4f) * (double)0.003f);
    }

    /// <summary>
    /// Low land kept low: above 0.15, hills are pressed down where the continents are low (the Meadows'
    /// and the Deep North's way; the Plains' rounds its product differently).
    /// </summary>
    private static float Pressed(float h, float land)
    {
        float over = (float)((double)h - (double)0.15f);
        float lift = (float)GenMath.Clamp01((double)land / (double)0.4f);
        return over > 0f ? (float)((double)h - (double)over * ((1.0 - lift) * 0.75)) : h;
    }

    /// <summary>
    /// The continents with their hills, the detail points they were read at: the height before rivers and grain.
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="land">The continents' height at the point.</param>
    /// <param name="dx">The detail point of x.</param>
    /// <param name="dz">The detail point of z.</param>
    private float Hilled(float x, float z, out float land, out double dx, out double dz)
    {
        land = Continent(x, z);
        dx = DetailPoint(x);
        dz = DetailPoint(z);
        return (float)((double)land + (double)Hills(dx, dz) * (double)0.1f);
    }
}
