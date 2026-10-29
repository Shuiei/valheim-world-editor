using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class Placer
    {
        /// <summary>
        /// The zone a world point is in.
        /// </summary>
        internal static (int X, int Y) ZoneOf((float X, float Z) p) =>
            ((int)MathF.Floor((float)((p.X + 32.0) / 64.0)), (int)MathF.Floor((float)((p.Z + 32.0) / 64.0)));

        /// <summary>
        /// A zone whose corner lies within reach of the middle (and inside the world: the rules' radius, the game's
        /// 10000 m).
        /// </summary>
        internal (int X, int Y) ZoneNearMiddle(float reach)
        {
            int n = (int)reach / 64;
            float radius = _gen.Rules.Radius;
            while (true)
            {
                var zone = (X: Rnd.Range(-n, n), Y: Rnd.Range(-n, n));
                float cx = zone.X * 64f, cy = zone.Y * 64f;
                if (MathF.Sqrt(cx * cx + cy * cy) < radius)
                {
                    return zone;
                }
            }
        }
    }
}
