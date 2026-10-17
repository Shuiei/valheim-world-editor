namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Dirt paint laid over the middle of a zone's ground, or taken off it again, for the Area panel tests that need paint
/// to bring back from a backup.
/// </summary>
internal static class DirtPaint
{
    /// <summary>
    /// Sets (<paramref name="on"/>) or clears dirt paint over grid (5, 5) to (59, 59) as one ground edit.
    /// </summary>
    internal static void Set(EditSession s, bool on) => s.EditGround("paint", g =>
    {
        var touched = new List<int>();
        for (int z = 5; z < 60; z++)
        {
            for (int x = 5; x < 60; x++)
            {
                int p = z * g.W + x;
                g.PMod[p] = (byte)(on ? 1 : 0);
                Array.Copy(on ? Brush.PaintOf(BrushTool.PaintDirt)! : new float[4], 0, g.Paint, p * 4, 4);
                touched.Add(p);
            }
        }
        return (touched, (4, 4, 61, 61));
    });
}
