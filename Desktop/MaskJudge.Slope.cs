namespace TerrainEditor.Desktop;

/// <summary>
/// The slope check of the mask: how steep the ground is at a point.
/// </summary>
internal sealed partial class MaskJudge
{
    /// <summary>The slope at a ground point, in degrees, from its neighbours' heights.</summary>
    /// <param name="p">The ground point index.</param>
    /// <returns>The slope in degrees.</returns>
    private float Slope(int p)
    {
        int gx = p % _w, gz = p / _w;
        // On the border one neighbour is the point itself, so the step is 1 m there, not 2 m.
        int x0 = Math.Max(0, gx - 1), x1 = Math.Min(_w - 1, gx + 1), z0 = Math.Max(0, gz - 1), z1 = Math.Min(_h - 1, gz + 1);
        float hx = x1 > x0 ? (_heights![gz * _w + x1] - _heights[gz * _w + x0]) / (x1 - x0) : 0;
        float hz = z1 > z0 ? (_heights![z1 * _w + gx] - _heights[z0 * _w + gx]) / (z1 - z0) : 0;
        return MathF.Atan(MathF.Sqrt(hx * hx + hz * hz)) * 180 / MathF.PI;
    }
}
