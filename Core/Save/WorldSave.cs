using System.Globalization;
using System.IO.Compression;
using System.Numerics;

namespace TerrainEditor.Save;

// Read-only loader for Valheim's chunked world save (world version >= 40):
//   _main.<n>.chunks  index of chunk files and how many objects each holds
//   XX_YY__<size>_<version>.chunk  objects (ZDOs) of one chunk
// Only what the terrain editor needs is kept: every object's prefab and position, and the
// terrain modification data ("TCData") of each zone's terrain compiler object.
public sealed class WorldSave
{
	private static readonly int TCDataKey = StableHash.Of("TCData");

	public static readonly int LocationProxyPrefab = StableHash.Of("LocationProxy");

	public static readonly int LocationKey = StableHash.Of("location");

	private static readonly int TimeCreatedKey = StableHash.Of("terrainModifierTimeCreated");

	private static readonly int CreatorKey = StableHash.Of("creator");

	// Player ids and the names stored with them: bed and tombstone owners, ward builders, players
	// (live: the players that are online).
	private static readonly int OwnerKey = StableHash.Of("owner"), OwnerNameKey = StableHash.Of("ownerName");
	private static readonly int CreatorNameKey = StableHash.Of("creatorName");
	private static readonly int PlayerIdKey = StableHash.Of("playerID"), PlayerNameKey = StableHash.Of("playerName");

	private static readonly int ScaleKey = StableHash.Of("scale");

	private static readonly int ScaleScalarKey = StableHash.Of("scaleScalar");

	// Tameable.SetTamed writes it (an int, 1 when tamed).
	private static readonly int TamedKey = StableHash.Of("tamed");

	// Prefabs (besides LocationProxy) whose objects carry terrain modifiers; set before loading.
	public static HashSet<int> ModifierPrefabs { get; set; } = new();

	public required string Directory { get; init; }

	public required int SaveNumber { get; init; }

	public string Name { get; private set; } = "";

	public string SeedName { get; private set; } = "";

	public int Seed { get; private set; }

	public int WorldGenVersion { get; private set; }

	public int ObjectCount { get; private set; }

	public int ChunkCount { get; private set; }

	public List<TerrainZone> TerrainZones { get; } = new();

	public List<ChunkFile> Chunks { get; } = new();

	// Placed locations (villages, trader, dungeons...): their terrain flattening is applied live by
	// the game and is not part of the saved terrain data.
	public List<(System.Numerics.Vector3 Position, int Location)> Locations { get; } = new();

	// The ground discs the editor placed for No limit ground (Uplift): invisible location proxies.
	public List<(int Id, Vector3 Position)> Discs { get; } = new();

	// Every placed object that can modify terrain while the game runs: locations and the prefabs in
	// ModifierPrefabs. Rotation is Euler degrees, as stored.
	public List<PlacedObject> Placed { get; } = new();

	// Objects that are tamed creatures (by id).
	public HashSet<int> Tamed { get; } = new();

	// Player-built pieces (objects with a creator): prefab, position and Y rotation in degrees.
	public List<(int Id, int Prefab, Vector3 Position, float RotationY)> Pieces { get; } = new();

	// Pieces per builder (the "creator" player id).
	public Dictionary<long, int> Creators { get; } = new();

	// Player names by player id, from the objects that store both.
	public Dictionary<long, string> PlayerNames { get; } = new();

	// The player id written as "creator" on new pieces (things built with the hammer, hoe, cultivator
	// or serving tray), so the game treats them as player built. Chosen in the editor; 0: not set.
	public static long Builder { get; set; }

	// The player who built the most pieces in this world (0 if nobody did).
	public long TopBuilder => Creators.Count == 0 ? 0 : Creators.MaxBy(c => c.Value).Key;

	// Every object in the save by id (its order in the save).
	public List<ObjectRef> ObjectRefs { get; } = new();

	// First object of each prefab: the template new objects of that prefab are copied from.
	public Dictionary<int, int> Templates { get; } = new();

