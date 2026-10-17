
namespace ValheimGen;

public sealed partial class WorldGenerator
{
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
}
