
namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// Applies the action along the line. Returns the points changed (grid indices), the rectangle around them (one
    /// point wider each way), and whether the game's limit stopped some. Does nothing with fewer than two points.
    /// </summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="b">The brush, for its noise (Natural look).</param>
    /// <param name="water">The water level, for River.</param>
    /// <param name="mask">Per grid point weight from 0 to 1 (0 leaves it alone); null changes every point.</param>
    public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Apply(Ground g, Brush b, float water, Func<int, float>? mask = null)
    {
        var curve = Curve();
        if (curve.Count < 2)
        {
            return (new List<int>(), (0, 0, 0, 0), false);
        }
        return new ApplyRun(this, g, b, water, mask, curve).Run();
    }
}
