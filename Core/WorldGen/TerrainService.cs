using System.Collections.Concurrent;
using System.IO.Compression;
using TerrainEditor.Save;

namespace ValheimGen;

// Base (unmodified) terrain for a world, computed offline with the ported world generator.
public sealed class TerrainService
{
	public const float WaterLevel = 30f;

	public const float MapRadius = 10500f;

	private readonly WorldGenerator _gen;

	private readonly ConcurrentDictionary<(int, int), float[]> _zones = new();

	private readonly Lazy<byte[]> _overviewPng;

	private readonly TerrainEditor.Terrain.TerrainModifiers? _modifiers;

	public TerrainService(WorldSave save, TerrainEditor.Terrain.TerrainModifiers? modifiers = null, int overviewSize = 1024)
	{
		_modifiers = modifiers;
		World world = new() { m_seed = save.Seed, m_seedName = save.SeedName, m_worldGenVersion = save.WorldGenVersion };
		WorldGenerator.Initialize(world);
		_gen = WorldGenerator.instance;
		_overviewPng = new Lazy<byte[]>(() => RenderOverview(overviewSize), LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public int OverviewSize { get; } = 1024;

	public byte[] OverviewPng => _overviewPng.Value;

	// The ground the game's saved edits apply to: generated terrain plus the runtime flattening of
	// locations (TerrainModifier), exactly as Heightmap.ApplyModifiers does before TerrainComp.
	public float[] BaseZone(int zx, int zz) => _zones.GetOrAdd((zx, zz), k =>
	{
		float[] h = BaseTerrain.BuildZone(_gen, k.Item1, k.Item2);
		_modifiers?.Apply(k.Item1, k.Item2, h);
		return h;
	});

	private readonly ConcurrentDictionary<(int, int), float[]> _masks = new();

	private readonly ConcurrentDictionary<(int, int), int[]> _biomes = new();

	// Biome at every vertex of a zone (65 x 65, row = z), for brush masks.
	public int[] VertexBiomes(int zx, int zz) => _biomes.GetOrAdd((zx, zz), k =>
	{
		int[] b = new int[65 * 65];
		float x0 = k.Item1 * 64f - 32f, z0 = k.Item2 * 64f - 32f;
		for (int i = 0; i < 65; i++)
		{
			for (int j = 0; j < 65; j++)
			{
				b[i * 65 + j] = (int)_gen.GetBiome(x0 + j, z0 + i);
			}
		}
		return b;
	});

	// The game's base paint mask for a zone (biome-specific; Color.black elsewhere).
	public float[] BaseMask(int zx, int zz) => _masks.GetOrAdd((zx, zz), k =>
	{
		float[] m = new float[65 * 65 * 4];
		BaseTerrain.BuildZone(_gen, k.Item1, k.Item2, m);
		return m;
	});

	// Heightmap.m_cornerBiomes: the biome sector at each zone corner. Sectors sample GetBiome on the
	// 12 m grid of AltBiomeWorldData (WorldSpaceToMapSpace / MapSpaceToWorldSpace).
	public int[] CornerBiomes(int zx, int zz)
	{
		static float Snap(float v) => ((int)((v - 6f) / 12f + 1024f) - 1024) * 12f + 6f;
		float x0 = zx * 64f - 32f, z0 = zz * 64f - 32f;
		return new[] { (x0, z0), (x0 + 64f, z0), (x0, z0 + 64f), (x0 + 64f, z0 + 64f) }
			.Select(c => { float sx = Snap(c.Item1), sz = Snap(c.Item2); return sx * sx + sz * sz > 110250000f ? (int)Heightmap.Biome.Ocean : (int)_gen.GetBiome(sx, sz); }).ToArray();
	}

	// Generated terrain only, without location flattening.
	public float[] RawZone(int zx, int zz) => BaseTerrain.BuildZone(_gen, zx, zz);

	public Heightmap.Biome BiomeAt(float x, float z) => _gen.GetBiome(x, z);

	private byte[] RenderOverview(int size)
	{
		float[] heights = new float[size * size];
		Heightmap.Biome[] biomes = new Heightmap.Biome[size * size];
		float step = 2f * MapRadius / size;
		// Row 0 is the north edge so the PNG has north up.
		Parallel.For(0, size, row =>
		{
			float z = MapRadius - (row + 0.5f) * step;
			for (int col = 0; col < size; col++)
			{
				float x = -MapRadius + (col + 0.5f) * step;
				int i = row * size + col;
				if (x * x + z * z > MapRadius * MapRadius)
				{
					heights[i] = -400f;
					biomes[i] = Heightmap.Biome.Ocean;
					continue;
				}
				Heightmap.Biome biome = _gen.GetBiome(x, z);
				biomes[i] = biome;
				heights[i] = _gen.GetBiomeHeight(biome, x, z, out _);
			}
		});
		byte[] rgba = new byte[size * size * 4];
		for (int row = 0; row < size; row++)
		{
			for (int col = 0; col < size; col++)
			{
				int i = row * size + col;
				float h = heights[i];
				(float r, float g, float b) = BiomeColor(biomes[i], h);
				if (h >= WaterLevel)
				{
					// Simple hill shading from the north-west.
					float dx = heights[row * size + Math.Min(col + 1, size - 1)] - heights[row * size + Math.Max(col - 1, 0)];
					float dz = heights[Math.Min(row + 1, size - 1) * size + col] - heights[Math.Max(row - 1, 0) * size + col];
					float shade = Math.Clamp(1f - (dx - dz) * 0.012f, 0.55f, 1.35f);
					r *= shade;
					g *= shade;
					b *= shade;
				}
				rgba[i * 4] = (byte)Math.Clamp(r, 0f, 255f);
				rgba[i * 4 + 1] = (byte)Math.Clamp(g, 0f, 255f);
				rgba[i * 4 + 2] = (byte)Math.Clamp(b, 0f, 255f);
				rgba[i * 4 + 3] = (byte)(heights[i] <= -400f ? 0 : 255);
			}
		}
		return Png.Encode(size, size, rgba);
	}

	internal static (float, float, float) BiomeColor(Heightmap.Biome biome, float h)
	{
		if (h < WaterLevel)
		{
			float depth = Math.Clamp((WaterLevel - h) / 60f, 0f, 1f);
			return (40 - 25 * depth, 95 - 55 * depth, 140 - 50 * depth);
		}
		return biome switch
		{
			Heightmap.Biome.Meadows => (112, 158, 72),
			Heightmap.Biome.BlackForest => (58, 92, 54),
			Heightmap.Biome.Swamp => (110, 94, 68),
			Heightmap.Biome.Mountain => (205, 210, 216),
			Heightmap.Biome.Plains => (196, 178, 96),
			Heightmap.Biome.Mistlands => (102, 98, 116),
			Heightmap.Biome.AshLands => (150, 64, 48),
			Heightmap.Biome.DeepNorth => (220, 232, 240),
			_ => (150, 150, 150)
		};
	}
}

// Minimal PNG writer (8-bit RGBA, no filtering).
public static class Png
{
	private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
	{
		uint c = (uint)n;
		for (int k = 0; k < 8; k++)
		{
			c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
		}
		return c;
	}).ToArray();

	public static byte[] Encode(int width, int height, byte[] rgba)
	{
		using MemoryStream png = new();
		png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
		byte[] ihdr = new byte[13];
		WriteBE(ihdr, 0, (uint)width);
		WriteBE(ihdr, 4, (uint)height);
		ihdr[8] = 8;
		ihdr[9] = 6;
		Chunk(png, "IHDR", ihdr);
		using (MemoryStream raw = new())
		{
			using (ZLibStream z = new(raw, CompressionLevel.Fastest, leaveOpen: true))
			{
				for (int y = 0; y < height; y++)
				{
					z.WriteByte(0);
					z.Write(rgba, y * width * 4, width * 4);
				}
			}
			Chunk(png, "IDAT", raw.ToArray());
		}
		Chunk(png, "IEND", Array.Empty<byte>());
		return png.ToArray();
	}

	private static void Chunk(Stream s, string type, byte[] data)
	{
		byte[] len = new byte[4];
		WriteBE(len, 0, (uint)data.Length);
		s.Write(len);
		byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
		s.Write(typeBytes);
		s.Write(data);
		uint crc = 0xFFFFFFFFu;
		foreach (byte b in typeBytes.Concat(data))
		{
			crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
		}
		WriteBE(len, 0, crc ^ 0xFFFFFFFFu);
		s.Write(len);
	}

	private static void WriteBE(byte[] b, int o, uint v)
	{
		b[o] = (byte)(v >> 24);
		b[o + 1] = (byte)(v >> 16);
		b[o + 2] = (byte)(v >> 8);
		b[o + 3] = (byte)v;
	}
}
