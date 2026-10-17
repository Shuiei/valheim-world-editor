using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>Solves the disc heights of a block against the ground asked for, and measures how far a ground misses it.</summary>
    private sealed class DiscHeights
    {
        /// <summary>The block worked on.</summary>
        private readonly Block _block;

        /// <summary>The ground asked for and the lift per point.</summary>
        private readonly BlockTargets _targets;

        /// <summary>The points where the ground may be changed by a disc.</summary>
        private readonly bool[] _region;

        /// <summary>The discs' heights are stored here.</summary>
        private readonly DiscNodes _nodes;

        /// <summary>A height solver for a block.</summary>
        /// <param name="block">The block.</param>
        /// <param name="targets">The ground asked for and the lift.</param>
        /// <param name="region">The points a disc may change.</param>
        /// <param name="nodes">Where the solved heights are stored.</param>
        public DiscHeights(Block block, BlockTargets targets, bool[] region, DiscNodes nodes)
        {
            _block = block;
            _targets = targets;
            _region = region;
            _nodes = nodes;
        }

        /// <summary>Solves each disc's height against the residual ground, a disc at a time, and stores the heights.</summary>
        /// <param name="discs">The discs (their heights are updated in place).</param>
        /// <param name="ground">The ground the discs make now.</param>
        public void Fit(List<Vector3> discs, float[] ground)
        {
            // The game's ground is a weighted mix of the disc heights (each disc levels its points to its
            // height, then blends the ring around it towards it), in the game's order: the disc heights
            // are fitted to it by least squares, a disc at a time.
            var columns = Weights(_block, discs);
            // Each disc stays within the heights asked for around it: one mostly covered by others would
            // otherwise go far up or down for its few points, and its new height changes the game's order.
            var range = discs.Select(WantedRange).ToList();
            float[] r = new float[_block.N];
            foreach (int p in _block.Points)
            {
                r[p] = _targets.Want[p] - ground[p];
            }
            for (int sweep = 0; sweep < Sweeps; sweep++)
            {
                for (int k = 0; k < discs.Count; k++)
                {
                    double num = 0, den = Damping;
                    foreach (var (p, w) in columns[k])
                    {
                        num += w * r[p];
                        den += w * w;
                    }
                    float d = Math.Clamp((float)(Step * num / den), range[k].Lo - discs[k].Y, range[k].Hi - discs[k].Y);
                    foreach (var (p, w) in columns[k])
                    {
                        r[p] -= w * d;
                    }
                    discs[k] += new Vector3(0, d, 0);
                }
            }
            foreach (var d in discs)
            {
                _nodes.Set((int)d.X, (int)d.Z, d.Y);
            }
        }

        /// <summary>The squared error of the ground against the wanted one, where edits or the lift matter (edits cannot make up big misses, so they count more).</summary>
        /// <param name="g">The ground per block point.</param>
        /// <returns>The error.</returns>
        public double Miss(float[] g)
        {
            double m = 0;
            foreach (int p in _block.Points)
            {
                if (_region[p] || _targets.Lift[p] != 0)
                {
                    // What the edits cannot make up counts most.
                    float r = MathF.Abs(_targets.Want[p] - g[p]);
                    m += r * r + (r > EditStore.MaxLevel ? 100 * (r - EditStore.MaxLevel) * (r - EditStore.MaxLevel) : 0);
                }
            }
            return m;
        }

        /// <summary>The lowest and highest ground asked for within a disc's reach.</summary>
        /// <param name="disc">The disc's centre.</param>
        /// <returns>The lowest and highest wanted height.</returns>
        private (float Lo, float Hi) WantedRange(Vector3 disc)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            _block.Disc(disc, DiscRadius, p => { lo = MathF.Min(lo, _targets.Want[p]); hi = MathF.Max(hi, _targets.Want[p]); });
            return (lo, hi);
        }
    }
}
