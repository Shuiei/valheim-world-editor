using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Shape tool and the natural objects a new ground buries.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Natural objects (trees, rocks, ore, bushes, pickables): what new ground buries or leaves floating.
    /// </summary>
    internal static bool Natural(WorldScene.Thing t) => !t.Piece && !t.Tamed
        && ObjectKinds.Of(Terrain.PrefabCatalog.NameOf(t.Prefab), false) is ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Ore or ObjectKind.Bushes or ObjectKind.Pickables;

    /// <summary>
    /// Puts a shape into the ground around grid point (cx, cz) (Shape tool). One undo step.
    /// </summary>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="f">The formula: the metres to add at each point within the radius (x, z metres east and north of
    /// the middle, d the distance, r the radius, h the height, n(x, z) the brushes' noise).</param>
    /// <param name="r">The radius in metres.</param>
    /// <param name="h">The height, h in the formula.</param>
    /// <param name="label">The history's name for the step.</param>
    /// <returns>How many points changed, whether any reached the game's ±8 m limit, and how many points the formula
    /// gave no number for (they are left as they were).</returns>
    public (int Touched, bool Clamped, int Bad) Shape(float cx, float cz, Func<Formula.Env, double> f, float r, float h, string label)
    {
        // The web editor gives shapes its fractal noise scaled to about -1..1.
        var env = new Formula.Env { Noise = (x, z) => Brush.Noise.Fbm((float)x, (float)z) / 1.6 };
        var mask = MaskNow();
        int touchedCount = 0, bad = 0;
        bool clamped = false, failed = false;
        Commit(label, g =>
        {
            var touched = new List<int>();
            int x0 = Math.Max(1, (int)MathF.Floor(cx - r)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + r));
            int z0 = Math.Max(1, (int)MathF.Floor(cz - r)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + r));
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    float x = gx - cx, z = gz - cz, d = MathF.Sqrt(x * x + z * z);
                    int p = gz * g.W + gx;
                    float w = mask?.Invoke(p) ?? 1;
                    if (d > r || g.Locked(gx, gz) || w <= 0)
                    {
                        continue;
                    }
                    env.Vars["x"] = x;
                    // North is up the grid.
                    env.Vars["z"] = z;
                    env.Vars["d"] = d;
                    env.Vars["r"] = r;
                    env.Vars["h"] = h;
                    double v;
                    try
                    {
                        v = f(env);
                    }
                    catch (Exception e)
                    {
                        // Logged at the first point only: a formula that fails tends to fail at every point.
                        if (!failed)
                        {
                            failed = true;
                            Log.CaughtBrief(e, "working out the shape's formula at a point (said once for the shape)");
                        }
                        v = double.NaN;
                    }
                    if (!double.IsFinite(v))
                    {
                        bad++;
                        continue;
                    }
                    if (v == 0)
                    {
                        continue;
                    }
                    clamped |= g.SetHeight(p, g.HeightOf(p) + (float)v * w);
                    touched.Add(p);
                }
            }
            touchedCount = touched.Count;
            return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1));
        }, [], []);
        return (touchedCount, clamped, bad);
    }
}
