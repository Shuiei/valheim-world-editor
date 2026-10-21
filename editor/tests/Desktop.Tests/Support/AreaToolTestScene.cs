using System.Numerics;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The area selections and the grid heights the Area tool tests work from: a box between two grid corners, and the height
/// at a grid point of the area's scene.
/// </summary>
internal static class AreaToolTestScene
{
    /// <summary>
    /// A box selection between two grid corners, with this soft edge.
    /// </summary>
    internal static AreaTool Box(float x0, float z0, float x1, float z1, float soft = 0)
    {
        var a = new AreaTool { Soft = soft };
        a.Points.Add(new Vector2(x0, z0));
        a.Points.Add(new Vector2(x1, z1));
        return a;
    }

    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    internal static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
}
