namespace TerrainEditor.Save;

// Which chunk file an object belongs to (ZoneSystem / ZDOMan.GetSaveClonePerChunk): the world is
// 512 x 512 sectors of 64 m; a base chunk covers 8 x 8 sectors, and a chunk of size s merges
// 2^s x 2^s base chunks (ZoneSystem.s_sizeFilters clears the low s bits of x and y).
public static class ChunkMath
{
	private static readonly ushort[] SizeFilters = { 65535, 65278, 64764, 63736, 61680 };

	public static (int X, int Z) SectorOf(float x, float z) => ((int)Math.Floor((x + 32f) / 64f), (int)Math.Floor((z + 32f) / 64f));

	public static ushort BaseChunk(int sectorX, int sectorZ)
	{
		uint ix = (uint)(sectorX + 256), iz = (uint)(sectorZ + 256);
		if (ix >= 512 || iz >= 512)
		{
			throw new ArgumentOutOfRangeException(nameof(sectorX), "Position is outside the world.");
		}
		return (ushort)(ix / 8 + ((iz / 8) << 8));
	}

	// The chunk file (from the current index) whose area contains the sector, if any.
	public static ChunkFile? Find(IEnumerable<ChunkFile> chunks, int sectorX, int sectorZ)
	{
		ushort c = BaseChunk(sectorX, sectorZ);
		return chunks.FirstOrDefault(f => f.Size < SizeFilters.Length && (ushort)(c & SizeFilters[f.Size]) == f.Chunk && f.Chunk != PortalChunk);
	}

	// ZoneSystem.ChunkPortal: the special chunk holding portals (never a terrain object's home).
	public const ushort PortalChunk = 0;
}
