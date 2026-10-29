
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>The crypt arena: columns on a grid, sarcophagi between them, chains and cobwebs above.</summary>
    /// <param name="f">The room's frame.</param>
    private void CryptGrid(Frame f)
    {
        for (float u = -f.HalfA + 3; u <= f.HalfA - 3; u += 4)
        {
            for (float v = -f.HalfD + 3; v <= f.HalfD - 3; v += 4)
            {
                if (Math.Abs(u) < 2.5f && Math.Abs(v) < 2.5f || f.NearDoor(u, v, 3))
                {
                    continue;
                }
                if (MathF.Abs(u) >= f.HalfA - 3.5f || MathF.Abs(v) >= f.HalfD - 3.5f)
                {
                    Column(f, u, v);
                }
                else
                {
                    Put(f, "crypt_skeleton_chest", u, v, R(0, 360), 1.2f);
                }
            }
        }
        Chains(f, 4);
        Webs(f, 0.6f);
    }
}
