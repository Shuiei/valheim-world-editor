
namespace TerrainEditor.Desktop;

// The per-point work of a sculpt or paint frame: each tool changes one grid point's height or paint by its weight.
public static partial class Sculpt
{
    /// <summary>What one frame needs for every point: the rate, the smoothing snapshot and its layout, the paint colour.</summary>
    /// <param name="Rate">The strength times the seconds of the frame.</param>
    /// <param name="Snapshot">The heights from before the frame (Smooth only), or null.</param>
    /// <param name="Sw">The snapshot's row width.</param>
    /// <param name="X0">The snapshot's first grid x (less the one-point margin).</param>
    /// <param name="Z0">The snapshot's first grid z (less the one-point margin).</param>
    /// <param name="Colour">The paint the tool moves the ground towards, or null for the sculpt tools.</param>
    internal readonly record struct Frame(float Rate, float[]? Snapshot, int Sw, int X0, int Z0, float[]? Colour);

    /// <summary>Applies the tool to one grid point, by its weight in the brush.</summary>
    internal static void ApplyPoint(Ground g, Brush b, Stroke s, Frame f, int gx, int gz, int p, float w)
    {
        float h = g.HeightOf(p);
        float rate = f.Rate;
        switch (s.Tool)
        {
            case BrushTool.Raise:
                s.Clamped |= g.SetHeight(p, h + 6 * rate * w);
                break;
            case BrushTool.Lower:
                s.Clamped |= g.SetHeight(p, h - 6 * rate * w);
                break;
            case BrushTool.Flatten:
                s.Clamped |= g.SetHeight(p, h + ((s.Target ?? b.Target) - h) * MathF.Min(1, 8 * rate * w));
                break;
            case BrushTool.Smooth:
                s.Clamped |= g.SetHeight(p, h + (SmoothAverage(f, gx, gz) - h) * MathF.Min(1, 10 * rate * w));
                break;
            case BrushTool.Natural:
                s.Clamped |= g.SetHeight(p, h + (NaturalTarget(g, b, s, p) - h) * MathF.Min(1, 4 * rate * w));
                break;
            case BrushTool.Restore:
                RestorePoint(g, p, rate, w);
                break;
            default:
                PaintPoint(g, p, f.Colour!, MathF.Min(1, 8 * rate * w));
                break;
        }
    }

    /// <summary>Restore: backs the point's edits and lift towards the ground as the world generated it.</summary>
    internal static void RestorePoint(Ground g, int p, float rate, float w)
    {
        float k = MathF.Min(1, 8 * rate * w);
        if (g.Mod[p] != 0)
        {
            g.Level[p] *= 1 - k;
            g.Smooth[p] *= 1 - k;
            if (MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) < 0.01f)
            {
                g.Level[p] = 0;
                g.Smooth[p] = 0;
                g.Mod[p] = 0;
            }
        }
        // Also back from a No limit lift, to the generated ground under saved No limit ground too (the next save takes
        // its ground discs away).
        float bare = g.BareLift(p), lift = g.Lift[p] + (bare - g.Lift[p]) * k;
        g.Lift[p] = MathF.Abs(lift - bare) < 0.01f ? bare : lift;
        if (g.PMod[p] != 0 && w > 0.5f)
        {
            g.PMod[p] = 0;
        }
    }

    /// <summary>The paint tools: moves the point's paint towards the tool's colour by k.</summary>
    internal static void PaintPoint(Ground g, int p, float[] colour, float k)
    {
        g.StartPaint(p);
        for (int c = 0; c < 4; c++)
        {
            g.Paint[p * 4 + c] += (colour[c] - g.Paint[p * 4 + c]) * k;
        }
    }
}