	// The object a new one is built from: its source object when given (and of the same prefab),
	// else the prefab's template.
	// null when the world has no object of the prefab: it is then built blank (NewObjectBytes).
	public ObjectRef? ModelFor(int prefab, int? sourceId)
	{
		if (sourceId is int s && s >= 0 && s < ObjectRefs.Count && ObjectRefs[s].Prefab == prefab)
		{
			return ObjectRefs[s];
		}
		return Templates.TryGetValue(prefab, out int t) ? ObjectRefs[t] : null;
	}

	public bool CanCreate(int prefab) => Templates.ContainsKey(prefab) || TerrainEditor.Terrain.PrefabCatalog.Get(prefab) != null;

	// Prefabs that can be created: any object in the world, and every placeable game prefab.
	public IEnumerable<int> Creatable => Templates.Keys.Concat(TerrainEditor.Terrain.PrefabCatalog.Placeable.Select(p => StableHash.Of(p.Name))).Distinct();

	// The save bytes of a new object: a copy of its model when the world has one, else a blank object
	// with the prefab's own flags from the game (what the game writes for a freshly placed object).
	// readSource gives the chunk file bytes the model lives in.
	public byte[]? NewObjectBytes(TerrainEditor.Editing.NewObject n, Func<ObjectRef, byte[]> readSource)
	{
		byte[]? bytes = BuildBytes(n, readSource);
		// A new piece of a kind players build (placed, pasted or built blank) is the chosen builder's,
		// like one built in the game. Without a builder the game takes it for part of a ruin: a third of
		// the materials back, no base for fires, ignored by raids. Moved and edited objects keep theirs.
		if (bytes != null && Builder != 0 && (n.Fresh || n.Raw == null && ModelFor(n.Prefab, n.SourceId) == null)
			&& TerrainEditor.Terrain.PieceCatalog.Get(n.Prefab)?.Tool != null)
		{
			ZdoData z = ZdoData.Parse(bytes);
			z.Set("longs", CreatorKey, Builder.ToString(CultureInfo.InvariantCulture));
			bytes = z.Serialize();
		}
		return bytes;
	}

	private byte[]? BuildBytes(TerrainEditor.Editing.NewObject n, Func<ObjectRef, byte[]> readSource)
	{
		if (n.Raw != null)
		{
			ZdoData z = ZdoData.Parse(n.Raw);
			z.Position = n.Position;
			z.Rotation = n.Rotation;
			if (n.Scale > 0f)
			{
				z.Set("vec3", ScaleKey, $"{n.Scale.ToString(CultureInfo.InvariantCulture)} {n.Scale.ToString(CultureInfo.InvariantCulture)} {n.Scale.ToString(CultureInfo.InvariantCulture)}");
				z.Set("floats", ScaleScalarKey, null);
			}
			return z.Serialize();
		}
		ObjectRef? model = ModelFor(n.Prefab, n.SourceId);
		if (model != null)
		{
			return ZdoBuilder.Build(readSource(model), model, model.File.WorldVersion, n.Position, n.Rotation, n.Scale, n.Fresh);
		}
		return TerrainEditor.Terrain.PrefabCatalog.Get(n.Prefab) is { } info ? ZdoBuilder.Blank(n.Prefab, info.Flags, n.Position, n.Rotation, n.Scale) : null;
	}

	// The saved bytes of an object of the save (id >= 0): from its chunk file, or from the live snapshot.
	public byte[] ObjectBytes(int id)
	{
		ObjectRef o = ObjectRefs[id];
		if (LiveBytes != null)
		{
			return LiveBytes[(int)o.Start..(int)o.End];
		}
		using FileStream fs = File.OpenRead(Path.Combine(Directory, o.File.FileName));
		byte[] b = new byte[o.End - o.Start];
		fs.Seek(o.Start, SeekOrigin.Begin);
		fs.ReadExactly(b);
		return b;
	}

