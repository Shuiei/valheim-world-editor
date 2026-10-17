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
        float cell = MathF.Max(reach, 0) + 1;
        var cells = new Dictionary<(int, int), List<Vector2>>();
        for (int k = 0; k < cave.Curve.Count; k++)
        {
            if (cave.Depth[k] > 0.5f)
            {
                Vector2 p = cave.Curve[k].P;
                var key = ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Y / cell));
                if (!cells.TryGetValue(key, out var list))
                {
                    cells[key] = list = [];
                }
                list.Add(p);
            }
        }
        return at =>
        {
            int cx = (int)MathF.Floor(at.X / cell), cz = (int)MathF.Floor(at.Y / cell);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (cells.TryGetValue((cx + dx, cz + dz), out var list) && list.Exists(p => Vector2.Distance(p, at) <= reach))
                    {
                        return true;
                    }
                }
            }
            return false;
        };
    }
}
