namespace TerrainEditor.Desktop;

/// <summary>
/// The Mask, like the web editor's (editor/masks.js; WorldEdit's masks, WorldPainter's filters): limits brushes, paths,
/// shapes, area actions and pastes to some biomes, a height range, a slope range or painted / unpainted ground. Off,
/// everything passes.
/// </summary>
public sealed class Mask
{
    /// <summary>
    /// The biomes the Mask panel offers, in its order: the game's biome flag and its display name.
    /// </summary>
    public static readonly (int Biome, string Name)[] BiomeNames =
    [
        (1, "Meadows"), (8, "Black Forest"), (2, "Swamp"), (4, "Mountain"), (16, "Plains"),
        (512, "Mistlands"), (32, "Ashlands"), (64, "Deep North"), (256, "Ocean"),
    ];

    /// <summary>
    /// Which ground passes by its paint. Dirt, Cultivated and Paved need painted ground with that colour channel (red,
    /// green, blue) at least half.
    /// </summary>
    public enum PaintRule
    {
        /// <summary>Paint does not matter.</summary>
        Any,
        /// <summary>Ground never painted, or painted almost black.</summary>
        Unpainted,
        /// <summary>Painted ground.</summary>
        Painted,
        /// <summary>Painted with red (dirt) at least half.</summary>
        Dirt,
        /// <summary>Painted with green (cultivated) at least half.</summary>
        Cultivated,
        /// <summary>Painted with blue (paved) at least half.</summary>
        Paved,
    }

    /// <summary>
    /// Whether the mask applies at all; off, <c>For</c> returns null and everything passes.
    /// </summary>
    public bool On { get; set; }
    /// <summary>
    /// The biome flags that pass. None chosen: every biome.
    /// </summary>
    public HashSet<int> Biomes { get; } = [];
    /// <summary>
    /// Lowest ground height that passes, in metres; null for no limit.
    /// </summary>
    public float? HeightMin { get; set; }
    /// <summary>
    /// Highest ground height that passes, in metres; null for no limit.
    /// </summary>
    public float? HeightMax { get; set; }
    /// <summary>
    /// Smallest slope that passes, in degrees; null for no limit.
    /// </summary>
    public float? SlopeMin { get; set; }
    /// <summary>
    /// Largest slope that passes, in degrees; null for no limit.
    /// </summary>
    public float? SlopeMax { get; set; }
    /// <summary>
    /// The rule on painted ground.
    /// </summary>
    public PaintRule Paint { get; set; }
    /// <summary>
    /// Raised by <c>Notify</c> after the settings change.
    /// </summary>
    public event Action? Changed;
    /// <summary>
    /// Tells listeners the settings changed; call after setting the properties.
    /// </summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>
    /// Settings that let (almost) nothing through.
    /// </summary>
    /// <returns>The warnings joined in one line, or empty when the settings are fine.</returns>
    public string Warning()
    {
        var w = new List<string>();
        if (SlopeMax is <= 0)
        {
            w.Add($"Slope max {SlopeMax}° only lets perfectly flat ground through: empty the box for no limit.");
        }
        if (SlopeMin is float a && SlopeMax is float b && a > b)
        {
            w.Add("Slope min is above max: nothing matches.");
        }
        if (HeightMin is float c && HeightMax is float d && c > d)
        {
            w.Add("Height min is above max: nothing matches.");
        }
        return string.Join(" ", w);
    }

    /// <summary>
    /// The mask over a ground (1 where it passes, 0 elsewhere), or null when off. The ground is judged as it is now,
    /// when the mask is made: an edit that changes it while asking (a mountain, a shape, a paste, a brush stroke) is
    /// not judged against its own changes (a slope then mixed changed and unchanged neighbours).
    /// </summary>
    /// <param name="g">The ground to judge; its heights and paint are copied now.</param>
    /// <param name="biomes">Each point's biome, by ground point index.</param>
    /// <param name="frozen">Each point is also judged only once (the answer is remembered).</param>
    /// <returns>A test by ground point index, or null when the mask is off.</returns>
    public Func<int, float>? For(Ground g, int[] biomes, bool frozen = false)
    {
        if (!On)
        {
            return null;
        }
        var set = Biomes.Count > 0 ? new HashSet<int>(Biomes) : null;
        float? hmin = HeightMin, hmax = HeightMax, smin = SlopeMin, smax = SlopeMax;
        var paint = Paint;
        int w = g.W, h = g.H;
        float[]? heights = null;
        if (hmin != null || hmax != null || smin != null || smax != null)
        {
            heights = new float[w * h];
            for (int p = 0; p < heights.Length; p++)
            {
                heights[p] = g.HeightOf(p);
            }
        }
        byte[]? pmod = paint != PaintRule.Any ? (byte[])g.PMod.Clone() : null;
        float[]? colours = paint != PaintRule.Any ? (float[])g.Paint.Clone() : null;
        float Slope(int p)
        {
            int gx = p % w, gz = p / w;
            float hx = heights![gz * w + Math.Min(w - 1, gx + 1)] - heights[gz * w + Math.Max(0, gx - 1)];
            float hz = heights[Math.Min(h - 1, gz + 1) * w + gx] - heights[Math.Max(0, gz - 1) * w + gx];
            return MathF.Atan(MathF.Sqrt(hx * hx + hz * hz) / 2) * 180 / MathF.PI;
        }
        bool PaintOk(int p)
        {
            if (paint == PaintRule.Any)
            {
                return true;
            }
            bool painted = pmod![p] != 0;
            float r = colours![p * 4], gr = colours[p * 4 + 1], b = colours[p * 4 + 2];
            return paint switch
            {
                PaintRule.Unpainted => !painted || r + gr + b < 0.3f,
                _ when !painted => false,
                PaintRule.Painted => r + gr + b >= 0.3f,
                PaintRule.Dirt => r >= 0.5f,
                PaintRule.Cultivated => gr >= 0.5f,
                _ => b >= 0.5f,
            };
        }
        float Test(int p)
        {
            if (set != null && (p >= biomes.Length || !set.Contains(biomes[p])))
            {
                return 0;
            }
            if (hmin != null || hmax != null)
            {
                float at = heights![p];
                if (at < hmin || at > hmax)
                {
                    return 0;
                }
            }
            if (smin != null || smax != null)
            {
                float s = Slope(p);
                if (s < smin || s > smax)
                {
                    return 0;
                }
            }
            return PaintOk(p) ? 1 : 0;
        }
        if (!frozen)
        {
            return Test;
        }
        var seen = new Dictionary<int, float>();
        return p => seen.TryGetValue(p, out float v) ? v : seen[p] = Test(p);
    }

    /// <summary>
    /// Alt + Shift + click: the height range around the clicked ground (±2 m). Turns the mask on.
    /// </summary>
    /// <param name="h">The clicked ground's height, in metres.</param>
    public void PickHeight(float h)
    {
        On = true;
        HeightMin = MathF.Round(h - 2, 1);
        HeightMax = MathF.Round(h + 2, 1);
        Notify();
    }
}