	// Generated zones and location instances from the .db2 file (null if it could not be read).
	public ZoneDb? Zones { get; private set; }

	// Everything else that is not terrain data (trees, rocks, bushes, pickables...): prefab, position,
	// rotation (Euler degrees) and scale (zero when the prefab's own scale applies).
	public List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> Objects { get; } = new();

	public static WorldSave Load(string directory)
	{
		int saveNumber = FindLatestSave(directory);
		WorldSave save = new() { Directory = directory, SaveNumber = saveNumber };
		save.LoadMetadata();
		save.LoadChunks();
		try
		{
			save.Zones = ZoneDb.Load(Path.Combine(directory, $"_main.{saveNumber}.db2"));
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Could not read the zone list in the .db2 file: {ex.Message}");
		}
		return save;
	}

	// Mirrors World.LoadWorld: an int length prefix, then a ZPackage with version, names and seed.
	private void LoadMetadata()
	{
		string path = Path.Combine(Directory, $"_main.{SaveNumber}.fwl2");
		using ValheimReader file = new(File.OpenRead(path));
		int length = file.ReadInt();
		using ValheimReader pkg = new(new MemoryStream(file.ReadBytes(length)));
		int version = pkg.ReadInt();
		Name = pkg.ReadString();
		SeedName = pkg.ReadString();
		Seed = pkg.ReadInt();
		pkg.ReadLong();
		WorldGenVersion = version >= 26 ? pkg.ReadInt() : 0;
	}

