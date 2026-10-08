using System.IO.Compression;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

// A brand-new world from a seed, in the chunked save format the editor reads (as the game with the
// Deep North save writes it, see WorldSave): save #1 with its metadata (.fwl2), its zone list and the
// random events' state (.db2), its chunk index (.chunks) and the commit marker (.ok), written last.
// The ground is the seed's own (the world generator); the zones within `radius` of the middle are
// generated with the game's vegetation rules (Regrow), the rest is left for the game to generate;
// zone 0, 0 also gets an unedited terrain compiler object, so the editor can save ground edits.
// flat: the most even zone of dry Meadows left without vegetation, its ground untouched (no edit, so
// the tools keep their whole ±8 m on it): a clean square to show them on (the documentation's pictures).
// Location instances (the start altar, villages, dungeons...) are not placed: the world is marked as
// not having them yet, so the game lays them out when it first loads it.
public static class WorldCreator
{
	public const int SaveFileVersion = 41;

	// The generator's version for new worlds (World.m_worldGenVersion) and the location list's
	// (ZoneSystem.m_locationVersion), as the game writes them today.
	public const int WorldGenVersion = 2, LocationVersion = 32;

	// The largest square of generated zones around the middle (radius in zones).
	public const int MaxRadius = 16;

	public sealed record Created(string Directory, int Seed, int Zones, int Objects, (int X, int Z)? Flat = null, float FlatSpan = 0);

	// The most even zone of dry Meadows within `radius` zones of the middle (not on its edge): the
	// one whose ground spans the fewest metres. Null when none is wholly above the water.
	public static (int X, int Z, float Span)? FlattestZone(ValheimGen.TerrainService terrain, int radius)
	{
		(int, int, float)? best = null;
		for (int z = -radius + 1; z < radius; z++)
		{
			for (int x = -radius + 1; x < radius; x++)
			{
				float[] h = terrain.BaseZone(x, z);
				float lo = h.Min(), hi = h.Max();
				if (lo < ValheimGen.TerrainService.WaterLevel + 1.5f || best is { } b && hi - lo >= b.Item3)
				{
					continue;
				}
				bool meadows = true;
				for (int k = 0; k < 9 && meadows; k++)
				{
					meadows = terrain.BiomeAt(x * 64 + (k % 3 - 1) * 28, z * 64 + (k / 3 - 1) * 28) == ValheimGen.Heightmap.Biome.Meadows;
				}
				if (meadows)
				{
					best = (x, z, hi - lo);
				}
			}
		}
		return best;
	}

	// The game's seed number for a seed name (World: m_seed = m_seedName.GetStableHashCode()).
	public static int SeedOf(string seedName) => StableHash.Of(seedName);

	// After the random events' state, what the game's save holds: an empty list, brotli-compressed
	// JSON {"list":[]} behind its length (copied from a world the game made).
	private static readonly byte[] EmptyListBlock = { 0x0f, 0, 0, 0, 0x0b, 0x05, 0x80, (byte)'{', (byte)'"', (byte)'l', (byte)'i', (byte)'s', (byte)'t', (byte)'"', (byte)':', (byte)'[', (byte)']', (byte)'}', 0x03 };

