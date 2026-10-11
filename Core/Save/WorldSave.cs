using System.Globalization;
using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// Read-only loader for Valheim's chunked world save (world version &gt;= 40):
/// <list type="bullet">
/// <item><description>_main.&lt;n&gt;.chunks: index of chunk files and how many objects each holds</description></item>
/// <item><description>XX_YY__&lt;size&gt;_&lt;version&gt;.chunk: objects (ZDOs) of one chunk</description></item>
/// </list>
/// Only what the terrain editor needs is kept: every object's prefab and position, and the terrain modification data
/// ("TCData") of each zone's terrain compiler object.
/// </summary>
public sealed class WorldSave
{
    /// <summary>Data key of a terrain compiler's saved ground edits.</summary>
    private static readonly int TCDataKey = StableHash.Of("TCData");

    /// <summary>Prefab hash of the proxy object that stands for a placed location.</summary>
    public static readonly int LocationProxyPrefab = StableHash.Of("LocationProxy");

    /// <summary>Int data key on a location proxy: the location's name hash.</summary>
    public static readonly int LocationKey = StableHash.Of("location");

    /// <summary>Long data key of a terrain modifier's creation time.</summary>
    private static readonly int TimeCreatedKey = StableHash.Of("terrainModifierTimeCreated");

    /// <summary>Long data key of the player id that built a piece.</summary>
    private static readonly int CreatorKey = StableHash.Of("creator");

    /// <summary>
    /// Player ids and the names stored with them: bed and tombstone owners, ward builders, players (live: the players
    /// that are online).
    /// </summary>
    private static readonly int OwnerKey = StableHash.Of("owner"), OwnerNameKey = StableHash.Of("ownerName");
    /// <summary>String data key of the builder's name stored with <c>creator</c>.</summary>
    private static readonly int CreatorNameKey = StableHash.Of("creatorName");
    /// <summary>Player id and name keys on player objects.</summary>
    private static readonly int PlayerIdKey = StableHash.Of("playerID"), PlayerNameKey = StableHash.Of("playerName");

    /// <summary>Vec3 data key of a per-axis scale.</summary>
    private static readonly int ScaleKey = StableHash.Of("scale");

    /// <summary>Float data key of a uniform scale.</summary>
    private static readonly int ScaleScalarKey = StableHash.Of("scaleScalar");

    /// <summary>Tameable.SetTamed writes it (an int, 1 when tamed).</summary>
    private static readonly int TamedKey = StableHash.Of("tamed");

    /// <summary>Prefabs (besides LocationProxy) whose objects carry terrain modifiers; set before loading.</summary>
    public static HashSet<int> ModifierPrefabs { get; set; } = [];

    /// <summary>
    /// The world's folder, or for a live world a description of its source ("live: " and the bridge address).
    /// </summary>
    public required string Directory { get; init; }

    /// <summary>The rolling save number (the n of _main.n.*) loaded; 0 for a live world.</summary>
    public required int SaveNumber { get; init; }

    /// <summary>The world's name from its metadata.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The seed as typed in the game.</summary>
    public string SeedName { get; private set; } = "";

    /// <summary>The seed number the generator uses (the seed name's stable hash).</summary>
    public int Seed { get; private set; }

    /// <summary>World.m_worldGenVersion (0 for metadata older than version 26).</summary>
    public int WorldGenVersion { get; private set; }

    /// <summary>How many objects were read (live: grows as new objects come in).</summary>
    public int ObjectCount { get; private set; }

    /// <summary>How many chunk files were read (1 for a live world).</summary>
    public int ChunkCount { get; private set; }

    /// <summary>Every object with saved ground data (TCData), one entry each: a zone can have two.</summary>
    public List<TerrainZone> TerrainZones { get; } = [];

    /// <summary>The chunk files read, in index order (live: the one snapshot file).</summary>
    public List<ChunkFile> Chunks { get; } = [];

    /// <summary>
    /// Placed locations (villages, trader, dungeons...): their terrain flattening is applied live by the game and is
    /// not part of the saved terrain data.
    /// </summary>
    public List<(Vector3 Position, int Location)> Locations { get; } = [];

