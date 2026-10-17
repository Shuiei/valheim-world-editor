using System.Numerics;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>
    /// The turn (degrees about the vertical) the game gives a location it spawns: along the slope under it when its
    /// kind asks (facing across the ground's normal, Quaternion.LookRotation of the normal crossed with up), else a
    /// random multiple of 22.5° from the zone's seed when its kind asks (the zone's first draw), else none. Rolled on
    /// the shared Unity random, which the caller puts back.
    /// </summary>
    private static float Yaw(ValheimGen.SeedLocations.Rule rule, ValheimGen.TerrainService terrain, Vector3 at, int zoneSeed)
    {
        if (rule.SlopeRotation != 0)
        {
            Vector3 n = Normal(terrain, at.X, at.Z);
            return MathF.Atan2(-n.Z, n.X) * 180f / MathF.PI;
        }
        if (rule.RandomRotation != 0)
        {
            Rnd.InitState(zoneSeed);
            return Rnd.Range(0, 16) * 22.5f;
        }
        return 0f;
    }

    /// <summary>The ground's normal at a world point, from the generator's heights a metre apart around it.</summary>
    private static Vector3 Normal(ValheimGen.TerrainService terrain, float x, float z)
    {
        float H(float wx, float wz)
        {
            var (zx, zz) = ChunkMath.SectorOf(wx, wz);
            float[] h = terrain.BaseZone(zx, zz);
            int ix = Math.Clamp((int)MathF.Round(wx - (zx * 64f - 32f)), 0, 64), iz = Math.Clamp((int)MathF.Round(wz - (zz * 64f - 32f)), 0, 64);
            return h[iz * 65 + ix];
        }
        return Vector3.Normalize(new Vector3(H(x - 1f, z) - H(x + 1f, z), 2f, H(x, z - 1f) - H(x, z + 1f)));
    }

    /// <summary>Prefab hash of the game's per-zone terrain compiler object.</summary>
    private static readonly int TerrainCompiler = StableHash.Of("_TerrainCompiler");
}
