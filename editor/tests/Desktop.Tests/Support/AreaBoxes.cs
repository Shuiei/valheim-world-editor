using System.Numerics;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Area selections made up for the tests.
/// </summary>
internal static class AreaBoxes
{
    /// <summary>
    /// A box selection between two grid corners, with a hard edge.
    /// </summary>
    internal static AreaTool Box(float x0, float z0, float x1, float z1)
    {
        var a = new AreaTool { Soft = 0 };
        a.Points.Add(new Vector2(x0, z0));
        a.Points.Add(new Vector2(x1, z1));
        return a;
    }
}
