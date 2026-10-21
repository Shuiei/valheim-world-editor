
namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Drives a brush stroke over the test area and reads a height back, for the sculpting tests.
/// </summary>
internal static class StrokeRun
{
    /// <summary>
    /// A stroke held for a number of frames of 1/60 s at grid point (x, z).
    /// </summary>
    internal static void Stroke(EditSession s, BrushTool tool, float x, float z, int frames = 30)
    {
        s.BeginStroke(tool, x, z);
        for (int i = 0; i < frames; i++)
        {
            s.StrokeStep(x, z, 1 / 60f);
        }
        s.EndStroke();
    }

    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    internal static float At(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
}
