using System.Numerics;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>
    /// The stairs from a stairwell's first cell down to the middle of its last, against one side, on
    /// stone stacks; a railing where the floor ends.
    /// </summary>
    private static void Stairs(DungeonPlan.Space top, float f, DungeonOut o)
    {
        var d = top.Dir;
        var (x, z) = (d.Z, -d.X);
        float cx = top.CenterX, cz = top.CenterZ, half = top.Deep;
        Vector3 P(float u, float v, float y) => new(cx - d.X * half + x * u + d.X * v, y, cz - d.Z * half + z * u + d.Z * v);
        float low = f - 6, yaw = DungeonOut.YawOf(d);
        for (int st = 0; st < 6; st++)
        {
            float v = 19 - 2 * st;
            o.Add("stone_stair", P(-2.5f, v, low + st), yaw);
            for (int b = 0; b < st; b++)
            {
                o.Add("stone_wall_2x1", P(-2.5f, v, low + b + 0.5f), yaw + 90);
            }
        }
        o.Add("stone_fence", P(-0.3f, 7.4f, f + 0.06f), yaw);
        o.Add("stone_fence", P(1.9f, 7.4f, f + 0.06f), yaw);
    }
}
