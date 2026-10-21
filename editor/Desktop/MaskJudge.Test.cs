namespace TerrainEditor.Desktop;

/// <summary>
/// The mask's answer for one ground point: 1 where it passes, 0 where it does not.
/// </summary>
internal sealed partial class MaskJudge
{
    /// <summary>Judges one ground point by the biome, height, slope and paint settings.</summary>
    /// <param name="p">The ground point index.</param>
    /// <returns>1 where the point passes, 0 where it does not.</returns>
    internal float Test(int p)
    {
        if (_biomes != null && (p >= _pointBiomes.Length || !_biomes.Contains(_pointBiomes[p])))
        {
            return 0;
        }
        if (_heightMin != null || _heightMax != null)
        {
            float at = _heights![p];
            if (at < _heightMin || at > _heightMax)
            {
                return 0;
            }
        }
        if (_slopeMin != null || _slopeMax != null)
        {
            float s = Slope(p);
            if (s < _slopeMin || s > _slopeMax)
            {
                return 0;
            }
        }
        return PaintOk(p) ? 1 : 0;
    }
}
