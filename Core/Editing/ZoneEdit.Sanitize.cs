namespace TerrainEditor.Editing;

public sealed partial class ZoneEdit
{
    /// <summary>Enforce the game's limits and drop no-op modifications, whatever the browser sent.</summary>
    /// <exception cref="ArgumentException">An array is not the size of the 65 × 65 grid.</exception>
    public void Sanitize()
    {
        if (Modified.Length != EditStore.Cells || Level.Length != EditStore.Cells || Smooth.Length != EditStore.Cells || PaintModified.Length != EditStore.Cells || Paint.Length != EditStore.Cells * 4
            || Lift.Length != EditStore.Cells)
        {
            throw new ArgumentException("Zone data must have 65x65 entries.");
        }
        for (int i = 0; i < EditStore.Cells; i++)
        {
            Level[i] = float.IsFinite(Level[i]) ? Math.Clamp(Level[i], -EditStore.MaxLevel, EditStore.MaxLevel) : 0f;
            Smooth[i] = float.IsFinite(Smooth[i]) ? Math.Clamp(Smooth[i], -EditStore.MaxSmooth, EditStore.MaxSmooth) : 0f;
            Lift[i] = float.IsFinite(Lift[i]) ? Math.Clamp(Lift[i], -EditStore.MaxLift, EditStore.MaxLift) : 0f;
            if (!Modified[i])
            {
                Level[i] = 0f;
                Smooth[i] = 0f;
            }
            for (int c = 0; c < 4; c++)
            {
                float v = Paint[i * 4 + c];
                Paint[i * 4 + c] = float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0f;
            }
        }
    }
}
