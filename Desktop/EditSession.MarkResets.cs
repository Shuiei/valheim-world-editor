using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Marking zones for reset as one undo step, with the ground edits there undone.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Marks zones for reset when saving (Reset zones), as one undo step: the generator makes them again, their trees,
    /// rocks, ruins and dungeon entrances gone (and the players' buildings unless kept); with ground, their ground
    /// edits in the area are undone now too.
    /// </summary>
    /// <param name="zones">The zones (world zone column and row).</param>
    /// <param name="keepBuildings">Keep the pieces players built.</param>
    /// <param name="ground">Also undo their ground edits.</param>
    /// <param name="label">The history's name for the step.</param>
    public void MarkResets(IReadOnlyCollection<(int X, int Z)> zones, bool keepBuildings, bool ground, string label)
    {
        var marked = Resets.ToDictionary(r => (r.X, r.Z));
        var resets = zones.SelectMany(z => ResetEntries(marked.GetValueOrDefault(z), new ZoneReset(z.X, z.Z, keepBuildings, ground))).ToArray();
        Func<Ground, (List<int>, (int, int, int, int))>? clearGround = !ground ? null : g => ClearZoneGround(g, zones);
        Commit(label, clearGround, [], [], resets);
    }

    /// <summary>
    /// Undoes the ground edits in the zones given: their points go back to the generated ground (the modified flags,
    /// levels, smoothing and lift cleared).
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="zones">The zones (world zone column and row).</param>
    /// <returns>The points changed, and the grid box around them.</returns>
    internal (List<int> Touched, (int, int, int, int) Rect) ClearZoneGround(Ground g, IReadOnlyCollection<(int X, int Z)> zones)
    {
        var touched = new List<int>();
        foreach (var (zx, zz) in zones)
        {
            int gx0 = (zx - g.X0) * 64, gz0 = (zz - g.Z0) * 64;
            for (int k = 0; k <= 64; k++)
            {
                for (int l = 0; l <= 64; l++)
                {
                    int gx = gx0 + l, gz = gz0 + k, p = gz * g.W + gx;
                    if (gx >= 0 && gz >= 0 && gx < g.W && gz < g.H && (g.Mod[p] != 0 || g.PMod[p] != 0 || g.Lift[p] != 0))
                    {
                        g.Mod[p] = g.PMod[p] = 0;
                        g.Level[p] = g.Smooth[p] = g.Lift[p] = 0;
                        touched.Add(p);
                    }
                }
            }
        }
        return (touched, BoxOf(touched));
    }
}
