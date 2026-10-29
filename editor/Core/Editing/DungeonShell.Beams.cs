using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>Beams under the roof across a room, every 4 m, over the room's own tiles only (not what its shape cuts away).</summary>
    private static void Beams(DungeonPlan.Space sp, float f, Material mat, DungeonOut o)
    {
        float top = f + DungeonPlan.HeightOf(sp) - mat.BeamTop;
        bool acrossX = sp.W <= sp.H; // beams span the narrower way
        float x0 = 2 * sp.X0, x1 = 2 * (sp.X0 + sp.W), z0 = 2 * sp.Z0, z1 = 2 * (sp.Z0 + sp.H);
        bool Over(float x, float z) => sp.Has(((int)MathF.Floor(x / 2), (int)MathF.Floor(z / 2)));
        if (acrossX)
        {
            for (float z = z0 + 4; z < z1 - 1; z += 4)
            {
                for (float x = x0 + 2; x < x1 - 1; x += 4)
                {
                    if (Over(x - 1.5f, z - 0.5f) && Over(x + 1.5f, z - 0.5f) && Over(x - 1.5f, z + 0.5f) && Over(x + 1.5f, z + 0.5f))
                    {
                        o.Add(mat.Beam!, new Vector3(x, top, z), 0);
                    }
                }
            }
        }
        else
        {
            for (float x = x0 + 4; x < x1 - 1; x += 4)
            {
                for (float z = z0 + 2; z < z1 - 1; z += 4)
                {
                    if (Over(x - 0.5f, z - 1.5f) && Over(x - 0.5f, z + 1.5f) && Over(x + 0.5f, z - 1.5f) && Over(x + 0.5f, z + 1.5f))
                    {
                        o.Add(mat.Beam!, new Vector3(x, top, z), 90);
                    }
                }
            }
        }
    }
}
