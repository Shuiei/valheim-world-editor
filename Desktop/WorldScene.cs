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
	public string LoadInfo { get; init; } = "";

	public readonly record struct Thing(int Id, int Prefab, Vector3 Position, Vector3 Rotation, float Scale, bool Piece);

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
		foreach (var (x, z) in keys)
		{
			int[] b = terrain.VertexBiomes(x, z);
			int ox = (x - x0) * 64, oz = (z - z0) * 64;
			for (int k = 0; k < 65; k++)
			{
				for (int l = 0; l < 65; l++)
				{
					biomes[(oz + k) * w + ox + l] = b[k * 65 + l];
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
			LoadInfo = $"{world.Name}: read in {readMs} ms, {size}×{size} zones and {things.Count:N0} objects ready in {watch.ElapsedMilliseconds} ms",
		};
	}
}
