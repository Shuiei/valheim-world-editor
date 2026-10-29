using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// A test of whether a point (grid metres) is within <paramref name="reach"/> of a point of the cave's line
    /// deeper than half a metre. The deep points are put once in square cells a metre wider than the reach, so a test
    /// looks only at the points of its own cell and the eight around it.
    /// </summary>
    /// <param name="cave">The cave worked out before the ground changes.</param>
    /// <param name="reach">How far from the line, metres.</param>
    internal static Func<Vector2, bool> NearDeep(CavePlan cave, float reach)
    {
        var index = new CellNeighbourIndex<Vector2>(MathF.Max(reach, 0) + 1);
        for (int k = 0; k < cave.Curve.Count; k++)
        {
            if (cave.Depth[k] > 0.5f)
            {
                Vector2 p = cave.Curve[k].P;
                index.Add(p.X, p.Y, p);
            }
        }
        return at => index.AnyNear(at.X, at.Y, p => Vector2.Distance(p, at) <= reach);
    }
}