    /// <summary>The ground discs the editor placed for No limit ground (Uplift): invisible location proxies.</summary>
    public List<(int Id, Vector3 Position)> Discs { get; } = [];

    /// <summary>
    /// Every placed object that can modify terrain while the game runs: locations and the prefabs in ModifierPrefabs.
    /// Rotation is Euler degrees, as stored.
    /// </summary>
    public List<PlacedObject> Placed { get; } = [];

    /// <summary>Objects that are tamed creatures (by id).</summary>
    public HashSet<int> Tamed { get; } = [];

    /// <summary>Player-built pieces (objects with a creator): prefab, position and Y rotation in degrees.</summary>
    public List<(int Id, int Prefab, Vector3 Position, float RotationY)> Pieces { get; } = [];

    /// <summary>Pieces per builder (the "creator" player id).</summary>
    public Dictionary<long, int> Creators { get; } = [];

    /// <summary>Player names by player id, from the objects that store both.</summary>
    public Dictionary<long, string> PlayerNames { get; } = [];

    /// <summary>
    /// The player id written as "creator" on new pieces (things built with the hammer, hoe, cultivator or serving
    /// tray), so the game treats them as player built. Chosen in the editor; 0: not set.
    /// </summary>
    public static long Builder { get; set; }

    /// <summary>Int data key ItemDrop.MakePiece sets on items set out as pieces.</summary>
    private static readonly int PieceKey = StableHash.Of("piece");

    /// <summary>The player who built the most pieces in this world (0 if nobody did).</summary>
    public long TopBuilder => Creators.Count == 0 ? 0 : Creators.MaxBy(c => c.Value).Key;

    /// <summary>Every object in the save by id (its order in the save).</summary>
    public List<ObjectRef> ObjectRefs { get; } = [];

    /// <summary>First object of each prefab: the template new objects of that prefab are copied from.</summary>
    public Dictionary<int, int> Templates { get; } = [];

    /// <summary>
    /// The object a new one is built from: its source object when given (and of the same prefab), else the prefab's
    /// template. null when the world has no object of the prefab: it is then built blank (NewObjectBytes).
    /// </summary>
    public ObjectRef? ModelFor(int prefab, int? sourceId)
    {
        if (sourceId is int s && s >= 0 && s < ObjectRefs.Count && ObjectRefs[s].Prefab == prefab)
        {
            return ObjectRefs[s];
        }
        return Templates.TryGetValue(prefab, out int t) ? ObjectRefs[t] : null;
    }

    /// <summary>
    /// Whether the editor can make an object of this prefab: the world has one to copy, or the game's catalogue knows
    /// it (placeable prefab or creature).
    /// </summary>
    public bool CanCreate(int prefab) => Templates.ContainsKey(prefab) || Terrain.PrefabCatalog.Get(prefab) != null || Terrain.PrefabCatalog.IsCreature(prefab);

    /// <summary>Prefabs that can be created: any object in the world, and every placeable game prefab.</summary>
    public IEnumerable<int> Creatable => Templates.Keys.Concat(Terrain.PrefabCatalog.Placeable.Select(p => StableHash.Of(p.Name))).Distinct();

