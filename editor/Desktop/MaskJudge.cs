namespace TerrainEditor.Desktop;

/// <summary>
/// Judges each ground point against a mask's settings. The mask's settings and the ground's heights and paint are
/// copied when the judge is made, so later edits do not change its answers.
/// </summary>
internal sealed partial class MaskJudge
{
    /// <summary>The biomes that pass; null for every biome.</summary>
    private readonly HashSet<int>? _biomes;
    /// <summary>Each ground point's biome, by ground point index.</summary>
    private readonly int[] _pointBiomes;
    /// <summary>Lowest ground height that passes, in metres; null for no limit.</summary>
    private readonly float? _heightMin;
    /// <summary>Highest ground height that passes, in metres; null for no limit.</summary>
    private readonly float? _heightMax;
    /// <summary>Smallest slope that passes, in degrees; null for no limit.</summary>
    private readonly float? _slopeMin;
    /// <summary>Largest slope that passes, in degrees; null for no limit.</summary>
    private readonly float? _slopeMax;
    /// <summary>The rule on painted ground.</summary>
    private readonly Mask.PaintRule _paint;
    /// <summary>The ground's width in points.</summary>
    private readonly int _w;
    /// <summary>The ground's height in points.</summary>
    private readonly int _h;
    /// <summary>Each ground point's height, copied; null when no height or slope limit is set.</summary>
    private readonly float[]? _heights;
    /// <summary>Each ground point's paint flag, copied; null when paint does not matter.</summary>
    private readonly byte[]? _paintFlags;
    /// <summary>Each ground point's paint colour (four floats), copied; null when paint does not matter.</summary>
    private readonly float[]? _colours;

    /// <summary>Copies the mask's settings and the ground's heights and paint, as they are now.</summary>
    /// <param name="mask">The mask whose settings to judge by.</param>
    /// <param name="g">The ground to judge; its heights and paint are copied now.</param>
    /// <param name="biomes">Each point's biome, by ground point index.</param>
    public MaskJudge(Mask mask, Ground g, int[] biomes)
    {
        _biomes = mask.Biomes.Count > 0 ? [.. mask.Biomes] : null;
        _pointBiomes = biomes;
        _heightMin = mask.HeightMin;
        _heightMax = mask.HeightMax;
        _slopeMin = mask.SlopeMin;
        _slopeMax = mask.SlopeMax;
        _paint = mask.Paint;
        _w = g.W;
        _h = g.H;
        if (_heightMin != null || _heightMax != null || _slopeMin != null || _slopeMax != null)
        {
            _heights = new float[_w * _h];
            for (int p = 0; p < _heights.Length; p++)
            {
                _heights[p] = g.HeightOf(p);
            }
        }
        _paintFlags = _paint != Mask.PaintRule.Any ? (byte[])g.PMod.Clone() : null;
        _colours = _paint != Mask.PaintRule.Any ? (float[])g.Paint.Clone() : null;
    }
}
