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
	// Replaced by a fresh read after saving.
	public required WorldSave World { get; set; }
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
	public float Water { get; init; } = TerrainService.WaterLevel;
	public required List<Thing> Things { get; init; }
	// The ground the world's locations and pieces flatten (for the Location flattening overlay).
	public TerrainModifiers? Modifiers { get; set; }
	// The world generator's ground (regrow asks it what grows where); null in tests that only draw.
	public TerrainService? Terrain { get; init; }
	// For the game's terrain shader, per grid point: the biome colour the game puts in its mesh (corner
	// biomes blended like Heightmap.GetBiomeColor, RGBA bytes), the paint mask (_ClearedMaskTex: the
	// paint where it was edited, the generated ground's otherwise), the depth below sea level the game
	// gives each zone's corners (blended across the zone) and points at the ±8 m edit limit.
	public required byte[] BiomeColor { get; init; }
	public required byte[] Mask { get; init; }
	public required float[] OceanDepth { get; init; }
	public required float[] Limit { get; init; }
	public string LoadInfo { get; init; } = "";
	// Editing the ground (null in tests that only draw).
	public EditSession? Session { get; set; }
	// The open world this area belongs to (shared by every area opened from the map).
	public WorldSession? Owner { get; init; }

	// An object or building piece. Things are only ever added to the list (indices stay valid): a
	// deleted one is kept, Gone, so undo can bring it back; a moved one is a new thing (see EditSession).
	public readonly record struct Thing(int Id, int Prefab, Vector3 Position, Vector3 Rotation, float Scale, bool Piece)
	{
		public bool Gone { get; init; }

		// A creature a player tamed (its own View kind).
		public bool Tamed { get; init; }

		// A runestone: in the save a location proxy, listed under the location's name. It can be
		// picked and deleted, not moved or copied (see PrefabCatalog.RunestoneLocations).
		public bool Runestone => PrefabCatalog.IsRunestone(Prefab);
	}

	// A new object is tamed when it is a moved tamed creature (a move keeps the object's own data).
	public static bool TamedOf(WorldSave world, NewObject n) => !n.Fresh && n.SourceId is int s && world.Tamed.Contains(s);

	// The game's biome colour for a biome (Heightmap.GetBiomeColor): which terrain textures it blends.
	internal static byte[] BiomeRgba(int biome) => biome switch
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
		var owner = WorldSession.Open(dir);
		return Load(owner, zx, zz, size, watch.ElapsedMilliseconds);
	}

	// An area (size × size zones around zone zx, zz) of an open world.
	public static WorldScene Load(WorldSession owner, int zx, int zz, int size, long readMs = 0)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		var world = owner.World;
		var edits = owner.Edits;
		var modifiers = owner.Modifiers;
		var terrain = owner.Terrain;
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
		var things = ReadThings(world, x0, z0, size, edits.Deleted, edits.Added);
		var scene = new WorldScene
		{
			World = world, Name = world.Name, X0 = x0, Z0 = z0, Size = size, W = w, H = h, Heights = heights, Biomes = biomes,
			Cx = minX + (w - 1) / 2f, Cz = minZ + (h - 1) / 2f, Things = things,
			BiomeColor = biomeCol, Mask = mask, OceanDepth = ocean, Limit = limit, Modifiers = modifiers, Terrain = terrain, Owner = owner,
			LoadInfo = $"{world.Name}: read in {readMs} ms, {size}×{size} zones and {things.Count:N0} objects ready in {watch.ElapsedMilliseconds} ms",
		};
		scene.Session = new EditSession(scene, Ground.Read(terrain, edits, x0, z0, size), edits);
		// The world's history (made in other areas), fitted to this one.
		if (owner?.History is { } kept)
		{
			scene.Session.Import(kept, ids => FindThings(world, edits, ids));
		}
		return scene;
	}

	// Objects by id wherever they are, as things gone or not (deleted, or added and undone, they are gone):
	// what a history fitted to an area needs that the area does not show.
	public static Dictionary<int, Thing> FindThings(WorldSave world, EditStore edits, IReadOnlyCollection<int> ids)
	{
		var want = ids.ToHashSet();
		var deleted = edits.Deleted;
		var added = edits.Added.Select(n => n.Id).ToHashSet();
		var found = new Dictionary<int, Thing>();
		foreach (int id in want.Where(i => i < 0))
		{
			if (edits.FindAdded(id) is { } n)
			{
				found[id] = new Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, PieceCatalog.Get(n.Prefab)?.Tool != null) { Gone = !added.Contains(id), Tamed = TamedOf(world, n) };
			}
		}
		if (want.Any(i => i >= 0))
		{
			foreach (var (id, prefab, p, r, sc) in world.Objects)
			{
				if (want.Contains(id))
				{
					found[id] = new Thing(id, prefab, p, r, sc.X, false) { Gone = deleted.Contains(id), Tamed = world.Tamed.Contains(id) };
				}
			}
			foreach (var (id, prefab, p, ry) in world.Pieces)
			{
				if (want.Contains(id))
				{
					found[id] = new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true) { Gone = deleted.Contains(id) };
				}
			}
		}
		return found;
	}

	// The objects and building pieces of the block's zones (those not deleted).
	// added: objects added in this session (other areas of the world may have placed or moved some here).
	public static List<Thing> ReadThings(WorldSave world, int x0, int z0, int size, ICollection<int> deleted, IEnumerable<NewObject>? added = null)
	{
		float minX = x0 * 64f - 32f, maxX = (x0 + size - 1) * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = (z0 + size - 1) * 64f + 32f;
		bool Inside(Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
		var things = new List<Thing>();
		foreach (var (id, prefab, p, r, sc) in world.Objects)
		{
			if (Inside(p) && !deleted.Contains(id))
			{
				things.Add(new Thing(id, prefab, p, r, sc.X, false) { Tamed = world.Tamed.Contains(id) });
			}
		}
		foreach (var (id, prefab, p, ry) in world.Pieces)
		{
			if (Inside(p) && !deleted.Contains(id))
			{
				things.Add(new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true));
			}
		}
		// Not the ground discs for No limit ground (invisible: they are just the ground).
		foreach (var n in added ?? Enumerable.Empty<NewObject>())
		{
			if (Inside(n.Position) && n.Prefab != WorldSave.LocationProxyPrefab)
			{
				things.Add(new Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, PieceCatalog.Get(n.Prefab)?.Tool != null) { Tamed = TamedOf(world, n) });
			}
		}
		return things;
	}

	// After an edit: the heights, paint mask and limit marks of a rectangle of grid points from the ground.
	public void Refresh(Ground g, int x0, int z0, int x1, int z1)
	{
		for (int gz = z0; gz <= z1; gz++)
		{
			for (int gx = x0; gx <= x1; gx++)
			{
				int p = gz * W + gx;
				Heights[p] = g.HeightOf(p);
				Limit[p] = g.AtLimit(p) ? 1 : 0;
				for (int c = 0; c < 4; c++)
				{
					Mask[p * 4 + c] = (byte)Math.Clamp((int)MathF.Round(g.MaskOf(p, c) * 255f), 0, 255);
				}
			}
		}
	}
}
