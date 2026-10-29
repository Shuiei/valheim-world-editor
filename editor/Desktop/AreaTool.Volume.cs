using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>
    /// Cut and fill (each point stands for 1 m²): what Flatten, Raise or Lower would move within the
    /// game's ±8 m limit (and what is out of its reach; with No limit on, within the lift's ±400 m of the
    /// generated ground, as <see cref="Ground.SetHeight"/> keeps it), and how much the ground inside has
    /// been raised and dug since it was generated.
    /// </summary>
    /// <returns>The text for the panel, empty while there is no selection.</returns>
    public string Volume(Ground g, GroundAction act, float height, float amount, Func<int, float>? mask = null)
    {
        if (WeightsIn(g.W, g.H, mask) is not { } a)
        {
            return "";
        }
        static string M3(double v) => v >= 1000 ? Lang.T("area.volume.thousands", v / 1000) : Lang.T("area.volume.cubic", Math.Round(v));
        double up = 0, down = 0, cut = 0, fill = 0, outOfReach = 0, raise = 0, lower = 0;
        foreach (var (p, w) in a.Cells)
        {
            if (g.Locked(p % g.W, p / g.W))
            {
                continue;
            }
            float h = g.HeightOf(p), b = g.Original(p), d = (h - b) * w;
            if (d > 0)
            {
                up += d;
            }
            else
            {
                down -= d;
            }

            // What SetHeight can reach: No limit moves the lift (±MaxLift round the generated ground, the
            // edit within the limit kept as it is), the limit the level part (±MaxLevel round the lifted
            // ground, the smoothing part kept: it takes that much of the reach on its side).
            float smooth = g.Mod[p] != 0 ? g.Smooth[p] : 0;
            float lo = g.NoLimit ? g.Base[p] + (h - b) - EditStore.MaxLift : b - EditStore.MaxLevel + MathF.Max(0, smooth);
            float hi = g.NoLimit ? g.Base[p] + (h - b) + EditStore.MaxLift : b + EditStore.MaxLevel + MathF.Min(0, smooth);
            float want = h + (height - h) * w, got = Math.Clamp(want, lo, hi);
            if (got > h)
            {
                fill += got - h;
            }
            else
            {
                cut += h - got;
            }

            outOfReach += MathF.Abs(want - got);
            raise += MathF.Max(0, MathF.Min(hi, h + amount * w) - h);
            lower += MathF.Max(0, h - MathF.Max(lo, h - amount * w));
        }
        string first = act switch
        {
            GroundAction.Flatten when outOfReach >= 1 && g.NoLimit => Lang.T("area.volume.flattenLift", height, M3(cut), M3(fill), M3(outOfReach)),
            GroundAction.Flatten when outOfReach >= 1 => Lang.T("area.volume.flattenLimit", height, M3(cut), M3(fill), M3(outOfReach)),
            GroundAction.Flatten => Lang.T("area.volume.flatten", height, M3(cut), M3(fill)),
            GroundAction.Raise => Lang.T("area.volume.raise", amount, M3(raise)),
            GroundAction.Lower => Lang.T("area.volume.lower", amount, M3(lower)),
            _ => "",
        };
        return (first == "" ? "" : first + "\n") + Lang.T("area.volume.inside", M3(up), M3(down));
    }

    /// <summary>The average height of the ground inside (for Flatten's "avg"), null without a selection.</summary>
    public float? Average(Ground g)
    {
        if (WeightsIn(g.W, g.H) is not { Cells.Count: > 0 } a)
        {
            return null;
        }
        return a.Cells.Average(c => g.HeightOf(c.G));
    }
}
