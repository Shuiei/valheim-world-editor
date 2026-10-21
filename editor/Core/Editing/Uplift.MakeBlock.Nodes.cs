using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>The disc heights of a block, by their metre position, and where new discs go next.</summary>
    internal sealed class DiscNodes
    {
        /// <summary>The block worked on.</summary>
        private readonly Block _block;

        /// <summary>The ground asked for and the lift per point.</summary>
        private readonly BlockTargets _targets;

        /// <summary>The points where a disc may be placed (a disc is only placed next to open points).</summary>
        private readonly bool[] _region;

        /// <summary>The disc heights by their metre position.</summary>
        private readonly Dictionary<(int, int), float> _heights = [];

        /// <summary>The discs placed so far, by metre position.</summary>
        /// <param name="block">The block.</param>
        /// <param name="targets">The ground asked for and the lift.</param>
        /// <param name="region">The points a disc may change.</param>
        public DiscNodes(Block block, BlockTargets targets, bool[] region)
        {
            _block = block;
            _targets = targets;
            _region = region;
        }

        /// <summary>How many discs there are.</summary>
        public int Count => _heights.Count;

        /// <summary>Stores a disc's height at its metre position.</summary>
        /// <param name="x">The disc's x.</param>
        /// <param name="z">The disc's z.</param>
        /// <param name="height">The disc's height.</param>
        public void Set(int x, int z, float height) => _heights[(x, z)] = height;

        /// <summary>The current discs, one per node.</summary>
        /// <returns>The discs' positions with their heights.</returns>
        public List<Vector3> Discs() => [.. _heights.Select(n => new Vector3(n.Key.Item1, n.Value, n.Key.Item2))];

        /// <summary>Adds a disc at each metre-grid node within a disc's reach of the point that has an open ground height.</summary>
        /// <param name="p">The block point.</param>
        public void AddNear(int p)
        {
            var (wx, wz) = _block.World(p);
            int r = (int)DiscRadius;
            for (int nz = (int)Math.Floor((wz - r) / (double)Spacing); nz <= (int)Math.Ceiling((wz + r) / (double)Spacing); nz++)
            {
                for (int nx = (int)Math.Floor((wx - r) / (double)Spacing); nx <= (int)Math.Ceiling((wx + r) / (double)Spacing); nx++)
                {
                    int cx = nx * Spacing, cz = nz * Spacing;
                    if ((cx - wx) * (cx - wx) + (cz - wz) * (cz - wz) <= DiscRadius * DiscRadius && !_heights.ContainsKey((cx, cz)) && _block.At(new Vector3(cx, 0, cz)) is int q && _targets.Open[q])
                    {
                        _heights[(cx, cz)] = _targets.Want[q];
                    }
                }
            }
        }

        /// <summary>Adds discs near every open point of the region whose ground is still off by more than the need.</summary>
        /// <param name="ground">The ground the discs make now.</param>
        public void AddWhereMissing(float[] ground)
        {
            foreach (int p in _block.Points)
            {
                if (_region[p] && _targets.Open[p] && MathF.Abs(_targets.Want[p] - ground[p]) > Need)
                {
                    AddNear(p);
                }
            }
        }
    }
}
