using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using TerrainService = ValheimGen.TerrainService;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Desktop;

// What the 3D view shows: a square block of zones of a world, its ground (heights and biomes, edits
// included) and the objects and building pieces in it. Read once from the save through the shared
// library, the same way the web editor's session does.
//
// Coordinates: the grid starts at the block's south-west corner (one point per metre, 64 per zone,
// shared edges once); the 3D view puts the block's middle at 0 and mirrors z (OpenGL is right-handed,
// Unity left-handed), like the web editor: view = (x - Cx, y, -(z - Cz)) in world metres.
public sealed class WorldScene
{
	public required WorldSave World { get; init; }
	public required string Name { get; init; }
	public int X0 { get; init; }
	public int Z0 { get; init; }
	public int Size { get; init; }
	public int W { get; init; }
	public int H { get; init; }
	public required float[] Heights { get; init; }
	public required int[] Biomes { get; init; }
	// World coordinates of the block's middle.
	public float Cx { get; init; }
	public float Cz { get; init; }
	public float Water => TerrainService.WaterLevel;
	public required List<Thing> Things { get; init; }
	// For the game's terrain shader, per grid point: the biome colour the game puts in its mesh (corner
	// biomes blended like Heightmap.GetBiomeColor, RGBA bytes), the paint mask (_ClearedMaskTex: the
	// paint where it was edited, the generated ground's otherwise), the depth below sea level the game
	// gives each zone's corners (blended across the zone) and points at the ±8 m edit limit.
	public required byte[] BiomeColor { get; init; }
	public required byte[] Mask { get; init; }
	public required float[] OceanDepth { get; init; }
	public required float[] Limit { get; init; }
	public string LoadInfo { get; init; } = "";

	public readonly record struct Thing(int Id, int Prefab, Vector3 Position, Vector3 Rotation, float Scale, bool Piece);

	// The game's biome colour for a biome (Heightmap.GetBiomeColor): which terrain textures it blends.
	private static byte[] BiomeRgba(int biome) => biome switch
	{
		2 => new byte[] { 255, 0, 0, 0 },
		4 => new byte[] { 0, 255, 0, 0 },
		8 => new byte[] { 0, 0, 255, 0 },
		16 => new byte[] { 0, 0, 0, 255 },
		32 => new byte[] { 255, 0, 0, 255 },
		64 => new byte[] { 0, 255, 0, 0 },
		512 => new byte[] { 0, 0, 255, 255 },
		_ => new byte[] { 0, 0, 0, 0 },
	};

	// The world's folder: a path, a world's name, or (null) the one saved last on this computer.
	public static string FindWorld(string? want)
	{
		if (want != null && Directory.Exists(want))
		{
			return Path.GetFullPath(want);
		}
		var all = Worlds.Find(AppSettings.Load()).Where(w => w.Usable).ToList();
		var pick = want == null ? all.FirstOrDefault() : all.FirstOrDefault(w => string.Equals(w.Name, want, StringComparison.OrdinalIgnoreCase));
		return pick?.Path ?? throw new InvalidOperationException(want == null ? "No Valheim world found on this computer." : $"No world called {want}.");
	}

