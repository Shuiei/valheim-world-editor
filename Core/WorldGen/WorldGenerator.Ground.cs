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
    /// Generators made so far, to number them (a thread holding a reference would keep one alive).
    /// </summary>
    private static int _generators;
    /// <summary>
    /// This generator's number, for the thread's last continent height.
    /// </summary>
    private readonly int _number = Interlocked.Increment(ref _generators);

    /// <summary>
    /// The generator number and point bits of this thread's last continent height.
    /// </summary>
    [ThreadStatic]
    private static int _continentOf, _continentX, _continentZ;

    /// <summary>
    /// This thread's last continent height.
    /// </summary>
    [ThreadStatic]
    private static float _continent;

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

    /// <summary>
    /// The Meadows: the continents with hills, pressed, rivers cut, then grain.
    /// </summary>
    private float MeadowsShape(float x, float z)
    {
        float h = Hilled(x, z, out float land, out double dx, out double dz);
        h = CutRivers(x, z, Pressed(h, land));
        return Grain(h, dx, dz);
    }

    /// <summary>
    /// The Black Forest (and the Mistlands while water is laid out): the continents with hills, rivers cut, then
    /// grain.
    /// </summary>
    private float ForestShape(float x, float z)
    {
        float h = Hilled(x, z, out _, out double dx, out double dz);
        return Grain(CutRivers(x, z, h), dx, dz);
    }

    /// <summary>
    /// The Plains: as the Meadows, pressed with the product rounded the Plains' way.
    /// </summary>
    private float PlainsShape(float x, float z)
    {
        float h = Hilled(x, z, out float land, out double dx, out double dz);
        float over = h - 0.15f;
        float lift = (float)GenMath.Clamp01((double)land / (double)0.4f);
        if (over > 0f)
        {
            h = (float)((double)h - (double)over * (1.0 - lift) * 0.75);
        }
        return Grain(CutRivers(x, z, h), dx, dz);
    }

    /// <summary>
    /// Swamps: flat at the waterline (0.137) with a little noise, wherever the continents are.
    /// </summary>
    private float SwampShape(float x, float z)
    {
        double dx = (float)((double)x + 100000.0), dz = (float)((double)z + 100000.0);
        float h = 0.137f;
        float ripples = (float)((double)GenMath.Noise(dx * (double)0.04f, dz * (double)0.04f) * GenMath.Noise(dx * (double)0.08f, dz * (double)0.08f));
        h = (float)((double)h + (double)ripples * (double)0.03f);
        return Grain(CutRivers(x, z, h), dx, dz);
    }

    /// <summary>
    /// Mountains: the continents' rise doubled above 0.4, bigger hills, and rocks where the land is steep.
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="menu">On the main menu's land instead of the continents.</param>
    private float MountainShape(float x, float z, bool menu)
    {
        float land = menu ? MenuContinent(x, z) : Continent(x, z);
        float steep = ContinentTilt(x, z);
        double dx = DetailPoint(x), dz = DetailPoint(z);
        float h = (float)((double)land + (double)(float)((double)land - (double)0.4f));
        h = (float)((double)h + (double)Hills(dx, dz) * (double)0.2f);
        h = Grain(CutRivers(x, z, h), dx, dz);
        return (float)((double)h + (double)GenMath.Noise(dx * (double)0.2f, dz * (double)0.2f) * 2.0 * steep);
    }

    /// <summary>
    /// The Mistlands: sharp-topped hills, terraced in steps of 1/400 on them, and a mask for the mist's
    /// cliffs (alpha).
    /// </summary>
    private float MistlandsShape(float x, float z, out Color mask)
    {
        float land = Continent(x, z);
        double dx = DetailPoint(x), dz = DetailPoint(z);
        double s = (double)0.7f;
        float peaks = GenMath.Noise(dx * (double)0.02f * s, dz * (double)0.02f * s) * GenMath.Noise(dx * (double)0.04f * s, dz * (double)0.04f * s);
        peaks = (float)((double)peaks + (double)GenMath.Noise(dx * (double)0.03f * s, dz * (double)0.03f * s) * GenMath.Noise(dx * (double)0.05f * s, dz * (double)0.05f * s) * peaks * 0.5);
        if (peaks > 0f)
        {
            peaks = (float)Math.Pow(peaks, 1.5);
        }
        float h = (float)((double)land + (double)peaks * (double)0.4f);
        h = CutRivers(x, z, h);
        float steep = (float)GenMath.Clamp01((double)peaks * 7.0);
        h = (float)((double)h + (double)GenMath.Noise(dx * (double)0.1f, dz * (double)0.1f) * (double)0.03f * steep);
        h = (float)((double)h + (double)GenMath.Noise(dx * (double)0.4f, dz * (double)0.4f) * (double)0.01f * steep);
        float cliffs = (float)(1.0 - (double)steep * (double)1.2f);
        cliffs = (float)((double)cliffs - (1.0 - (double)GenMath.Step(0.1f, 0.3f, steep)));
        float smooth = (float)((double)h + (double)GenMath.Noise(dx * (double)0.4f, dz * (double)0.4f) * (double)0.002f);
        float terraced = (float)((double)Mathf.Ceil((float)((double)h * 400.0)) / 400.0);
        mask = new Color(0f, 0f, 0f, cliffs);
        return GenMath.Lerp(smooth, terraced, steep);
    }

    /// <summary>
    /// The main menu's ground: the menu's land with the hills and grain.
    /// </summary>
    private float MenuShape(float x, float z)
    {
        float land = MenuContinent(x, z);
        double dx = DetailPoint(x), dz = DetailPoint(z);
        return Grain((float)((double)land + (double)Hills(dx, dz) * (double)0.1f), dx, dz);
    }

    /// <summary>
    /// The Deep North: raised by 0.1, pressed as the Meadows, with a snow mask (green) from noise.
    /// </summary>
    private float DeepNorthShape(float x, float z, out Color mask)
    {
        float land = Continent(x, z) + 0.1f;
        double dx = DetailPoint(x), dz = DetailPoint(z);
        float h = (float)((double)land + (double)Hills(dx, dz) * (double)0.1f);
        h = Grain(CutRivers(x, z, Pressed(h, land)), dx, dz);
        float snow = (float)GenMath.Layers(new Vector2((float)(dx * (double)0.01f), (float)(dz * (double)0.01f)), 3, 2.0, 0.5);
        snow = (snow + 1f) / 2f;
        mask = new Color(0f, 0.3f + snow * 0.3f, 0f, 0f);
        return h;
    }

    /// <summary>
    /// The Deep North as the game has it while it lays out streams (raised unless for rivers).
    /// </summary>
    private float DeepNorthRoughShape(float x, float z, bool forRivers)
    {
        float land = Continent(x, z);
        if (!forRivers)
        {
            land += 0.1f;
        }
        float fx = DetailPoint(x), fz = DetailPoint(z);
        double dx = fx, dz = fz;
        land = (float)((double)land + (double)Mathf.Max(0f, (float)((double)land - (double)0.4f)));
        land = (float)((double)land + (double)Hills(dx, dz) * (double)0.2f);
        land = (float)((double)land * (double)1.2f);
        land = CutRivers(x, z, land);
        // Its grain read in single precision (the game's way here).
        land = (float)((double)land + (double)GenMath.Noise(fx * 0.1f, fz * 0.1f) * (double)0.01f);
        return (float)((double)land + (double)GenMath.Noise(fx * 0.4f, fz * 0.4f) * (double)0.003f);
    }

    /// <summary>
    /// The Ashlands as the game has it while it lays out streams.
    /// </summary>
    private float AshlandsRoughShape(float x, float z)
    {
        float h = Hilled(x, z, out _, out double dx, out double dz);
        h = (float)((double)h + (double)0.1f);
        return CutRivers(x, z, Grain(h, dx, dz));
    }

    /// <summary>
    /// The Ashlands: lava fields and crags from cellular noise near the edge, high ground blended with
    /// it, lava (mask alpha) in the cracks.
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="mask">Lava in alpha (0..1).</param>
    /// <param name="cheap">Fewer octaves (the map's mask needs no more).</param>
    /// <returns>The height in the generator's units, not metres.</returns>
    public float GetAshlandsHeight(float x, float z, out Color mask, bool cheap = false)
    {
        double px = x, pz = z;
        double land = Continent((float)px, (float)pz);
        // Near the edge (within 1 km, waved), and towards the middle line (east-west within 7.5 km).
        double nearEdge = GenMath.Length(px, pz + AshlandsShift - AshlandsShift * 0.3) - (EdgeRadius + (double)WorldAngle((float)px, (float)pz) * 100.0);
        nearEdge = GenMath.SmoothBlend(0.1, 1.0, 1.0 - GenMath.Clamp01(Math.Abs(nearEdge) / 1000.0));
        nearEdge *= 1.0 - GenMath.Clamp01(Math.Abs(px) / 7500.0);
        // Down to the sea floor at the world's edge.
        double inside = 1.0 - GenMath.Clamp01((GenMath.Length(px, pz) - 10150.0) / 600.0);
        px += (double)(100000f + _offsetDetail);
        pz += (double)(100000f + _offsetDetail);
        double cells = 0.0, weight = 1.0, freq = (double)0.33f;
        for (int i = 0, n = cheap ? 2 : 5; i < n; i++)
        {
            cells += weight * GenMath.SmoothBlend(0.0, 1.0, Cells.GetCellular(px * freq, pz * freq));
            freq *= 2.0;
            weight *= 0.5;
        }
        cells = GenMath.Remap(cells, -1.0, 1.0, 0.0, 1.0);
        double crags = GenMath.Lerp(nearEdge, GenMath.Overlay(nearEdge, cells), 0.5);
        double h = GenMath.Lerp(land, (double)0.15f, 0.75);
        h += crags * 0.5;
        h = GenMath.Lerp(-1.0, h, GenMath.SmoothBlend(0.0, 1.0, inside));
        const double LavaLevel = 0.15;
        double cracks = 0.0;
        weight = 1.0;
        freq = 8.0;
        for (int i = 0, n = cheap ? 2 : 3; i < n; i++)
        {
            cracks += weight * Cells.GetCellular(px * freq, pz * freq);
            freq *= 2.0;
            weight *= 0.5;
        }
        cracks = GenMath.Remap(cracks, -1.0, 1.0, 0.0, 1.0);
        cracks = GenMath.Clamp01(Math.Pow(cracks, 4.0) * 2.0);
        double swell = Math.Pow(GenMath.Remap(Cells.GetSimplexFractal(px * 0.075, pz * 0.075), -1.0, 1.0, 0.0, 1.0), (double)1.4f);
        h *= swell;
        double lava = GenMath.Layers(new Vector2((float)(px * (double)0.01f), (float)(pz * (double)0.01f)), 3, 2.0, 0.5);
        lava *= GenMath.Clamp01(GenMath.Remap(nearEdge, 0.0, 0.5, 0.5, 1.0));
        lava = Math.Pow(GenMath.Step((double)0.7f, 1.0, lava), 2.0);
        lava = GenMath.Overlay(lava, cracks);
        lava *= GenMath.Clamp01((h - LavaLevel - 0.02) / 0.01);
        double sink = GenMath.Remap(Math.Pow(GenMath.Noise(px * 0.05 + 5124.0, pz * 0.05 + 5000.0), 2.0), 0.0, 1.0, (double)0.01f, (double)0.055f);
        double floor = Mathf.Clamp((float)(h - sink), (float)(LavaLevel + (double)0.01f), 5000f);
        h = GenMath.Lerp(h, floor, lava);
        mask = new Color(0f, 0f, 0f, (float)lava);
        return (float)h;
    }

    // ---- Rivers in the ground.

    /// <summary>
    /// Lowered to the river's bed where a river runs (wider rivers a little deeper).
    /// </summary>
    private float CutRivers(float x, float z, float h)
    {
        RiverAt(x, z, out float weight, out float width);
        if (weight <= 0f)
        {
            return h;
        }
        float t = GenMath.Step(20f, 60f, width);
        float bank = GenMath.Lerp(0.14f, 0.12f, t);
        float bed = GenMath.Lerp(0.139f, 0.128f, t);
        if (h > bank)
        {
            h = GenMath.Lerp(h, bank, weight);
        }
        if (h > bed)
        {
            h = GenMath.Lerp(h, bed, GenMath.Step(0.85f, 1f, weight));
        }
        return h;
    }
}
