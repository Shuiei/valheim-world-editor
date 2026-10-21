using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Taking a change's ground and paint back off the points it changed, as a removal does.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Takes a change's height, smoothing, lift and paint off the ground points it changed: each value moves back by the
    /// change's own delta, the flags follow the change's marks where a later step did not move them. Under the lock.
    /// </summary>
    /// <param name="c">The change taken out.</param>
    internal void RevertPoints(Change c)
    {
        var g = Ground;
        for (int k = 0; k < c.Points.Length; k++)
        {
            int p = c.Points[k];
            g.Level[p] = Math.Clamp(g.Level[p] + c.Before.Level[k] - c.After.Level[k], -EditStore.MaxLevel, EditStore.MaxLevel);
            g.Smooth[p] = Math.Clamp(g.Smooth[p] + c.Before.Smooth[k] - c.After.Smooth[k], -EditStore.MaxSmooth, EditStore.MaxSmooth);
            // A later step that changed the point's modified flag keeps it (a flatten back to the original height
            // leaves it set with no level).
            if (g.Mod[p] == c.After.Mod[k])
            {
                g.Mod[p] = (byte)(MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) > 1e-4f || c.Before.Mod[k] != 0 && g.Mod[p] != 0 ? 1 : 0);
            }
            g.Lift[p] = Math.Clamp(g.Lift[p] + c.Before.Lift[k] - c.After.Lift[k], -EditStore.MaxLift, EditStore.MaxLift);
            RevertPaint(c, k, p);
        }
    }

    /// <summary>
    /// Takes a change's paint back off one ground point, when nothing repainted the point since the change: its paint
    /// flag and its four paint values go back to what they were before the change.
    /// </summary>
    /// <param name="c">The change taken out.</param>
    /// <param name="k">The change's index for the point (its values are in the change's order).</param>
    /// <param name="p">The point's index in the ground.</param>
    internal void RevertPaint(Change c, int k, int p)
    {
        var g = Ground;
        bool samePaint = g.PMod[p] == c.After.PMod[k];
        for (int ch = 0; ch < 4 && samePaint; ch++)
        {
            samePaint = MathF.Abs(g.Paint[p * 4 + ch] - c.After.Paint[k * 4 + ch]) < 1e-4f;
        }
        if (samePaint)
        {
            g.PMod[p] = c.Before.PMod[k];
            Array.Copy(c.Before.Paint, k * 4, g.Paint, p * 4, 4);
        }
    }
}