	public static Created Create(string folder, string name, string seedName, int radius = 0, long? uid = null, bool flat = false)
	{
		name = name.Trim();
		seedName = seedName.Trim();
		if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			throw new ArgumentException("The world needs a name that can be a file name.", nameof(name));
		}
		if (seedName.Length == 0 || seedName.Length > 10)
		{
			throw new ArgumentException("The seed is 1 to 10 characters, as in the game.", nameof(seedName));
		}
		if (radius < 0 || radius > MaxRadius)
		{
			throw new ArgumentOutOfRangeException(nameof(radius), $"The generated zones reach 0 to {MaxRadius} zones from the middle.");
		}
		if (flat && radius < 2)
		{
			throw new ArgumentException("A bare zone needs generated zones around it (a radius of 2 or more).", nameof(flat));
		}
		if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any(f => Path.GetFileName(f).StartsWith("_main.") || f.EndsWith(".chunk")))
		{
			throw new InvalidOperationException($"{folder} already holds a world: a new one is never written over it.");
		}
		Directory.CreateDirectory(folder);
		int seed = SeedOf(seedName);
		string main = Path.Combine(folder, "_main.1");
		WriteMetadata(main + ".fwl2", name, seedName, seed, uid ?? Random.Shared.NextInt64(1, long.MaxValue));
		WriteIndex(main + ".chunks", new List<(ushort Chunk, int Count)>());
		var db = ZoneDb.Empty(SaveFileVersion, LocationVersion, Tail());
		db.Save(main + ".db2");

		int objects = 0, zones = 0;
		(int X, int Z, float Span)? flatZone = null;
		if (radius > 0)
		{
			// The game's vegetation on the seed's own ground, zone by zone, in base chunk files.
			WorldSave empty = WorldSave.Load(folder);
			var terrain = new ValheimGen.TerrainService(empty);
			var spots = Regrow.Zones(terrain, new EditStore(empty), seed, -radius, -radius, radius, radius);
			if (flat)
			{
				flatZone = FlattestZone(terrain, radius) ?? throw new InvalidOperationException("No zone of dry, even Meadows near the middle: try another seed or a larger radius.");
				var (fx, fz, _) = flatZone.Value;
				// Nothing grows on it.
				spots = spots.Where(s => (int)MathF.Floor((s.X + 32) / 64) != fx || (int)MathF.Floor((s.Z + 32) / 64) != fz).ToList();
			}
			var byChunk = new SortedDictionary<ushort, List<byte[]>>();
			foreach (var s in spots)
			{
				int prefab = StableHash.Of(s.Name);
				if (TerrainEditor.Terrain.PrefabCatalog.Get(prefab) is not { } info)
				{
					continue;
				}
				var (sx, sz) = ChunkMath.SectorOf(s.X, s.Z);
				ushort chunk = ChunkMath.BaseChunk(sx, sz);
				if (chunk == ChunkMath.PortalChunk)
				{
					continue;
				}
				(byChunk.TryGetValue(chunk, out var list) ? list : byChunk[chunk] = new()).Add(ZdoBuilder.Blank(prefab, info.Flags, new System.Numerics.Vector3(s.X, s.Y, s.Z), new System.Numerics.Vector3(s.Rx, s.Ry, s.Rz), s.Scale));
				objects++;
			}
			// One terrain compiler object, with no edit, in the middle zone: the writer copies it for
			// every zone whose ground is edited later (the game makes the first one on the first edit).
			ushort middle = ChunkMath.BaseChunk(0, 0);
			(byChunk.TryGetValue(middle, out var mid) ? mid : byChunk[middle] = new()).Add(EmptyTerrainObject());
			objects++;
			var index = new List<(ushort Chunk, int Count)>();
			foreach (var (chunk, list) in byChunk)
			{
				WriteChunk(Path.Combine(folder, ChunkFile.NameFor(chunk, 0, 1)), list);
				index.Add((chunk, list.Count));
			}
			WriteIndex(main + ".chunks", index);
			for (int z = -radius; z <= radius; z++)
			{
				for (int x = -radius; x <= radius; x++)
				{
					db.Generated.Add(((short)x, (short)z));
					zones++;
				}
			}
			db.Save(main + ".db2");
		}
		// Written last, like the game: only then is the save complete.
		File.WriteAllBytes(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
		return new Created(folder, seed, zones, objects, flatZone is var (cx, cz, _) ? (cx, cz) : null, flatZone?.Span ?? 0);
	}

	private static readonly int TerrainCompiler = StableHash.Of("_TerrainCompiler");

	// The game's terrain compiler object of zone 0, 0 with nothing edited: its position in the short
	// form, the prefab's object flags, and the TCData the writer encodes for an untouched zone.
	internal static byte[] EmptyTerrainObject()
	{
		ushort flags = TerrainEditor.Terrain.PrefabCatalog.Get(TerrainCompiler)?.Flags ?? 0x0D00;
		ZdoData z = ZdoData.Parse(ZdoBuilder.Blank(TerrainCompiler, flags, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 0f));
		z.SmallPosition = true;
		z.SetBytes(StableHash.Of("TCData"), WorldWriter.EncodeTerrain(new ZoneEdit(0, 0)));
		return z.Serialize();
	}

	// World.SaveWorldMetaData: a length, then the package (version, name, seed name, seed, unique id,
	// generator version, "has a database", no starting global keys, and the trailing count the game
	// writes after them).
	private static void WriteMetadata(string path, string name, string seedName, int seed, long uid)
	{
		using MemoryStream pkg = new();
		using (BinaryWriter p = new(pkg, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			p.Write(SaveFileVersion);
			p.Write(name);
			p.Write(seedName);
			p.Write(seed);
			p.Write(uid);
			p.Write(WorldGenVersion);
			p.Write(true);
			p.Write(0);
			p.Write(0);
		}
		using BinaryWriter w = new(File.Create(path));
		w.Write((int)pkg.Length);
		w.Write(pkg.ToArray());
	}

	private static void WriteIndex(string path, List<(ushort Chunk, int Count)> chunks)
	{
		using BinaryWriter w = new(File.Create(path));
		w.Write((short)SaveFileVersion);
		w.Write(chunks.Sum(c => c.Count));
		w.Write(chunks.Count);
		foreach (var (chunk, count) in chunks)
		{
			w.Write(chunk);
			w.Write((byte)0);
			w.Write(1u);
			w.Write(count);
		}
	}

	private static void WriteChunk(string path, List<byte[]> objects)
	{
		using BinaryWriter w = new(File.Create(path));
		w.Write((short)SaveFileVersion);
		w.Write(objects.Count);
		foreach (byte[] o in objects)
		{
			w.Write(o);
		}
	}

	// RandEventSystem's save with no event running (timer, event name, time, position), then the list.
	private static byte[] Tail()
	{
		using MemoryStream ms = new();
		using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			w.Write(0f);
			w.Write("");
			w.Write(0f);
			w.Write(0f); w.Write(0f); w.Write(0f);
			w.Write(EmptyListBlock);
		}
		return ms.ToArray();
	}
}
