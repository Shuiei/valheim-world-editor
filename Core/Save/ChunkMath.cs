namespace TerrainEditor.Save;

/// <summary>
/// Which chunk file an object belongs to (ZoneSystem / ZDOMan.GetSaveClonePerChunk): the world is 512 x 512 sectors
/// of 64 m; a base chunk covers 8 x 8 sectors, and a chunk of size s merges 2^s x 2^s base chunks
/// (ZoneSystem.s_sizeFilters clears the low s bits of x and y).
/// </summary>
public static class ChunkMath
{
    /// <summary>Bit masks per chunk size 0..4 that clear the low bits of a base chunk's x and y bytes.</summary>
    private static readonly ushort[] SizeFilters = [65535, 65278, 64764, 63736, 61680];

    /// <summary>The 64 m sector (zone) holding a world position; sectors are centred on multiples of 64 m.</summary>
    /// <param name="x">World x in metres.</param>
    /// <param name="z">World z in metres.</param>
    public static (int X, int Z) SectorOf(float x, float z) => ((int)Math.Floor((x + 32f) / 64f), (int)Math.Floor((z + 32f) / 64f));

    /// <summary>
    /// The base chunk id of a sector: x / 8 in the low byte and z / 8 in the high byte, after shifting sectors to
    /// 0..511.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The sector is outside the world.</exception>
    public static ushort BaseChunk(int sectorX, int sectorZ)
    {
        uint ix = (uint)(sectorX + 256), iz = (uint)(sectorZ + 256);
        if (ix >= 512 || iz >= 512)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorX), "Position is outside the world.");
        }
        return (ushort)(ix / 8 + ((iz / 8) << 8));
    }

    /// <summary>A sector inside the world's chunk grid (±256 zones, about ±16 km).</summary>
    public static bool InWorld(int sectorX, int sectorZ) => (uint)(sectorX + 256) < 512 && (uint)(sectorZ + 256) < 512;

    /// <summary>
    /// The chunk file (from the current index) whose area contains the sector, if any (none outside the world).
    /// </summary>
    public static ChunkFile? Find(IEnumerable<ChunkFile> chunks, int sectorX, int sectorZ)
    {
        if (!InWorld(sectorX, sectorZ))
        {
            return null;
        }
        ushort c = BaseChunk(sectorX, sectorZ);
        return chunks.FirstOrDefault(f => f.Size < SizeFilters.Length && (ushort)(c & SizeFilters[f.Size]) == f.Chunk && f.Chunk != PortalChunk);
    }

    /// <summary>
    /// <see cref="Find"/> for many sectors of one index: each base chunk's file is looked up once, then remembered.
    /// </summary>
    public static Func<int, int, ChunkFile?> Finder(IReadOnlyList<ChunkFile> chunks)
    {
        Dictionary<ushort, ChunkFile?> found = [];
        return (sectorX, sectorZ) =>
        {
            if (!InWorld(sectorX, sectorZ))
            {
                return null;
            }
            ushort c = BaseChunk(sectorX, sectorZ);
            return found.TryGetValue(c, out ChunkFile? f) ? f : found[c] = Find(chunks, sectorX, sectorZ);
        };
    }

    /// <summary>ZoneSystem.ChunkPortal: the special chunk holding portals (never a terrain object's home).</summary>
    public const ushort PortalChunk = 0;
}
