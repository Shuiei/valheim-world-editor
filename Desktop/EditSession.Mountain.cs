using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Mountain tool: a mountain put into the ground, clearing the natural objects it buries.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Puts a mountain into the ground around grid point (cx, cz) (Mountain tool), past the game's ±8 m (saving turns
    /// it into ground discs). One undo step.
    /// </summary>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="m">The mountain's shape.</param>
    /// <param name="clear">Whether the natural objects where the ground rises more than Uplift.ClearHeight go (saving
    /// would take them away anyway).</param>
    /// <param name="label">The history's name for the step.</param>
    /// <returns>How many ground points changed and how many objects were cleared; null when the mountain does not fit
    /// in the open area.</returns>
    public (int Touched, int Cleared)? Mountain(float cx, float cz, MountainSpec m, bool clear, string label)
    {
        var g = Ground;
        float reach = m.Reach;
        if (cx - reach < 1 || cz - reach < 1 || cx + reach > g.W - 2 || cz + reach > g.H - 2)
        {
            return null;
        }
        var shape = Desktop.Mountain.Shape(m);
        var mask = MaskNow();
        var remove = clear ? ObjectsToClear(cx, cz, reach, shape, mask) : [];
        int touchedCount = 0;
        Commit(label, gr =>
        {
            var (touched, rect) = RaiseGround(gr, cx, cz, reach, shape, mask);
            touchedCount = touched.Count;
            return (touched, rect);
        }, remove, []);
        return (touchedCount, remove.Count);
    }

    /// <summary>
    /// The natural objects a mountain's ground would bury: those inside its reach where the ground rises more than
    /// Uplift.ClearHeight. Under the scene's things lock.
    /// </summary>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="reach">The mountain's reach in grid points.</param>
    /// <param name="shape">The mountain's height at a point, from its middle.</param>
    /// <param name="mask">The mask over the ground, or null when off.</param>
    /// <returns>The indices of the things to take away.</returns>
    private List<int> ObjectsToClear(float cx, float cz, float reach, Func<float, float, float> shape, Func<int, float>? mask)
    {
        var g = Ground;
        float ox = Scene.OriginX, oz = Scene.OriginZ;
        var remove = new List<int>();
        lock (Scene.Things)
        {
            for (int i = 0; i < Scene.Things.Count; i++)
            {
                var t = Scene.Things[i];
                float gx = t.Position.X - ox, gz = t.Position.Z - oz;
                if (!t.Gone && Natural(t) && MathF.Abs(gx - cx) <= reach && MathF.Abs(gz - cz) <= reach
                    && shape(gx - cx, gz - cz) * (mask?.Invoke((int)MathF.Round(gz) * g.W + (int)MathF.Round(gx)) ?? 1) > Uplift.ClearHeight)
                {
                    remove.Add(i);
                }
            }
        }
        return remove;
    }
}
