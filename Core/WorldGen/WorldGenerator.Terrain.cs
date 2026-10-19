
namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Rivers in the ground.
    /// <summary>
    /// Lowered to the river's bed where a river runs (wider rivers a little deeper). The point is in the generator's
    /// coordinates; the rivers lie in world metres (Expand World Size's prefix on the game's AddRivers multiplies the
    /// point back by the stretch).
    /// </summary>
    private float CutRivers(float x, float z, float h)
    {
        RiverAt(x * _v.Stretch, z * _v.Stretch, out float weight, out float width);
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

    /// <summary>
    /// The Ashlands: lava fields and crags from cellular noise near the edge, high ground blended with
    /// it, lava (mask alpha) in the cracks. The point is in the generator's coordinates (<see cref="Stretched"/>).
    /// </summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="mask">Lava in alpha (0..1).</param>
    /// <param name="cheap">Fewer octaves (the map's mask needs no more).</param>
    /// <returns>The height in the generator's units, not metres.</returns>
    internal float GetAshlandsHeight(float x, float z, out Color mask, bool cheap = false)
    {
        double px = x, pz = z;
        double land = Continent((float)px, (float)pz);
        // Near the edge (within 1 km, waved), and towards the middle line (east-west within 7.5 km).
        double nearEdge = GenMath.Length(px, pz + (double)_v.AshlandsYOffset - (double)_v.AshlandsYOffset * 0.3) - ((double)_v.AshlandsMinDistance + (double)WorldAngle((float)px, (float)pz) * 100.0);
        nearEdge = GenMath.SmoothBlend(0.1, 1.0, 1.0 - GenMath.Clamp01(Math.Abs(nearEdge) / _v.AshlandsLength));
        nearEdge *= 1.0 - GenMath.Clamp01(Math.Abs(px) / _v.AshlandsWidth);
        // Down to the sea floor at the world's edge.
        double inside = 1.0 - GenMath.Clamp01((GenMath.Length(px, pz) - _v.AshlandsSink) / 600.0);
        px += (double)(100000f + _v.DetailX);
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
}
