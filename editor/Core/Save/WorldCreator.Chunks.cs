namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>
    /// The objects of a new world grouped by the base chunk file each goes into, in chunk order. Objects in the
    /// portals' chunk are never added.
    /// </summary>
    internal sealed class ChunkBatch
    {
        /// <summary>The objects of each chunk, by chunk.</summary>
        private readonly SortedDictionary<ushort, List<byte[]>> _byChunk = [];

        /// <summary>How many objects were added (the middle zone's terrain object is counted by its caller).</summary>
        public int Objects { get; set; }

        /// <summary>The chunks with their objects, in chunk order.</summary>
        public IEnumerable<(ushort Chunk, List<byte[]> Objects)> Chunks => _byChunk.Select(p => (p.Key, p.Value));

        /// <summary>Adds an object into its zone's base chunk file (never the portals' chunk).</summary>
        /// <param name="x">The object's x in metres.</param>
        /// <param name="z">The object's z in metres.</param>
        /// <param name="zdo">The object's bytes.</param>
        public void Add(float x, float z, byte[] zdo)
        {
            var (sx, sz) = ChunkMath.SectorOf(x, z);
            ushort chunk = ChunkMath.BaseChunk(sx, sz);
            if (chunk == ChunkMath.PortalChunk)
            {
                return;
            }
            Put(chunk, zdo);
            Objects++;
        }

        /// <summary>Puts an object's bytes into a chunk, without counting it.</summary>
        /// <param name="chunk">The chunk.</param>
        /// <param name="zdo">The object's bytes.</param>
        public void Put(ushort chunk, byte[] zdo) =>
            (_byChunk.TryGetValue(chunk, out var list) ? list : _byChunk[chunk] = []).Add(zdo);
    }
}
