namespace TerrainEditor.Desktop;

// The per-point work of a sculpt or paint frame: how much of each action a frame applies (GroundOps does the action).
public static partial class Sculpt
{
    /// <summary>What one frame needs for every point: the rate, the heights before it, the paint colour.</summary>
    /// <param name="Rate">The strength times the seconds of the frame.</param>
    /// <param name="Before">The heights from before the frame, over the brush's rectangle and a point round it (Smooth only), or null.</param>
    /// <param name="Colour">The paint the tool moves the ground towards, or null for the sculpt tools.</param>
    internal readonly record struct Frame(float Rate, HeightsBefore? Before, float[]? Colour);

    /// <summary>
    /// Applies the tool to one grid point, by its weight in the brush: a brush held down applies a share of its action
    /// every frame (the rate times the weight), the action itself being the Area and Path tools' (<see cref="GroundOps"/>).
    /// </summary>
    internal static void ApplyPoint(Ground g, Brush b, Stroke s, Frame f, int p, float w)
    {
        float rate = f.Rate;
        switch (s.Tool)
        {
            case BrushTool.Raise:
                s.Clamped |= GroundOps.Move(g, p, 6 * rate * w);
                break;
            case BrushTool.Lower:
                s.Clamped |= GroundOps.Move(g, p, -(6 * rate * w));
                break;
            case BrushTool.Flatten:
                s.Clamped |= GroundOps.Toward(g, p, s.Target ?? b.Target, MathF.Min(1, 8 * rate * w));
                break;
            case BrushTool.Smooth:
                s.Clamped |= GroundOps.Smooth(g, f.Before!, p, MathF.Min(1, 10 * rate * w), GroundOps.BrushSmoothReach);
                break;
            case BrushTool.Natural:
                s.Clamped |= GroundOps.Toward(g, p, NaturalTarget(g, b, s, p), MathF.Min(1, 4 * rate * w));
                break;
            case BrushTool.Restore:
                GroundOps.Restore(g, p, MathF.Min(1, 8 * rate * w), w);
                break;
            default:
                GroundOps.Paint(g, p, f.Colour!, MathF.Min(1, 8 * rate * w));
                break;
        }
    }
}
