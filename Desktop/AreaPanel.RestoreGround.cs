using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// Puts the backup's ground back over the selection's points (inside the soft edge exactly, outside it faded by the
    /// weight). Locked points and those already the same as in the backup are left alone.
    /// </summary>
    /// <param name="session">The session whose Mask and selection apply.</param>
    /// <param name="g">The current ground.</param>
    /// <param name="be">The backup's edits, applied over the block to get its ground.</param>
    /// <returns>The points changed, and the box around the selection's points (one point wider all round).</returns>
    private (List<int> Touched, (int, int, int, int) Box) RestoreGround(EditSession session, Ground g, EditStore be)
    {
        // The backup's ground edits over the block.
        var bk = new Ground(g.W, g.H, g.X0, g.Z0, g.ZonesX, g.ZonesZ);
        Array.Copy(g.Base, bk.Base, g.Base.Length);
        bk.TakeEdits(be);
        var touched = new List<int>();
        if (Area.WeightsIn(g.W, g.H, session.MaskNow()) is not { } a)
        {
            return (touched, (0, 0, 0, 0));
        }
        foreach (var (p, w) in a.Cells)
        {
            if (g.Locked(p % g.W, p / g.W) || SameGroundPoint(g, bk, p))
            {
                continue;
            }
            if (w > 0.999f)
            {
                // Inside the soft edge: exactly as in the backup.
                CopyGroundPoint(g, bk, p);
            }
            else
            {
                BlendGroundPoint(g, bk, p, w);
            }
            touched.Add(p);
        }
        return (touched, (a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1));
    }

    /// <summary>Whether a ground point is the same now as in the backup (height, original, edits and paint).</summary>
    /// <param name="g">The current ground.</param>
    /// <param name="bk">The backup's ground.</param>
    /// <param name="p">The point's index.</param>
    private static bool SameGroundPoint(Ground g, Ground bk, int p) => g.Mod[p] == bk.Mod[p] && g.PMod[p] == bk.PMod[p] && MathF.Abs(g.HeightOf(p) - bk.HeightOf(p)) < 1e-4f && MathF.Abs(g.Original(p) - bk.Original(p)) < 1e-4f
        && (g.PMod[p] == 0 || Enumerable.Range(0, 4).All(c => MathF.Abs(g.Paint[p * 4 + c] - bk.Paint[p * 4 + c]) < 1e-4f));

    /// <summary>Takes the backup's edits of a point outright (inside the soft edge).</summary>
    /// <param name="g">The current ground, changed.</param>
    /// <param name="bk">The backup's ground.</param>
    /// <param name="p">The point's index.</param>
    private static void CopyGroundPoint(Ground g, Ground bk, int p)
    {
        g.Mod[p] = bk.Mod[p];
        g.Level[p] = bk.Level[p];
        g.Smooth[p] = bk.Smooth[p];
        g.PMod[p] = bk.PMod[p];
        Array.Copy(bk.Paint, p * 4, g.Paint, p * 4, 4);
        // The backup's original ground (its own ground discs) as a lift on this one's.
        g.Lift[p] = bk.Original(p) - g.Base[p];
    }

    /// <summary>Moves a point part of the way to the backup's height and paint, by its weight (the soft edge).</summary>
    /// <param name="g">The current ground, changed.</param>
    /// <param name="bk">The backup's ground.</param>
    /// <param name="p">The point's index.</param>
    /// <param name="w">The weight: how far towards the backup.</param>
    private static void BlendGroundPoint(Ground g, Ground bk, int p, float w)
    {
        float h = g.HeightOf(p);
        g.SetHeight(p, h + (bk.HeightOf(p) - h) * w);
        if (bk.PMod[p] != 0 || g.PMod[p] != 0)
        {
            if (g.PMod[p] == 0)
            {
                g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
                g.Paint[p * 4 + 3] = 1;
                g.PMod[p] = 1;
            }
            for (int c = 0; c < 4; c++)
            {
                float target = bk.PMod[p] != 0 ? bk.Paint[p * 4 + c] : c == 3 ? 1 : 0;
                g.Paint[p * 4 + c] += (target - g.Paint[p * 4 + c]) * w;
            }
        }
    }
}
