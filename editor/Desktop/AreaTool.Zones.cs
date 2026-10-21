
namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>
    /// The zones under the selection (world zone coordinates): those with a grid point inside the outline, whatever
    /// the soft edge (a point on a zone's edge belongs to its neighbours too, as <see cref="Ground.ZonesOf"/> has
    /// it); a point just outside the outline does not count.
    /// </summary>
    public List<(int X, int Z)> ZonesUnder(Ground g)
    {
        if (WeightsIn(g.W, g.H) is not { } a)
        {
            return [];
        }
        var inside = new List<int>();
        for (int gz = a.Z0; gz <= a.Z1; gz++)
        {
            for (int gx = a.X0; gx <= a.X1; gx++)
            {
                if (Inside(a.Poly, gx, gz))
                {
                    inside.Add(gz * g.W + gx);
                }
            }
        }
        return [.. g.ZonesOf(inside).Select(z => (g.X0 + z.X, g.Z0 + z.Z))];
    }
}