    /// <summary>
    /// The save bytes of a new object: a copy of its model when the world has one, else a blank object with the
    /// prefab's own flags from the game (what the game writes for a freshly placed object).
    /// </summary>
    /// <param name="n">The new object.</param>
    /// <param name="readSource">Gives the chunk file bytes the model lives in.</param>
    /// <returns>The bytes, or null when the prefab is unknown to both the world and the game.</returns>
    public byte[]? NewObjectBytes(Editing.NewObject n, Func<ObjectRef, byte[]> readSource)
    {
        byte[]? bytes = BuildBytes(n, readSource);
        // A new piece of a kind players build (placed, pasted or built blank) is the chosen builder's,
        // like one built in the game. Without a builder the game takes it for part of a ruin: a third of
        // the materials back, no base for fires, ignored by raids. Moved and edited objects keep theirs.
        if (bytes != null && Builder != 0 && (n.Fresh || n.Raw == null && ModelFor(n.Prefab, n.SourceId) == null)
            && Terrain.PieceCatalog.Get(n.Prefab)?.Tool != null)
        {
            var z = ZdoData.Parse(bytes);
            z.Set("longs", CreatorKey, Builder.ToString(CultureInfo.InvariantCulture));
            bytes = z.Serialize();
        }
        // Food and drink set out with the serving tray (Feaster) are items made pieces: the game marks
        // them with "piece" (ItemDrop.MakePiece). Without it they would be loose items, falling and
        // despawning away from a base.
        if (bytes != null && (n.Fresh || n.Raw == null && ModelFor(n.Prefab, n.SourceId) == null)
            && Terrain.PieceCatalog.Get(n.Prefab)?.Tool == "feaster" && Terrain.PrefabCatalog.ItemKindOf(n.Prefab) != null)
        {
            var z = ZdoData.Parse(bytes);
            z.Set("ints", PieceKey, "1");
            bytes = z.Serialize();
        }
        if (bytes != null && n.Data is { Count: > 0 } data)
        {
            var z = ZdoData.Parse(bytes);
            foreach (var f in data)
            {
                z.Set(f.Section, f.Key, f.Value);
            }
            bytes = z.Serialize();
        }
        return bytes;
    }

