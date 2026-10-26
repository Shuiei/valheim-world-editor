using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Mask, like the web editor's (editor/masks.js; WorldEdit's masks, WorldPainter's filters): limits brushes, paths,
/// shapes, area actions and pastes to some biomes, a height range, a slope range or painted / unpainted ground. Off,
/// everything passes.
/// </summary>
public sealed class Mask
{
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
    /// <param name="english">In English (what Claude is told) rather than the language in use (the Mask panel).</param>
    /// <returns>The warnings joined in one line, or empty when the settings are fine.</returns>
    public string Warning(bool english = false)
    {
        var w = new List<Said>();
        if (SlopeMax is <= 0)
        {
            w.Add(Said.Of("mask.error.flatOnly", SlopeMax));
        }
        if (SlopeMin is float a && SlopeMax is float b && a > b)
        {
            w.Add(Said.Of("mask.error.slopeRange"));
        }
        if (HeightMin is float c && HeightMax is float d && c > d)
        {
            w.Add(Said.Of("mask.error.heightRange"));
        }
        var said = Said.Join(" ", w);
        return english ? said.English : said.Shown;
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
        var judge = new MaskJudge(this, g, biomes);
        if (!frozen)
        {
            return judge.Test;
        }
        var seen = new Dictionary<int, float>();
        return p => seen.TryGetValue(p, out float v) ? v : seen[p] = judge.Test(p);
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
