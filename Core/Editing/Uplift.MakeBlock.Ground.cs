using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>The ground a block makes from a set of discs, with the objects that stay (the raw zone grounds are read once).</summary>
    private sealed class BlockGround
    {
        /// <summary>The block whose points the ground is given for.</summary>
        private readonly Block _block;

        /// <summary>The terrain service (the raw ground per zone).</summary>
        private readonly ValheimGen.TerrainService _terrain;

        /// <summary>The objects that stay as they are (the ones near the block).</summary>
        private readonly List<PlacedObject> _keep;

        /// <summary>The raw ground of each zone, read once.</summary>
        private readonly Dictionary<(int, int), float[]> _raw = [];

        /// <summary>A ground maker for a block.</summary>
        /// <param name="block">The block.</param>
        /// <param name="terrain">The terrain service.</param>
        /// <param name="keep">The objects that stay.</param>
        public BlockGround(Block block, ValheimGen.TerrainService terrain, List<PlacedObject> keep)
        {
            _block = block;
            _terrain = terrain;
            _keep = keep;
        }

        /// <summary>The ground the given discs (with the kept objects) make, per block point.</summary>
        /// <param name="discs">The discs.</param>
        /// <returns>The ground height per block point.</returns>
        public float[] Make(List<Vector3> discs)
        {
            var mods = new TerrainModifiers(_keep.Concat(discs.Select(DiscAt)));
            float[] g = new float[_block.N];
            foreach (var (zx, zz) in _block.Zones)
            {
                if (!_raw.TryGetValue((zx, zz), out float[]? r))
                {
                    r = _raw[(zx, zz)] = _terrain.RawZone(zx, zz);
                }
                float[] h = (float[])r.Clone();
                mods.Apply(zx, zz, h);
                _block.Each(zx, zz, (i, p) => g[p] = h[i]);
            }
            return g;
        }
    }
}
