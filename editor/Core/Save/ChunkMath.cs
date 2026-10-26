namespace TerrainEditor.Save;

/// <summary>
/// Which chunk file an object belongs to (ZoneSystem / ZDOMan.GetSaveClonePerChunk): the world is a grid of sectors of
/// 64 m, 512 x 512 in the game (<see cref="VanillaWidth"/>; up to 2048 x 2048 with Expand World Size, which the world
/// file says: <see cref="WorldSave.GridWidth"/>); a base chunk covers 8 x 8 sectors, and a chunk of size s merges
/// 2^s x 2^s base chunks (ZoneSystem.s_sizeFilters clears the low s bits of x and y). A chunk's id is a byte per axis,
/// which is why 2048 sectors (256 base chunks) is the most a grid can have.
/// </summary>
public static class ChunkMath
{
    /// <summary>The game's grid: 512 sectors a side, ±256 zones, about ±16 km.</summary>
    public const int VanillaWidth = 512;

    /// <summary>The widest grid the chunk ids can name: 2048 sectors a side, about ±65 km.</summary>
    public const int MaxWidth = 2048;

    /// <summary>Bit masks per chunk size 0..4 that clear the low bits of a base chunk's x and y bytes.</summary>
    private static readonly ushort[] SizeFilters = [65535, 65278, 64764, 63736, 61680];

    /// <summary>The 64 m sector (zone) holding a world position; sectors are centred on multiples of 64 m.</summary>
    /// <param name="x">World x in metres.</param>
    /// <param name="z">World z in metres.</param>
    public static (int X, int Z) SectorOf(float x, float z) => ((int)Math.Floor((x + 32f) / 64f), (int)Math.Floor((z + 32f) / 64f));

    /// <summary>
    /// The base chunk id of a sector: x / 8 in the low byte and z / 8 in the high byte, after shifting sectors to
    /// 0..width-1.
    /// </summary>
    /// <param name="sectorX">The sector's x.</param>
    /// <param name="sectorZ">The sector's z.</param>
    /// <param name="width">The grid's sectors a side (the game's 512).</param>
    /// <exception cref="ArgumentOutOfRangeException">The sector is outside the world.</exception>
    public static ushort BaseChunk(int sectorX, int sectorZ, int width = VanillaWidth)
    {
        if (!InWorld(sectorX, sectorZ, width))
        {
            throw new ArgumentOutOfRangeException(nameof(sectorX), "Position is outside the world.");
        }
        uint ix = (uint)(sectorX + width / 2), iz = (uint)(sectorZ + width / 2);
        return (ushort)(ix / 8 + ((iz / 8) << 8));
    }

    /// <summary>A sector inside the world's chunk grid (±width/2 zones: ±256 zones, about ±16 km, in the game).</summary>
    /// <param name="sectorX">The sector's x.</param>
    /// <param name="sectorZ">The sector's z.</param>
    /// <param name="width">The grid's sectors a side (the game's 512).</param>
    public static bool InWorld(int sectorX, int sectorZ, int width = VanillaWidth) => (uint)(sectorX + width / 2) < (uint)width && (uint)(sectorZ + width / 2) < (uint)width;

    /// <summary>
    /// The chunk file (from the current index) whose area contains the sector, if any (none outside the world).
    /// </summary>
    /// <param name="chunks">The save's chunk files.</param>
    /// <param name="sectorX">The sector's x.</param>
    /// <param name="sectorZ">The sector's z.</param>
    /// <param name="width">The grid's sectors a side (the game's 512).</param>
    public static ChunkFile? Find(IEnumerable<ChunkFile> chunks, int sectorX, int sectorZ, int width = VanillaWidth)
    {
        if (!InWorld(sectorX, sectorZ, width))
        {
            return null;
        }
        ushort c = BaseChunk(sectorX, sectorZ, width);
        return chunks.FirstOrDefault(f => f.Size < SizeFilters.Length && (ushort)(c & SizeFilters[f.Size]) == f.Chunk && f.Chunk != PortalChunk);
    }

    /// <summary>
    /// <see cref="Find"/> for many sectors of one index: each base chunk's file is looked up once, then remembered.
    /// </summary>
    /// <param name="chunks">The save's chunk files.</param>
    /// <param name="width">The grid's sectors a side (the game's 512).</param>
    public static Func<int, int, ChunkFile?> Finder(IReadOnlyList<ChunkFile> chunks, int width = VanillaWidth)
    {
        Dictionary<ushort, ChunkFile?> found = [];
        return (sectorX, sectorZ) =>
        {
            if (!InWorld(sectorX, sectorZ, width))
            {
                return null;
            }
            ushort c = BaseChunk(sectorX, sectorZ, width);
            return found.TryGetValue(c, out ChunkFile? f) ? f : found[c] = Find(chunks, sectorX, sectorZ, width);
        };
    }

    /// <summary>ZoneSystem.ChunkPortal: the special chunk holding portals (never a terrain object's home).</summary>
    public const ushort PortalChunk = 0;
}
