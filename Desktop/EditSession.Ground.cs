
namespace TerrainEditor.Desktop;

/// <summary>
/// Ground changes made all at once (a path, an area action, a mountain's raise) as one undo step.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A change of the ground made all at once (a path, an area action): change gets the ground and returns the points
    /// it changed and the grid rectangle around them. One undo step.
    /// </summary>
    /// <returns>The ground points changed (none: no undo step).</returns>
    public List<int> EditGround(string label, Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)> change)
    {
        List<int> touched = [];
        Commit(label, g =>
        {
            var made = change(g);
            touched = made.Touched;
            return made;
        }, [], []);
        return touched;
    }

    /// <summary>
    /// Runs a ground change of a step under the lock: the ground points it touched, their values before and after, and
    /// the zones they are in (sent to the edit store). Nothing when there is no change or it touched nothing.
    /// </summary>
    /// <param name="ground">The ground change (see Commit); null for none.</param>
    private (int[] Points, Ground.State Before, Ground.State After, List<(int X, int Z)> Zones) ChangeGround(
        Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? ground)
    {
        lock (_lock)
        {
            if (ground == null)
            {
                return ([], NoPoints, NoPoints, []);
            }
            var start = Ground.Snapshot();
            var (touched, rect) = ground(Ground);
            if (touched.Count == 0)
            {
                return ([], NoPoints, NoPoints, []);
            }
            Touch(rect);
            var zones = Ground.ZonesOf(touched);
            int[] points = [.. touched.Distinct().Order()];
            var (before, after) = (Pick(start, points), Pick(Ground.Snapshot(), points));
            Send(zones);
            return (points, before, after, zones);
        }
    }

    /// <summary>
    /// Raises the ground of a mountain: each point inside its reach that may change gets the mountain's height there,
    /// with the game's limit lifted meanwhile.
    /// </summary>
    /// <param name="gr">The ground.</param>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="reach">The mountain's reach in grid points.</param>
    /// <param name="shape">The mountain's height at a point, from its middle.</param>
    /// <param name="mask">The mask over the ground, or null when off.</param>
    /// <returns>The points changed, and the grid box around them.</returns>
    private static (List<int> Touched, (int, int, int, int) Rect) RaiseGround(Ground gr, float cx, float cz, float reach,
        Func<float, float, float> shape, Func<int, float>? mask)
    {
        var touched = new List<int>();
        bool was = gr.NoLimit;
        gr.NoLimit = true;
        int x0 = (int)MathF.Floor(cx - reach), x1 = (int)MathF.Ceiling(cx + reach), z0 = (int)MathF.Floor(cz - reach), z1 = (int)MathF.Ceiling(cz + reach);
        try
        {
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    int p = gz * gr.W + gx;
                    float w = mask?.Invoke(p) ?? 1;
                    if (gr.Locked(gx, gz) || w <= 0)
                    {
                        continue;
                    }
                    float v = shape(gx - cx, gz - cz) * w;
                    if (v == 0)
                    {
                        continue;
                    }
                    gr.SetHeight(p, gr.HeightOf(p) + v);
                    touched.Add(p);
                }
            }
        }
        finally
        {
            gr.NoLimit = was;
        }
        return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1));
    }
}
