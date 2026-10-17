
namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>
    /// What an action does to one grid point: the ground, the brush, the action's numbers, the heights before the action
    /// (for the blur of Smooth and Naturalize) and Naturalize's blur reach.
    /// </summary>
    /// <param name="G">The ground to change.</param>
    /// <param name="B">The brush: Naturalize's bumps and size, Erode's rest angle and random.</param>
    /// <param name="Act">The action.</param>
    /// <param name="Height">Flatten's height, in metres.</param>
    /// <param name="Amount">Raise and Lower's amount, in metres.</param>
    /// <param name="Paint">The colour (four weights, as the paint brushes put down).</param>
    /// <param name="Before">The heights before the action.</param>
    /// <param name="NaturalReach">Naturalize's blur reach, in grid points.</param>
    internal sealed record CellChange(Ground G, Brush B, GroundAction Act, float Height, float Amount, float[] Paint, HeightSnapshot Before, int NaturalReach)
    {
        /// <summary>Changes one grid point by its weight, as the action does.</summary>
        /// <param name="p">The grid point (index z * width + x).</param>
        /// <param name="w">The point's weight, 0 to 1 (the soft edge).</param>
        /// <returns>Whether the point reached the game's ±8 m limit.</returns>
        public bool Change(int p, float w)
        {
            float h = G.HeightOf(p);
            int gx = p % G.W, gz = p / G.W;
            switch (Act)
            {
                case GroundAction.Flatten:
                    return G.SetHeight(p, h + (Height - h) * w);
                case GroundAction.Raise:
                    return G.SetHeight(p, h + Amount * w);
                case GroundAction.Lower:
                    return G.SetHeight(p, h - Amount * w);
                case GroundAction.Smooth:
                    return G.SetHeight(p, h + (Before.Blur(p, 3) - h) * w);
                case GroundAction.Natural:
                    return G.SetHeight(p, h + (Before.Blur(p, NaturalReach) + B.NoiseAmp * B.Noise.Fbm((G.OriginX + gx) / B.NoiseSize, (G.OriginZ + gz) / B.NoiseSize) - h) * w);
                case GroundAction.Restore:
                    RestorePoint(p, w);
                    return false;
                case GroundAction.Paint:
                    PaintPoint(p, w);
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>Takes a point back towards the generated ground, its paint and its lift too (as the Restore brush does).</summary>
        /// <param name="p">The grid point.</param>
        /// <param name="w">The point's weight.</param>
        internal void RestorePoint(int p, float w)
        {
            if (G.Mod[p] != 0)
            {
                G.Level[p] *= 1 - w;
                G.Smooth[p] *= 1 - w;
                if (w > 0.98f || MathF.Abs(G.Level[p]) + MathF.Abs(G.Smooth[p]) < 0.01f)
                {
                    G.Level[p] = G.Smooth[p] = 0;
                    G.Mod[p] = 0;
                }
            }
            // Also back down from a No limit lift (a mountain, a deep paste), as the Restore brush.
            G.Lift[p] = w > 0.98f || MathF.Abs(G.Lift[p] * (1 - w)) < 0.01f ? 0 : G.Lift[p] * (1 - w);
            if (G.PMod[p] != 0 && w > 0.5f)
            {
                G.PMod[p] = 0;
            }
        }

        /// <summary>Paints a point with the colour, by its weight.</summary>
        /// <param name="p">The grid point.</param>
        /// <param name="w">The point's weight.</param>
        internal void PaintPoint(int p, float w)
        {
            G.StartPaint(p);
            for (int c = 0; c < 4; c++)
            {
                G.Paint[p * 4 + c] += (Paint[c] - G.Paint[p * 4 + c]) * w;
            }
        }
    }
}