	public static WorldScene Load(string dir, int zx, int zz, int size)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		WorldSave.ModifierPrefabs = TerrainModifiers.NetworkPrefabHashes.ToHashSet();
		var world = WorldSave.Load(dir);
		long readMs = watch.ElapsedMilliseconds;
		var edits = new EditStore(world);
		var terrain = new TerrainService(world, new TerrainModifiers(world));
		int x0 = zx - size / 2, z0 = zz - size / 2, x1 = x0 + size - 1, z1 = z0 + size - 1;
		// The zones' generated ground in parallel (the slow part), then the grid with the edits.
		var keys = (from z in Enumerable.Range(z0, size) from x in Enumerable.Range(x0, size) select (x, z)).ToArray();
		Parallel.ForEach(keys, k => terrain.BaseZone(k.x, k.z));
		var (w, h, heights) = HeightGrid.Read(terrain, edits, x0, z0, x1, z1);
		int[] biomes = new int[w * h];
		byte[] biomeCol = new byte[w * h * 4], mask = new byte[w * h * 4];
		float[] limit = new float[w * h], ocean = new float[w * h];
		static float Smooth01(float t) => t * t * (3 - 2 * t);
		foreach (var (x, z) in keys)
		{
			int[] b = terrain.VertexBiomes(x, z);
			float[] baseMask = terrain.BaseMask(x, z);
			byte[][] corners = terrain.CornerBiomes(x, z).Select(BiomeRgba).ToArray();
			var e = edits.Get(x, z);
			int ox = (x - x0) * 64, oz = (z - z0) * 64;
			for (int k = 0; k < 65; k++)
			{
				for (int l = 0; l < 65; l++)
				{
					int i = k * 65 + l, g = (oz + k) * w + ox + l;
					biomes[g] = b[i];
					float ix = Smooth01(l / 64f), iy = Smooth01(k / 64f);
					for (int c = 0; c < 4; c++)
					{
						// Integer steps like the web editor (Math.trunc), so both look the same.
						int lo = (int)(corners[0][c] + (corners[1][c] - corners[0][c]) * ix), hi = (int)(corners[2][c] + (corners[3][c] - corners[2][c]) * ix);
						biomeCol[g * 4 + c] = (byte)(int)(lo + (hi - lo) * iy);
						float m = e != null && e.PaintModified[i] ? e.Paint[i * 4 + c] : baseMask[i * 4 + c];
						mask[g * 4 + c] = (byte)Math.Clamp((int)MathF.Round(m * 255f), 0, 255);
					}
					limit[g] = e != null && e.Modified[i] && MathF.Abs(e.Level[i] + e.Smooth[i]) >= EditStore.MaxLevel - 0.05f ? 1 : 0;
				}
			}
		}
		// Heightmap.UpdateCornerDepths: each zone gets max(0, 30 - height) at its corners, blended across it.
		for (int zz2 = 0; zz2 < size; zz2++)
		{
			for (int zx2 = 0; zx2 < size; zx2++)
			{
				float D(int gx, int gz) => MathF.Max(0, 30 - heights[gz * w + gx]);
				int ax = zx2 * 64, az = zz2 * 64;
				float d00 = D(ax, az), d10 = D(ax + 64, az), d01 = D(ax, az + 64), d11 = D(ax + 64, az + 64);
				for (int k = 0; k <= 64; k++)
				{
					for (int l = 0; l <= 64; l++)
					{
						float u = l / 64f, v = k / 64f;
						ocean[(az + k) * w + ax + l] = (d00 + (d10 - d00) * u) * (1 - v) + (d01 + (d11 - d01) * u) * v;
					}
				}
			}
		}
		float minX = x0 * 64f - 32f, maxX = x1 * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = z1 * 64f + 32f;
		bool Inside(Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
		var things = new List<Thing>();
		foreach (var (id, prefab, p, r, s) in world.Objects)
		{
			if (Inside(p) && !edits.Deleted.Contains(id))
			{
				things.Add(new Thing(id, prefab, p, r, s.X, false));
			}
		}
		foreach (var (id, prefab, p, ry) in world.Pieces)
		{
			if (Inside(p) && !edits.Deleted.Contains(id))
			{
				things.Add(new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true));
			}
		}
		return new WorldScene
		{
			World = world, Name = world.Name, X0 = x0, Z0 = z0, Size = size, W = w, H = h, Heights = heights, Biomes = biomes,
			Cx = minX + (w - 1) / 2f, Cz = minZ + (h - 1) / 2f, Things = things,
			BiomeColor = biomeCol, Mask = mask, OceanDepth = ocean, Limit = limit,
			LoadInfo = $"{world.Name}: read in {readMs} ms, {size}×{size} zones and {things.Count:N0} objects ready in {watch.ElapsedMilliseconds} ms",
		};
	}
}