	private static int FindLatestSave(string directory)
	{
		// The game keeps rolling saves (_main.<n>.*); the highest number with an index file is current.
		int best = -1;
		foreach (string path in System.IO.Directory.GetFiles(directory, "_main.*.chunks"))
		{
			string[] parts = Path.GetFileName(path).Split('.');
			if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > best)
			{
				best = n;
			}
		}
		if (best < 0)
		{
			throw new InvalidDataException("No _main.<n>.chunks file found in " + directory + ". Is this a chunked (Deep North) world save?");
		}
		return best;
	}

	private void LoadChunks()
	{
		string indexPath = Path.Combine(Directory, $"_main.{SaveNumber}.chunks");
		List<(ushort Chunk, byte Size, uint Version, int Count)> chunks = new();
		using (ValheimReader index = new(File.OpenRead(indexPath)))
		{
			index.ReadUShort();
			index.ReadInt();
			int count = index.ReadInt();
			for (int i = 0; i < count; i++)
			{
				chunks.Add((index.ReadUShort(), index.ReadByte(), index.ReadUInt(), index.ReadInt()));
			}
		}
		foreach (var (chunk, size, version, numZdos) in chunks)
		{
			ChunkFile file = new() { Chunk = chunk, Size = size, Version = version, IndexCount = numZdos };
			string path = Path.Combine(Directory, file.FileName);
			if (!File.Exists(path))
			{
				throw new FileNotFoundException("Chunk file listed in the index is missing", path);
			}
			LoadChunk(path, file);
			Chunks.Add(file);
			ChunkCount++;
		}
	}

	// True when the world came from the live bridge plugin instead of save files.
	public bool IsLive { get; private set; }

	// The decompressed live snapshot; object byte ranges (ObjectRef.Start .. End) point into it.
	public byte[]? LiveBytes { get; private set; }

	// A snapshot from the WorldEditorBridge plugin (gzip): world info, ZoneSystem data and every
	// persistent object in the chunk file format, followed by each object's live ZDOID.
	public static WorldSave LoadLive(byte[] gzipped, string source)
	{
		using MemoryStream raw = new();
		using (GZipStream gz = new(new MemoryStream(gzipped), CompressionMode.Decompress))
		{
			gz.CopyTo(raw);
		}
		raw.Position = 0;
		WorldSave save = new() { Directory = source, SaveNumber = 0, IsLive = true, LiveBytes = raw.ToArray() };
		using ValheimReader pkg = new(raw);
		if (pkg.ReadInt() != 0x42455756)
		{
			throw new InvalidDataException("not a WorldEditorBridge snapshot");
		}
		int version = pkg.ReadInt();
		if (version != 1)
		{
			throw new InvalidDataException($"snapshot version {version} is not supported (update the editor)");
		}
		save.Name = pkg.ReadString();
		save.SeedName = pkg.ReadString();
		save.Seed = pkg.ReadInt();
		save.WorldGenVersion = pkg.ReadInt();
		double netTime = BitConverter.Int64BitsToDouble(pkg.ReadLong());
		save.Zones = ZoneDb.FromPackage(pkg.ReadBytes(pkg.ReadInt()), netTime);
		ChunkFile file = new() { Chunk = 0, Size = 0, Version = 0, IndexCount = 0 };
		save.ReadObjects(pkg, file);
		save.Chunks.Add(file);
		save.ChunkCount = 1;
		foreach (ObjectRef o in save.ObjectRefs)
		{
			o.LiveId = (pkg.ReadLong(), pkg.ReadUInt());
		}
		return save;
	}

	private void LoadChunk(string path, ChunkFile file)
	{
		using ValheimReader pkg = new(new BufferedStream(File.OpenRead(path), 1 << 20));
		ReadObjects(pkg, file);
	}

	private void ReadObjects(ValheimReader pkg, ChunkFile file)
	{
		file.WorldVersion = pkg.ReadShort();
		file.Count = pkg.ReadInt();
		for (int i = 0; i < file.Count; i++)
		{
			ReadZdo(pkg, file.WorldVersion, file);
			ObjectCount++;
		}
		file.Length = pkg.Position;
	}

	// Mirrors ZDO.Load(ZPackage, Version.World), recording where things are in the file so a
	// writer can later copy objects unchanged or patch just the terrain data.
	private void ReadZdo(ValheimReader pkg, int worldVersion, ChunkFile file)
	{
		long start = pkg.Position;
		int id = ObjectRefs.Count;
		ObjectRef objRef = new() { File = file, Start = start };
		ObjectRefs.Add(objRef);
		const ushort Connections = 0x1, Floats = 0x2, Vec3 = 0x4, Quats = 0x8, Ints = 0x10, Longs = 0x20, Strings = 0x40, ByteArrays = 0x80;
		const ushort Rotation = 0x1000, SmallPosition = 0x2000;
		bool chunked = worldVersion >= WorldVersion.ChunkedSave;
		ushort flags = pkg.ReadUShort();
		if (!chunked)
		{
			pkg.ReadVector2s();
		}
		Vector3 position;
		if ((flags & SmallPosition) != 0)
		{
			var (x, y) = pkg.ReadVector2s();
			position = new Vector3(x, 0f, y);
		}
		else
		{
			position = pkg.ReadVector3();
		}
		int prefab = pkg.ReadInt();
		Vector3 rotation = Vector3.Zero;
		if ((flags & Rotation) != 0)
		{
			rotation = chunked ? pkg.ReadSmallRotation() : pkg.ReadVector3();
		}
		objRef.DataStart = pkg.Position;
		objRef.Flags = flags;
		objRef.Prefab = prefab;
		objRef.Position = position;
		bool tracked = prefab == LocationProxyPrefab || ModifierPrefabs.Contains(prefab);
		int location = 0;
		long timeCreated = 0;
		long creator = 0, owner = 0, playerId = 0;
		// Zero = not stored: the object keeps its prefab's own scale.
		Vector3 scale = Vector3.Zero;
		if ((flags & 0xFF) == 0)
		{
			objRef.End = pkg.Position;
			Templates.TryAdd(prefab, id);
			file.Objects.Add((start, pkg.Position));
			if (!tracked && prefab != LocationProxyPrefab)
			{
				Objects.Add((id, prefab, position, rotation, scale));
			}
			if (tracked && prefab != LocationProxyPrefab)
			{
				Placed.Add(new PlacedObject(prefab, 0, position, rotation, 0));
			}
			return;
		}
		if ((flags & Connections) != 0)
		{
			pkg.ReadByte();
			pkg.ReadInt();
		}
		if ((flags & Floats) != 0)
		{
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				int key = pkg.ReadInt();
				float value = pkg.ReadSingle();
				if (key == ScaleScalarKey)
				{
					scale = new Vector3(value);
				}
			}
		}
		if ((flags & Vec3) != 0)
		{
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				int key = pkg.ReadInt();
				Vector3 value = pkg.ReadVector3();
				if (key == ScaleKey)
				{
					scale = value;
				}
			}
		}
		Skip(pkg, worldVersion, flags, Quats, p => p.SkipQuaternion());
		if ((flags & Ints) != 0)
		{
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				int key = pkg.ReadInt();
				int value = pkg.ReadInt();
				if (prefab == LocationProxyPrefab && key == LocationKey)
				{
					Locations.Add((position, value));
					location = value;
				}
				else if (key == TamedKey && value != 0)
				{
					Tamed.Add(id);
				}
			}
		}
		if ((flags & Longs) != 0)
		{
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				int key = pkg.ReadInt();
				long value = pkg.ReadLong();
				if (key == TimeCreatedKey)
				{
					timeCreated = value;
				}
				else if (key == CreatorKey)
				{
					creator = value;
				}
				else if (key == OwnerKey)
				{
					owner = value;
				}
				else if (key == PlayerIdKey)
				{
					playerId = value;
				}
			}
		}
		if ((flags & Strings) != 0)
		{
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				int key = pkg.ReadInt();
				string value = pkg.ReadString();
				long who = key == OwnerNameKey ? owner : key == CreatorNameKey ? creator : key == PlayerNameKey ? playerId : 0;
				if (who != 0 && value.Length > 0)
				{
					PlayerNames[who] = value;
				}
			}
		}
		if ((flags & ByteArrays) != 0)
		{
			int n = pkg.ReadNumItems(worldVersion);
			for (int i = 0; i < n; i++)
			{
				int key = pkg.ReadInt();
				long valueStart = pkg.Position;
				byte[] value = pkg.ReadByteArray();
				if (key == TCDataKey)
				{
					TerrainZone zone = TerrainZone.Decode(position, prefab, value);
					zone.Source = new ZdoLocation(file, start, -1, valueStart, pkg.Position, flags);
					TerrainZones.Add(zone);
				}
			}
		}
		long end = pkg.Position;
		objRef.End = end;
		file.Objects.Add((start, end));
		if (tracked && (prefab != LocationProxyPrefab || location != 0))
		{
			Placed.Add(new PlacedObject(prefab, location, position, rotation, timeCreated));
		}
		bool terrain = TerrainZones.Count > 0 && TerrainZones[^1].Source is { } last && last.File == file && last.Start == start;
		objRef.IsPiece = creator != 0;
		objRef.IsTerrain = terrain;
		if (!terrain)
		{
			Templates.TryAdd(prefab, id);
		}
		if (creator != 0)
		{
			Creators[creator] = Creators.GetValueOrDefault(creator) + 1;
			Pieces.Add((id, prefab, position, rotation.Y));
		}
		else if (!tracked && !terrain && prefab != LocationProxyPrefab)
		{
			Objects.Add((id, prefab, position, rotation, scale));
		}
		else if (prefab == LocationProxyPrefab && TerrainEditor.Editing.Uplift.IsDisc(location))
		{
			Discs.Add((id, position));
		}
		else if (prefab == LocationProxyPrefab && TerrainEditor.Terrain.PrefabCatalog.IsRunestone(location))
		{
			// A runestone: listed under its location's name (see PrefabCatalog.RunestoneLocations).
			Objects.Add((id, location, position, rotation, scale));
		}
		foreach (TerrainZone z in TerrainZones)
		{
			if (z.Source is { } src && src.File == file && src.Start == start && src.End < 0)
			{
				z.Source = src with { End = end };
			}
		}
	}

	private static void Skip(ValheimReader pkg, int worldVersion, ushort flags, ushort flag, Action<ValheimReader> readValue)
	{
		if ((flags & flag) == 0)
		{
			return;
		}
		int n = pkg.ReadNumItems(worldVersion);
		for (int i = 0; i < n; i++)
		{
			pkg.ReadInt();
			readValue(pkg);
		}
	}
}