    /// <summary>
    /// The new object's bytes before the builder and data fields: its raw bytes moved, else a copy of its model, else a
    /// blank object from the catalogue (null when the game does not know the prefab).
    /// </summary>
    private byte[]? BuildBytes(Editing.NewObject n, Func<ObjectRef, byte[]> readSource)
    {
        if (n.Raw != null)
        {
            var z = ZdoData.Parse(n.Raw);
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
        // Also what is not offered for placing (creatures: generated dungeons put some down).
        return Terrain.PrefabCatalog.Details(n.Prefab) is { } info ? ZdoBuilder.Blank(n.Prefab, info.Flags, n.Position, n.Rotation, n.Scale) : null;
    }

    /// <summary>
    /// The saved bytes of an object of the save (id &gt;= 0): from its chunk file, or from the live snapshot.
    /// </summary>
    public byte[] ObjectBytes(int id)
    {
        ObjectRef o = ObjectRefs[id];
        if (LiveBytes != null)
        {
            return LiveSource(o.File)[(int)o.Start..(int)o.End];
        }
        using FileStream fs = File.OpenRead(Path.Combine(Directory, o.File.FileName));
        byte[] b = new byte[o.End - o.Start];
        fs.Seek(o.Start, SeekOrigin.Begin);
        fs.ReadExactly(b);
        return b;
    }

    /// <summary>Generated zones and location instances from the .db2 file (null if it could not be read).</summary>
    public ZoneDb? Zones { get; private set; }

    /// <summary>
    /// Everything else that is not terrain data (trees, rocks, bushes, pickables...): prefab, position, rotation (Euler
    /// degrees) and scale (zero when the prefab's own scale applies).
    /// </summary>
    public List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> Objects { get; } = [];

    /// <summary>
    /// Loads the current save of a world folder (see <c>CommittedSave</c>); a .db2 that cannot be read leaves
    /// <c>Zones</c> null.
    /// </summary>
    /// <exception cref="InvalidDataException">The folder holds no chunked world save.</exception>
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

    /// <summary>Mirrors World.LoadWorld: an int length prefix, then a ZPackage with version, names and seed.</summary>
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

    /// <summary>The save number to load, as <c>CommittedSave</c> picks it.</summary>
    /// <exception cref="InvalidDataException">No save index is in the folder.</exception>
    private static int FindLatestSave(string directory)
    {
        int best = CommittedSave(directory);
        if (best < 0)
        {
            throw new InvalidDataException("No _main.<n>.chunks file found in " + directory + ". Is this a chunked (Deep North) world save?");
        }
        return best;
    }

    /// <summary>
    /// The game keeps rolling saves (_main.&lt;n&gt;.*); the current one is the highest number with an index file and
    /// the commit marker (.ok, written last: a save without it was cut short, or is being written right now). A folder
    /// where no save has the marker: the highest index. -1: none.
    /// </summary>
    public static int CommittedSave(string directory)
    {
        int best = -1, committed = -1;
        foreach (string path in System.IO.Directory.GetFiles(directory, "_main.*.chunks"))
        {
            string[] parts = Path.GetFileName(path).Split('.');
            if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int n))
            {
                best = Math.Max(best, n);
                if (n > committed && File.Exists(Path.Combine(directory, $"_main.{n}.ok")))
                {
                    committed = n;
                }
            }
        }
        return committed >= 0 ? committed : best;
    }

    /// <summary>Reads the .chunks index, then every chunk file it lists.</summary>
    /// <exception cref="FileNotFoundException">A chunk file the index lists is missing.</exception>
    private void LoadChunks()
    {
        string indexPath = Path.Combine(Directory, $"_main.{SaveNumber}.chunks");
        List<(ushort Chunk, byte Size, uint Version, int Count)> chunks = [];
        using (ValheimReader index = new(File.OpenRead(indexPath)))
        {
            index.ReadUShort();
            index.ReadInt();
            // Each: chunk (2), size (1), version (4), object count (4).
            int count = index.ReadCount(11);
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

    /// <summary>True when the world came from the live bridge plugin instead of save files.</summary>
    public bool IsLive { get; private set; }

    /// <summary>The decompressed live snapshot; object byte ranges (ObjectRef.Start .. End) point into it.</summary>
    public byte[]? LiveBytes { get; private set; }

    /// <summary>
    /// Live: the bytes an object's offsets are in, the snapshot's or those of objects read since from the game
    /// (MergeLive: each read is its own little file).
    /// </summary>
    private readonly Dictionary<ChunkFile, byte[]> _liveFiles = [];

    /// <summary>
    /// Live: the bytes a chunk file's object offsets point into (the snapshot unless MergeLive made the file).
    /// </summary>
    public byte[] LiveSource(ChunkFile file) => _liveFiles.TryGetValue(file, out byte[]? b) ? b : LiveBytes!;

    /// <summary>
    /// Live: objects the game no longer has (removed in the game). Left out of every list; their ids stay taken, and
    /// their bytes stay readable (history).
    /// </summary>
    public HashSet<int> Vanished { get; } = [];

    /// <summary>
    /// Live: held while the world's lists change (MergeLive) and while other threads read them (an area loading, the
    /// map's search and statistics): what the game changed comes in between.
    /// </summary>
    public object Sync { get; } = new();

    /// <summary>Live: the objects by ZDOID and by zone (not the vanished ones), made at the first merge.</summary>
    private Dictionary<(long, uint), int>? _byLiveId;
    /// <summary>Live: object ids by zone; see <c>_byLiveId</c>.</summary>
    private Dictionary<(int, int), HashSet<int>>? _byZone;

    /// <summary>Builds the ZDOID and zone lookups once, from every object not vanished.</summary>
    private void Index()
    {
        if (_byLiveId != null)
        {
            return;
        }
        _byLiveId = [];
        _byZone = [];
        for (int id = 0; id < ObjectRefs.Count; id++)
        {
            if (!Vanished.Contains(id))
            {
                Track(id);
            }
        }
    }

    /// <summary>Adds an object to the ZDOID and zone lookups.</summary>
    private void Track(int id)
    {
        ObjectRef o = ObjectRefs[id];
        _byLiveId![o.LiveId] = id;
        (_byZone!.TryGetValue(o.Zone, out var set) ? set : _byZone[o.Zone] = []).Add(id);
    }

    /// <summary>Removes an object from the ZDOID and zone lookups.</summary>
    private void Untrack(int id)
    {
        ObjectRef o = ObjectRefs[id];
        _byLiveId!.Remove(o.LiveId);
        if (_byZone!.TryGetValue(o.Zone, out var set))
        {
            set.Remove(id);
        }
    }

    /// <summary>
    /// Live: the object is in the game under another ZDOID now (the editor made it again there: an applied delete
    /// undone); followed by that one from then on.
    /// </summary>
    public void SetLiveId(int id, (long User, uint Id) zdo)
    {
        lock (Sync)
        {
            if (_byLiveId != null && !Vanished.Contains(id))
            {
                Untrack(id);
                ObjectRefs[id].LiveId = zdo;
                Track(id);
            }
            else
            {
                ObjectRefs[id].LiveId = zdo;
            }
        }
    }

    /// <summary>What a <c>MergeLive</c> call changed.</summary>
    /// <param name="Added">Objects new to the editor (new ids).</param>
    /// <param name="Updated">Objects the game changed (data, place), read again under their own id.</param>
    /// <param name="Vanished">Objects gone from the game.</param>
    /// <param name="TerrainZones">Zones whose ground data came or went.</param>
    public sealed record Merged(List<int> Added, List<int> Updated, List<int> Vanished, HashSet<(int X, int Z)> TerrainZones)
    {
        /// <summary>Objects that shape the ground as the game runs came, went or changed.</summary>
        public bool Modifiers { get; set; }
    }

    /// <summary>
    /// Live: the zones' objects as the game has them now (ZDOID and bytes, as the plugin's /zone sends them), merged
    /// in. Null when the world's lists are being read elsewhere right now: try again later.
    /// </summary>
    /// <param name="zones">The zones the objects were read from; objects of them the game lacks vanish.</param>
    /// <param name="objects">Every object the game has in those zones.</param>
    /// <param name="keep">
    /// Objects not to vanish though the game lacks them (the editor's own applied deletions: undo brings them back).
    /// </param>
    /// <param name="ours">ZDOIDs of objects the editor made itself (its new objects).</param>
    public Merged? MergeLive(IReadOnlyCollection<(int X, int Z)> zones, IReadOnlyList<(long User, uint Id, byte[] Bytes)> objects,
        Func<int, bool> keep, Func<(long User, uint Id), bool> ours)
    {
        if (!Monitor.TryEnter(Sync))
        {
            return null;
        }
        try
        {
            Index();
            var result = new Merged([], [], [], []);
            var seen = new HashSet<int>();
            var fresh = new List<(long User, uint Id, byte[] Bytes, int Replace)>();
            foreach (var (user, zid, bytes) in objects)
            {
                if (_byLiveId!.TryGetValue((user, zid), out int id))
                {
                    seen.Add(id);
                    if (!ObjectBytes(id).AsSpan().SequenceEqual(bytes))
                    {
                        fresh.Add((user, zid, bytes, id));
                    }
                }
                else if (!ours((user, zid)))
                {
                    fresh.Add((user, zid, bytes, -1));
                }
            }
            var gone = zones.SelectMany(z => _byZone!.TryGetValue(z, out var set) ? set : Enumerable.Empty<int>()).Where(id => !seen.Contains(id) && !keep(id)).ToList();
            // Out of the lists at once (one pass each): the vanished, and the changed ones read again below.
            var drop = gone.Concat(fresh.Where(f => f.Replace >= 0).Select(f => f.Replace)).ToHashSet();
            if (drop.Count > 0)
            {
                Drop(drop, result);
            }
            foreach (int id in gone)
            {
                Untrack(id);
                Vanished.Add(id);
                result.Vanished.Add(id);
            }
            if (fresh.Count > 0)
            {
                byte[] all = [.. fresh.SelectMany(f => f.Bytes)];
                ChunkFile file = new() { Chunk = 0, Size = 0, Version = 0, IndexCount = 0, WorldVersion = Chunks.Count > 0 ? Chunks[0].WorldVersion : 41, Count = fresh.Count, Length = all.Length };
                _liveFiles[file] = all;
                using ValheimReader pkg = new(new MemoryStream(all));
                foreach (var (user, zid, _, replace) in fresh)
                {
                    if (replace >= 0)
                    {
                        Untrack(replace);
                    }
                    int zonesBefore = TerrainZones.Count;
                    int id = ReadZdo(pkg, file.WorldVersion, file, replace);
                    ObjectRefs[id].LiveId = (user, zid);
                    Track(id);
                    (replace >= 0 ? result.Updated : result.Added).Add(id);
                    if (replace < 0)
                    {
                        ObjectCount++;
                    }
                    if (TerrainZones.Count > zonesBefore)
                    {
                        var z = TerrainZones[^1];
                        result.TerrainZones.Add((z.ZoneX, z.ZoneZ));
                    }
                    result.Modifiers |= Shapes(ObjectRefs[id].Prefab);
                }
            }
            return result;
        }
        finally
        {
            Monitor.Exit(Sync);
        }
    }

    /// <summary>
    /// Whether objects of this prefab shape the ground as the game runs (location proxies and ModifierPrefabs).
    /// </summary>
    private static bool Shapes(int prefab) => prefab == LocationProxyPrefab || ModifierPrefabs.Contains(prefab);

    /// <summary>These objects out of every list (their ids stay; the changed ones are read again after).</summary>
    private void Drop(HashSet<int> ids, Merged result)
    {
        Objects.RemoveAll(x => ids.Contains(x.Id));
        Pieces.RemoveAll(x => ids.Contains(x.Id));
        Discs.RemoveAll(x => ids.Contains(x.Id));
        foreach (int id in ids)
        {
            ObjectRef o = ObjectRefs[id];
            Tamed.Remove(id);
            if (o.Creator != 0 && Creators.TryGetValue(o.Creator, out int n))
            {
                Creators[o.Creator] = n - 1;
            }
            if (o.Prefab == LocationProxyPrefab)
            {
                int at = Locations.FindIndex(l => Vector3.DistanceSquared(l.Position, o.Position) < 1e-4f);
                if (at >= 0)
                {
                    Locations.RemoveAt(at);
                }
            }
            if (Shapes(o.Prefab))
            {
                result.Modifiers = true;
                int placed = Placed.FindIndex(x => x.Prefab == o.Prefab && Vector3.DistanceSquared(x.Position, o.Position) < 1e-4f);
                if (placed >= 0)
                {
                    Placed.RemoveAt(placed);
                }
            }
        }
        var sources = ids.Select(id => (ObjectRefs[id].File, ObjectRefs[id].Start)).ToHashSet();
        foreach (var z in TerrainZones.Where(z => z.Source is { } src && sources.Contains((src.File, src.Start))))
        {
            result.TerrainZones.Add((z.ZoneX, z.ZoneZ));
        }
        TerrainZones.RemoveAll(z => z.Source is { } src && sources.Contains((src.File, src.Start)));
    }

    // The largest live snapshot read, unpacked: the biggest worlds' saves are a few hundred megabytes,
    // and one array cannot pass 2 GiB anyway.
    private const int MaxSnapshot = 1536 << 20;

    /// <summary>Loads a world from a WorldEditorBridge snapshot (gzip-compressed, snapshot version 1).</summary>
    /// <param name="gzipped">The snapshot as <c>LiveBridge.Snapshot</c> returns it.</param>
    /// <param name="source">What to show as the world's <c>Directory</c>.</param>
    /// <exception cref="InvalidDataException">
    /// Not a bridge snapshot, or a version this editor does not read.
    /// </exception>
    public static WorldSave LoadLive(byte[] gzipped, string source)
    {
        byte[] raw = CheckedReads.Gunzip(gzipped, MaxSnapshot);
        WorldSave save = new() { Directory = source, SaveNumber = 0, IsLive = true, LiveBytes = raw };
        using ValheimReader pkg = new(new MemoryStream(raw));
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

    /// <summary>The objects of one chunk file, read alone: the fuzz tests' way in.</summary>
    internal static WorldSave ReadChunk(Stream stream)
    {
        WorldSave save = new() { Directory = "", SaveNumber = 0 };
        ChunkFile file = new() { Chunk = 0, Size = 0, Version = 0, IndexCount = 0 };
        using ValheimReader pkg = new(stream);
        save.ReadObjects(pkg, file);
        save.Chunks.Add(file);
        return save;
    }

    /// <summary>Reads every object of one chunk file.</summary>
    private void LoadChunk(string path, ChunkFile file)
    {
        using ValheimReader pkg = new(new BufferedStream(File.OpenRead(path), 1 << 20));
        ReadObjects(pkg, file);
    }

    /// <summary>
    /// A chunk's objects: its world version, the object count, then each object; records the file's length.
    /// </summary>
    private void ReadObjects(ValheimReader pkg, ChunkFile file)
    {
        file.WorldVersion = pkg.ReadShort();
        // The smallest object: flags (2), a small position (4), its prefab (4).
        file.Count = pkg.ReadCount(10);
        for (int i = 0; i < file.Count; i++)
        {
            ReadZdo(pkg, file.WorldVersion, file);
            ObjectCount++;
        }
        file.Length = pkg.Position;
    }

    /// <summary>
    /// Mirrors ZDO.Load(ZPackage, Version.World), recording where things are in the file so a writer can later copy
    /// objects unchanged or patch just the terrain data.
    /// </summary>
    /// <param name="pkg">Reader positioned at the object.</param>
    /// <param name="worldVersion">The world version the object was written in.</param>
    /// <param name="file">The chunk file the bytes belong to.</param>
    /// <param name="replace">Read in place of that object (live: changed in the game), else a new one.</param>
    /// <returns>The object's id.</returns>
    private int ReadZdo(ValheimReader pkg, int worldVersion, ChunkFile file, int replace = -1)
    {
        long start = pkg.Position;
        int id = replace >= 0 ? replace : ObjectRefs.Count;
        ObjectRef objRef = new() { File = file, Start = start };
        if (replace >= 0)
        {
            objRef.LiveId = ObjectRefs[id].LiveId;
            ObjectRefs[id] = objRef;
        }
        else
        {
            ObjectRefs.Add(objRef);
        }
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
            return id;
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
                    var zone = TerrainZone.Decode(position, prefab, value);
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
        objRef.Creator = creator;
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
        else if (prefab == LocationProxyPrefab && Editing.Uplift.IsDisc(location))
        {
            Discs.Add((id, position));
        }
        else if (prefab == LocationProxyPrefab && Terrain.PrefabCatalog.IsRunestone(location))
        {
            // A runestone: listed under its location's name (see PrefabCatalog.RunestoneLocations).
            Objects.Add((id, location, position, rotation, scale));
        }
        // Only this object's own terrain data can still lack its end (the last zone added, if it is this
        // object's): looking through every zone for each object made loading a large world slow.
        if (terrain && TerrainZones[^1].Source is { End: < 0 } src)
        {
            TerrainZones[^1].Source = src with { End = end };
        }
        return id;
    }

    /// <summary>Skips one data section (count, then key and value each) when its flag is set.</summary>
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

/// <summary>An object that can modify terrain while the game runs.</summary>
/// <param name="Prefab">Its prefab hash.</param>
/// <param name="Location">For a location proxy, the location's name hash; else 0.</param>
/// <param name="Position">World position in metres.</param>
/// <param name="Rotation">Euler degrees, as stored.</param>
/// <param name="TimeCreated">Its terrainModifierTimeCreated value, 0 when not stored.</param>
public sealed record PlacedObject(int Prefab, int Location, Vector3 Position, Vector3 Rotation, long TimeCreated);

/// <summary>One chunk file of a save, as its index lists it and as read.</summary>
public sealed class ChunkFile
{
    /// <summary>The base chunk id (see ChunkMath).</summary>
    public ushort Chunk { get; init; }

    /// <summary>How many low bits of x and y the chunk merges (see ChunkMath).</summary>
    public byte Size { get; init; }

    /// <summary>The file's version number, part of its name.</summary>
    public uint Version { get; init; }

    /// <summary>The object count the index lists for the file.</summary>
    public int IndexCount { get; init; }

    /// <summary>The world version written at the start of the file.</summary>
    public int WorldVersion { get; set; }

    /// <summary>The object count read from the file itself.</summary>
    public int Count { get; set; }

    /// <summary>Bytes read from the file, header and objects.</summary>
    public long Length { get; set; }

    /// <summary>Byte range of every object in the file, in file order.</summary>
    public List<(long Start, long End)> Objects { get; } = [];

    /// <summary>Same naming as ChunkSaveMapping.GetChunkFilename.</summary>
    public string FileName => NameFor(Chunk, Size, Version);

    /// <summary>A chunk file's name: chunk high and low byte in hex, then size and version.</summary>
    public static string NameFor(ushort chunk, byte size, uint version) => $"{chunk >> 8:x2}_{chunk & 0xFF:x2}__{size}_{version}.chunk";
}

/// <summary>
/// Where a terrain object lives in its chunk file, and where its TCData value (int length + bytes) is.
/// </summary>
/// <param name="File">The chunk file.</param>
/// <param name="Start">Where the object starts.</param>
/// <param name="End">Where the object ends (-1 while it is still being read).</param>
/// <param name="DataStart">Where the TCData value starts.</param>
/// <param name="DataEnd">Where the TCData value ends.</param>
/// <param name="Flags">The object's ZDO flags.</param>
public sealed record ZdoLocation(ChunkFile File, long Start, long End, long DataStart, long DataEnd, ushort Flags);

/// <summary>One zone's terrain modifications, decoded from TerrainComp's saved data.</summary>
public sealed class TerrainZone
{
    /// <summary>Where the data sits in the save (set by the loader once the value is read).</summary>
    public ZdoLocation? Source { get; set; }

    /// <summary>A zone's side in metres.</summary>
    public const float ZoneSize = 64f;

    /// <summary>The terrain compiler object's position: the zone's centre.</summary>
    public required Vector3 Center { get; init; }

    /// <summary>The zone's x index.</summary>
    public int ZoneX => (int)MathF.Floor((Center.X + ZoneSize / 2f) / ZoneSize);

    /// <summary>The zone's z index.</summary>
    public int ZoneZ => (int)MathF.Floor((Center.Z + ZoneSize / 2f) / ZoneSize);

    /// <summary>The prefab hash of the terrain compiler object.</summary>
    public int Prefab { get; init; }

    /// <summary>TerrainComp's count of operations applied to the zone.</summary>
    public int Operations { get; init; }

    /// <summary>(Width+1)^2 vertices, row-major along world Z then X.</summary>
    public required bool[] ModifiedHeight { get; init; }

    /// <summary>Level change per vertex, in metres (0 where not modified).</summary>
    public required float[] LevelDelta { get; init; }

    /// <summary>Smoothing change per vertex, in metres (0 where not modified).</summary>
    public required float[] SmoothDelta { get; init; }

    /// <summary>Which points of the paint grid carry paint.</summary>
    public required bool[] ModifiedPaint { get; init; }

    /// <summary>Paint mask per point of the paint grid (RGBA, as saved; zero where not painted).</summary>
    public required Vector4[] Paint { get; init; }

    /// <summary>Vertices per row of the height grid.</summary>
    public int HeightWidth => (int)Math.Round(Math.Sqrt(ModifiedHeight.Length));

    /// <summary>Points per row of the paint grid.</summary>
    public int PaintWidth => (int)Math.Round(Math.Sqrt(ModifiedPaint.Length));

    // The largest terrain data read, unpacked: a zone's is about 110 KB (65 x 65 heights and paints).
    private const int MaxData = 16 << 20;

    /// <summary>Mirrors TerrainComp.Load.</summary>
    /// <param name="center">The terrain compiler object's position.</param>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="compressed">The gzip-compressed TCData value.</param>
    public static TerrainZone Decode(Vector3 center, int prefab, byte[] compressed)
    {
        using ValheimReader pkg = new(new MemoryStream(CheckedReads.Gunzip(compressed, MaxData)));
        pkg.ReadInt();
        int operations = pkg.ReadInt();
        pkg.ReadVector3();
        pkg.ReadSingle();
        int heights = pkg.ReadCount(1);
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
        int paints = pkg.ReadCount(1);
        bool[] painted = new bool[paints];
        var paint = new Vector4[paints];
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
