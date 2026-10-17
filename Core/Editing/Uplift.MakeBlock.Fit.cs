using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>
    /// Fits the disc heights of one block to the ground asked for: discs are added where the ground is still off,
    /// their heights are solved by least squares, and the round whose real ground misses least is kept.
    /// </summary>
    private sealed class BlockFit
    {
        /// <summary>The block worked on.</summary>
        private readonly Block _block;

        /// <summary>The ground asked for and the lift per point.</summary>
        private readonly BlockTargets _targets;

        /// <summary>The points where the ground may be changed by a disc.</summary>
        private readonly bool[] _region;

        /// <summary>The ground the discs make.</summary>
        private readonly BlockGround _ground;

        /// <summary>The discs.</summary>
        private readonly DiscNodes _nodes;

        /// <summary>The height solver.</summary>
        private readonly DiscHeights _heights;

        /// <summary>A block to fit, with what it keeps and the points a disc may change.</summary>
        /// <param name="block">The block.</param>
        /// <param name="terrain">The terrain service.</param>
        /// <param name="targets">The ground asked for and the lift.</param>
        /// <param name="keep">The objects that stay.</param>
        /// <param name="region">The points a disc may change.</param>
        public BlockFit(Block block, ValheimGen.TerrainService terrain, BlockTargets targets, List<PlacedObject> keep, bool[] region)
        {
            _block = block;
            _targets = targets;
            _region = region;
            _ground = new BlockGround(block, terrain, keep);
            _nodes = new DiscNodes(block, targets, region);
            _heights = new DiscHeights(block, targets, region, _nodes);
        }

        /// <summary>Fits the discs and returns them with the ground they make (the bare ground when there are none).</summary>
        /// <returns>The discs' positions and heights, and the ground.</returns>
        public (List<Vector3> Final, float[] Ground) Run()
        {
            float[] bare = _ground.Make([]);
            bool[] need = new bool[_block.N];
            foreach (int p in _block.Points)
            {
                need[p] = _region[p] && _targets.Open[p] && MathF.Abs(_targets.Want[p] - bare[p]) > Need;
            }
            foreach (int p in _block.Points)
            {
                if (need[p])
                {
                    _nodes.AddNear(p);
                }
            }

            // The game's order changes with the heights, which the fit does not see: each round's discs are
            // checked against the real ground, and the best kept.
            List<Vector3>? best = null;
            double bestMiss = double.MaxValue;
            for (int round = 0; round < Rounds && _nodes.Count > 0; round++)
            {
                var discs = _nodes.Discs();
                float[] ground = _ground.Make(discs);
                _heights.Fit(discs, ground);
                ground = _ground.Make(_nodes.Discs());
                double miss = _heights.Miss(ground);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    best = _nodes.Discs();
                }
                if (round < Rounds - 1)
                {
                    // Still too far off out of the discs' reach (at a disc's blended edge): one more disc there.
                    int before = _nodes.Count;
                    _nodes.AddWhereMissing(ground);
                    if (_nodes.Count == before && round >= 2)
                    {
                        break;
                    }
                }
            }
            var final = best ?? _nodes.Discs();
            return (final, final.Count > 0 ? _ground.Make(final) : bare);
        }
    }
}