public sealed record PlacedObject(int Prefab, int Location, Vector3 Position, Vector3 Rotation, long TimeCreated);

public sealed class ChunkFile
{
	public ushort Chunk { get; init; }

	public byte Size { get; init; }

	public uint Version { get; init; }

	public int IndexCount { get; init; }

	public int WorldVersion { get; set; }

	public int Count { get; set; }

	public long Length { get; set; }

	// Byte range of every object in the file, in file order.
	public List<(long Start, long End)> Objects { get; } = new();

	// Same naming as ChunkSaveMapping.GetChunkFilename.
	public string FileName => NameFor(Chunk, Size, Version);

	public static string NameFor(ushort chunk, byte size, uint version) => $"{chunk >> 8:x2}_{chunk & 0xFF:x2}__{size}_{version}.chunk";
}

// Where a terrain object lives in its chunk file, and where its TCData value (int length + bytes) is.
public sealed record ZdoLocation(ChunkFile File, long Start, long End, long DataStart, long DataEnd, ushort Flags);

// One zone's terrain modifications, decoded from TerrainComp's saved data.
public sealed class TerrainZone
{
	public ZdoLocation? Source { get; set; }

	public const float ZoneSize = 64f;

	public required Vector3 Center { get; init; }

	public int ZoneX => (int)MathF.Floor((Center.X + ZoneSize / 2f) / ZoneSize);

