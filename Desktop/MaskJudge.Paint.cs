namespace TerrainEditor.Desktop;

/// <summary>
/// The paint check of the mask: whether a ground point's paint passes the paint rule.
/// </summary>
internal sealed partial class MaskJudge
{
    /// <summary>Whether a ground point's paint passes the paint rule.</summary>
    /// <param name="p">The ground point index.</param>
    /// <returns>True when the paint passes, or the rule does not care about paint.</returns>
    internal bool PaintOk(int p)
    {
        if (_paint == Mask.PaintRule.Any)
        {
            return true;
        }
        bool painted = _paintFlags![p] != 0;
        float r = _colours![p * 4], gr = _colours[p * 4 + 1], b = _colours[p * 4 + 2];
        return _paint switch
        {
            Mask.PaintRule.Unpainted => !painted || r + gr + b < 0.3f,
            _ when !painted => false,
            Mask.PaintRule.Painted => r + gr + b >= 0.3f,
            Mask.PaintRule.Dirt => r >= 0.5f,
            Mask.PaintRule.Cultivated => gr >= 0.5f,
            _ => b >= 0.5f,
        };
    }
}
