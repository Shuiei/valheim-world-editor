using System.Numerics;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Helpers of the Path tool tests: a tool with these points, the ground height at a grid point, and applying the path
/// as one step.
/// </summary>
internal static class SharedPathTool
{
    /// <summary>
    /// A Path tool with these points.
    /// </summary>
    internal static PathTool Line(params (float X, float Z)[] pts)
    {
        var p = new PathTool();
        foreach (var (x, z) in pts)
        {
            p.Points.Add(new Vector2(x, z));
        }
        return p;
    }

    /// <summary>
    /// The ground height at grid point (x, z).
    /// </summary>
    internal static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    /// <summary>
    /// Applies the path's action to the ground as one step; returns the zones changed.
    /// </summary>
    internal static List<int> Apply(EditSession s, PathTool p) =>
        s.EditGround("Path", g => { var (t, r, _) = p.Apply(g, s.Brush, s.Scene.Water); return (t, r); });
}