	public int ZoneZ => (int)MathF.Floor((Center.Z + ZoneSize / 2f) / ZoneSize);

	public int Prefab { get; init; }

	public int Operations { get; init; }

	// (Width+1)^2 vertices, row-major along world Z then X.
	public required bool[] ModifiedHeight { get; init; }

	public required float[] LevelDelta { get; init; }

	public required float[] SmoothDelta { get; init; }

	public required bool[] ModifiedPaint { get; init; }

	public required Vector4[] Paint { get; init; }

	public int HeightWidth => (int)Math.Round(Math.Sqrt(ModifiedHeight.Length));

	public int PaintWidth => (int)Math.Round(Math.Sqrt(ModifiedPaint.Length));

	// Mirrors TerrainComp.Load.
	public static TerrainZone Decode(Vector3 center, int prefab, byte[] compressed)
	{
		using MemoryStream raw = new();
		using (GZipStream gzip = new(new MemoryStream(compressed), CompressionMode.Decompress))
		{
			gzip.CopyTo(raw);
		}
		raw.Position = 0;
		using ValheimReader pkg = new(raw);
		pkg.ReadInt();
		int operations = pkg.ReadInt();
		pkg.ReadVector3();
		pkg.ReadSingle();
		int heights = pkg.ReadInt();
		bool[] modified = new bool[heights];
		float[] level = new float[heights];
		float[] smooth = new float[heights];
		for (int i = 0; i < heights; i++)
		{
			modified[i] = pkg.ReadBool();
			if (modified[i])
			{
				level[i] = pkg.ReadSingle();
				smooth[i] = pkg.ReadSingle();
			}
		}
		int paints = pkg.ReadInt();
		bool[] painted = new bool[paints];
		Vector4[] paint = new Vector4[paints];
		for (int i = 0; i < paints; i++)
		{
			painted[i] = pkg.ReadBool();
			if (painted[i])
			{
				paint[i] = new Vector4(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
			}
		}
		return new TerrainZone
		{
			Center = center,
			Prefab = prefab,
			Operations = operations,
			ModifiedHeight = modified,
			LevelDelta = level,
			SmoothDelta = smooth,
			ModifiedPaint = painted,
			Paint = paint
		};
	}
}
