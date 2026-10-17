
namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>
    /// The heights of the ground before an action, over the selection's box and the reach of its blur round it, for the
    /// blur of Smooth and Naturalize (none for the other actions).
    /// </summary>
    /// <param name="G">The ground the heights were read from.</param>
    /// <param name="Heights">The heights, row by row over the box.</param>
    /// <param name="X0">The box's west grid point.</param>
    /// <param name="Z0">The box's south grid point.</param>
    /// <param name="W">The box's width in grid points.</param>
    internal sealed record HeightSnapshot(Ground G, float[] Heights, int X0, int Z0, int W)
    {
        /// <summary>Reads the heights before an action.</summary>
        /// <param name="g">The ground.</param>
        /// <param name="a">The selection's weights.</param>
        /// <param name="reach">How far round the box the blur reads (every point it reads is kept); below zero: none.</param>
        /// <returns>The heights taken.</returns>
        public static HeightSnapshot Take(Ground g, Weights a, int reach)
        {
            if (reach < 0)
            {
                return new HeightSnapshot(g, [], 0, 0, 0);
            }
            int bx0 = Math.Max(0, a.X0 - reach), bz0 = Math.Max(0, a.Z0 - reach);
            int bx1 = Math.Min(g.W - 1, a.X1 + reach), bz1 = Math.Min(g.H - 1, a.Z1 + reach);
            int bw = bx1 - bx0 + 1;
            float[] heights = new float[bw * (bz1 - bz0 + 1)];
            for (int z = bz0; z <= bz1; z++)
            {
                for (int x = bx0; x <= bx1; x++)
                {
                    heights[(z - bz0) * bw + x - bx0] = g.HeightOf(z * g.W + x);
                }
            }
            return new HeightSnapshot(g, heights, bx0, bz0, bw);
        }

        /// <summary>The average of the heights before the action within r grid points of a point (the square round it, inside the ground).</summary>
        /// <param name="p">The grid point.</param>
        /// <param name="r">The blur's reach, in grid points.</param>
        /// <returns>The blurred height.</returns>
        public float Blur(int p, int r)
        {
            int gx = p % G.W, gz = p / G.W, n = 0;
            float s = 0;
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = gx + dx, z = gz + dz;
                    if (x >= 0 && z >= 0 && x < G.W && z < G.H)
                    {
                        s += Heights[(z - Z0) * W + x - X0];
                        n++;
                    }
                }
            }
            return s / n;
        }
    }
}
