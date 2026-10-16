using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Save;

/// <summary>
/// A brand-new world from a seed, in the chunked save format the editor reads (as the game with the Deep North save
/// writes it, see WorldSave): save #1 with its metadata (.fwl2), its zone list and the random events' state (.db2), its
/// chunk index (.chunks) and the commit marker (.ok), written last. The ground is the seed's own (the world generator);
/// the zones within <c>radius</c> of the middle are generated with the game's vegetation rules (Regrow), the rest is
/// left for the game to generate; zone 0, 0 also gets an unedited terrain compiler object, so the editor can save
/// ground edits. The seed's locations in the generated zones (the start temple, altars, ruins...) are spawned as the
/// game would spawn them (Sites), and each generated zone gets its zone control object, as the game's own have; a zone
/// whose location the editor cannot make (a dungeon's entrance, whose interior the game lays out) is left whole to the
/// game. The world is marked as not having its locations laid out yet, so the game lays out the rest when it first
/// loads it.
/// </summary>
public static class WorldCreator
{
    /// <summary>The save format version written into every file of the new world.</summary>
    public const int SaveFileVersion = 41;

    /// <summary>
    /// The generator's version for new worlds (World.m_worldGenVersion) and the location list's
    /// (ZoneSystem.m_locationVersion), as the game writes them today.
    /// </summary>
    public const int WorldGenVersion = 2, LocationVersion = 32;

    /// <summary>The largest square of generated zones around the middle (radius in zones).</summary>
    public const int MaxRadius = 16;

    /// <summary>What <c>Create</c> made.</summary>
    /// <param name="Directory">The world's folder.</param>
    /// <param name="Seed">The seed number of the seed name.</param>
    /// <param name="Zones">How many zones were generated (0 with radius 0).</param>
    /// <param name="Objects">How many objects were written, the terrain compiler object included.</param>
    /// <param name="Flat">The bare zone left for pictures, or null when none was asked for.</param>
    /// <param name="FlatSpan">
    /// How many metres the bare zone's ground spans from lowest to highest (0 without one).
    /// </param>
    /// <param name="Locations">How many locations were spawned in the generated zones.</param>
    public sealed record Created(string Directory, int Seed, int Zones, int Objects, (int X, int Z)? Flat = null, float FlatSpan = 0, int Locations = 0);

    /// <summary>
    /// The most even zone of dry Meadows within <c>radius</c> zones of the middle (not on its edge), none of
    /// <paramref name="avoid"/>: the one whose ground spans the fewest metres. Null when none is wholly above the water.
    /// </summary>
    /// <param name="terrain">The world's terrain.</param>
    /// <param name="radius">How far from the middle to look, in zones.</param>
    /// <param name="avoid">Zones not to take (those holding a location); none by default.</param>
    /// <returns>
    /// The zone and its ground's span in metres (dry: its lowest point at least 1.5 m above the water).
    /// </returns>
    public static (int X, int Z, float Span)? FlattestZone(ValheimGen.TerrainService terrain, int radius, IReadOnlySet<(int X, int Z)>? avoid = null)
    {
        (int, int, float)? best = null;
        for (int z = -radius + 1; z < radius; z++)
        {
            for (int x = -radius + 1; x < radius; x++)
            {
                if (avoid?.Contains((x, z)) == true)
                {
                    continue;
                }
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

    /// <summary>The game's seed number for a seed name (World: m_seed = m_seedName.GetStableHashCode()).</summary>
    public static int SeedOf(string seedName) => StableHash.Of(seedName);

    /// <summary>
    /// After the random events' state, what the game's save holds: an empty list, brotli-compressed JSON {"list":[]}
    /// behind its length (copied from a world the game made).
    /// </summary>
    private static readonly byte[] EmptyListBlock = [0x0f, 0, 0, 0, 0x0b, 0x05, 0x80, (byte)'{', (byte)'"', (byte)'l', (byte)'i', (byte)'s', (byte)'t', (byte)'"', (byte)':', (byte)'[', (byte)']', (byte)'}', 0x03];

    /// <summary>
    /// The characters no file name may hold on Windows (Linux and macOS forbid only / and NUL, but a world is copied
    /// between systems).
    /// </summary>
    private static readonly char[] ForbiddenInNames = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// The names Windows keeps for devices, which no file may have, with any extension (the superscript digits
    /// count as digits there too, and the console's input and output have their own).
    /// </summary>
    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
    ];

    /// <summary>
    /// Whether a world name can be a file name on every system the world may be copied to, the same on each of
    /// them: not empty, none of / \ : * ? " &lt; &gt; | or a control character, not ending with a dot or a space, and
    /// not one of Windows' device names (CON, NUL, CONIN$, COM1…, COM¹…) with or without an extension. The game names the
    /// world's files after it.
    /// </summary>
    /// <param name="name">The name, as typed (not trimmed).</param>
    public static bool IsWorldName(string name)
    {
        if (name.Length == 0 || name[^1] is '.' or ' ' || name.Any(c => c < ' ' || c == '\x7f' || ForbiddenInNames.Contains(c)))
        {
            return false;
        }
        return !DeviceNames.Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Writes a new world into a folder that holds none yet.</summary>
    /// <param name="folder">The folder to write into; created when missing.</param>
    /// <param name="name">The world's name, which must also be a file name on every system (<c>IsWorldName</c>).</param>
    /// <param name="seedName">The seed, 1 to 10 characters as in the game.</param>
    /// <param name="radius">
    /// How many zones from the middle (0 to <c>MaxRadius</c>) are generated with vegetation; 0 leaves every zone to the
    /// game.
    /// </param>
    /// <param name="uid">The world's unique id, or null for a random one.</param>
    /// <param name="flat">
    /// The most even zone of dry Meadows left without vegetation, its ground untouched (no edit, so the tools keep
    /// their whole ±8 m on it): a clean square to show them on (the documentation's pictures). Needs a radius of 2 or
    /// more.
    /// </param>
    /// <exception cref="ArgumentException">A bad name, seed, radius or flat request.</exception>
    /// <exception cref="InvalidOperationException">
    /// The folder already holds a world, or no zone is fit to be the flat one.
    /// </exception>
    public static Created Create(string folder, string name, string seedName, int radius = 0, long? uid = null, bool flat = false)
    {
        name = name.Trim();
        seedName = seedName.Trim();
        if (!IsWorldName(name))
        {
            throw new ArgumentException("The world needs a name that can be a file name on every system.", nameof(name));
        }
        if (seedName.Length is 0 or > 10)
        {
            throw new ArgumentException("The seed is 1 to 10 characters, as in the game.", nameof(seedName));
        }
        if (radius is < 0 or > MaxRadius)
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
        WriteIndex(main + ".chunks", []);
        var db = ZoneDb.Empty(SaveFileVersion, LocationVersion, Tail());
        db.Save(main + ".db2");

        int objects = 0, zones = 0, placed = 0;
        (int X, int Z, float Span)? flatZone = null;
        if (radius > 0)
        {
            // The game's vegetation on the seed's own ground, zone by zone, in base chunk files, and the seed's
            // locations in those zones as the game would spawn them. A zone whose location the editor cannot make is
            // left whole to the game: nothing grows in it, and it is not marked generated.
            var empty = WorldSave.Load(folder);
            var terrain = new ValheimGen.TerrainService(empty);
            var sites = Sites(terrain, seed, radius);
            var left = sites.Where(s => s.Objects == null).Select(s => s.Zone).ToHashSet();
            var spots = Regrow.Zones(terrain, new EditStore(empty), seed, -radius, -radius, radius, radius);
            (int X, int Z)? bare = null;
            if (flat)
            {
                flatZone = FlattestZone(terrain, radius, sites.Select(s => s.Zone).ToHashSet()) ?? throw new InvalidOperationException("No zone of dry, even Meadows near the middle: try another seed or a larger radius.");
                bare = (flatZone.Value.X, flatZone.Value.Z);
            }
            // Nothing grows on the bare zone, in the zones left to the game, or on the ground a location clears.
            bool Grows(Regrow.Spot s)
            {
                var zone = ChunkMath.SectorOf(s.X, s.Z);
                return zone != bare && !left.Contains(zone)
                    && !sites.Any(l => l.Clear > 0f && (s.X - l.Position.X) * (s.X - l.Position.X) + (s.Z - l.Position.Z) * (s.Z - l.Position.Z) < l.Clear * l.Clear);
            }
            var byChunk = new SortedDictionary<ushort, List<byte[]>>();
            // An object into its zone's base chunk file (never the portals' chunk).
            void Add(float x, float z, byte[] zdo)
            {
                var (sx, sz) = ChunkMath.SectorOf(x, z);
                ushort chunk = ChunkMath.BaseChunk(sx, sz);
                if (chunk == ChunkMath.PortalChunk)
                {
                    return;
                }
                (byChunk.TryGetValue(chunk, out var list) ? list : byChunk[chunk] = []).Add(zdo);
                objects++;
            }
            foreach (var s in spots.Where(Grows))
            {
                int prefab = StableHash.Of(s.Name);
                if (Terrain.PrefabCatalog.Get(prefab) is { } info)
                {
                    Add(s.X, s.Z, ZdoBuilder.Blank(prefab, info.Flags, new Vector3(s.X, s.Y, s.Z), new Vector3(s.Rx, s.Ry, s.Rz), s.Scale));
                }
            }
            foreach (var site in sites)
            {
                if (site.Objects is { } made)
                {
                    foreach (var (at, zdo) in made)
                    {
                        Add(at.X, at.Z, zdo);
                    }
                    db.Locations.Add((site.Hash, site.Position, true));
                    placed++;
                }
            }
            // The game's own objects of a generated zone: a zone control in each (its creatures spawn from it), and one
            // terrain compiler object, with no edit, in the middle zone: the writer copies it for every zone whose
            // ground is edited later (the game makes the first one on the first edit).
            ushort ctrlFlags = Terrain.PrefabCatalog.Get(ZoneCtrl)?.Flags ?? 0x0100;
            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (left.Contains((x, z)))
                    {
                        continue;
                    }
                    Add(x * 64f, z * 64f, ZdoBuilder.Blank(ZoneCtrl, ctrlFlags, new Vector3(x * 64f, 0f, z * 64f), Vector3.Zero, 0f));
                    db.Generated.Add(((short)x, (short)z));
                    zones++;
                }
            }
            ushort middle = ChunkMath.BaseChunk(0, 0);
            (byChunk.TryGetValue(middle, out var mid) ? mid : byChunk[middle] = []).Add(EmptyTerrainObject());
            objects++;
            var index = new List<(ushort Chunk, int Count)>();
            foreach (var (chunk, list) in byChunk)
            {
                WriteChunk(Path.Combine(folder, ChunkFile.NameFor(chunk, 0, 1)), list);
                index.Add((chunk, list.Count));
            }
            WriteIndex(main + ".chunks", index);
            db.Save(main + ".db2");
        }
        // Written last, like the game: only then is the save complete.
        File.WriteAllBytes(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
        return new Created(folder, seed, zones, objects, flatZone is var (cx, cz, _) ? (cx, cz) : null, flatZone?.Span ?? 0, placed);
    }

    /// <summary>Prefab hash of the game's zone control object (one in every generated zone).</summary>
    private static readonly int ZoneCtrl = StableHash.Of("_ZoneCtrl");

    /// <summary>Int data key of a location proxy's seed.</summary>
    private static readonly int SeedKey = StableHash.Of("seed");

    /// <summary>A location of the seed in a zone to generate.</summary>
    /// <param name="Zone">Its zone.</param>
    /// <param name="Name">Its kind's prefab name.</param>
    /// <param name="Hash">The name's stable hash: its proxy's location, the zone file's entry.</param>
    /// <param name="Position">Where it stands, world metres (the water's level when its kind snaps to the water).</param>
    /// <param name="Clear">How far around it the vegetation is cleared (0: not cleared).</param>
    /// <param name="Objects">
    /// What is written for it (its networked objects, then its proxy), each with where it is; null when its zone is left
    /// to the game.
    /// </param>
    internal sealed record Site((int X, int Z) Zone, string Name, int Hash, Vector3 Position, float Clear, List<(Vector3 At, byte[] Zdo)>? Objects);

    /// <summary>
    /// The seed's locations within <paramref name="radius"/> zones of the middle, as the game would spawn them when it
    /// generates their zones (ZoneSystem.PlaceLocations and SpawnLocation): laid out by SeedLocations (none when the
    /// game's rules are not read), stood on the generator's ground or the water, turned along the slope or at random
    /// from the zone's seed as their kind asks, with their networked objects (those their random parts keep, rolled
    /// from that seed) and a location proxy. A location with an interior (a dungeon's entrance: the game lays out its
    /// rooms itself) or one the game's files do not describe has no objects: its zone is left to the game.
    /// </summary>
    /// <param name="terrain">The new world's terrain (its generator).</param>
    /// <param name="seed">The world's seed number.</param>
    /// <param name="radius">How far from the middle the zones are generated.</param>
    internal static List<Site> Sites(ValheimGen.TerrainService terrain, int seed, int radius)
    {
        var sites = new List<Site>();
        if (!ValheimGen.SeedLocations.Available)
        {
            return sites;
        }
        ushort proxyFlags = Terrain.PrefabCatalog.Get(WorldSave.LocationProxyPrefab)?.Flags ?? 0x0B00;
        Rnd.State was = Rnd.state;
        try
        {
            foreach (var p in ValheimGen.SeedLocations.Place(terrain.Generator, ValheimGen.SeedLocations.Which.All))
            {
                var zone = ChunkMath.SectorOf(p.X, p.Z);
                if (Math.Abs(zone.X) > radius || Math.Abs(zone.Z) > radius)
                {
                    continue;
                }
                int hash = StableHash.Of(p.Rule.prefab);
                var position = new Vector3(p.X, p.Rule.snapToWater != 0 ? ValheimGen.TerrainService.WaterLevel : p.Y, p.Z);
                if (LocationPrefabs.Get(p.Rule.prefab) is not { Interior: false } prefab)
                {
                    sites.Add(new Site(zone, p.Rule.prefab, hash, position, 0f, null));
                    continue;
                }
                int zoneSeed = LocationPrefabs.ZoneSeed(seed, zone.X, zone.Z);
                float yaw = Yaw(p.Rule, terrain, position, zoneSeed);
                var objects = new List<(Vector3 At, byte[] Zdo)>();
                foreach (var m in LocationPrefabs.Made(prefab, position, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI / 180f), zoneSeed))
                {
                    int made = StableHash.Of(m.Prefab);
                    if (Terrain.PrefabCatalog.Get(made) is { } info)
                    {
                        objects.Add((m.Position, ZdoBuilder.Blank(made, info.Flags, m.Position, BlueprintFormats.ToEuler(m.Rotation), 0f)));
                    }
                }
                objects.Add((position, ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, proxyFlags, position, new Vector3(0f, yaw, 0f), 0f, [(WorldSave.LocationKey, hash), (SeedKey, zoneSeed)])));
                sites.Add(new Site(zone, p.Rule.prefab, hash, position, prefab.ClearArea ? p.Rule.exteriorRadius : 0f, objects));
            }
        }
        finally
        {
            Rnd.state = was;
        }
        return sites;
    }

    /// <summary>
    /// The turn (degrees about the vertical) the game gives a location it spawns: along the slope under it when its
    /// kind asks (facing across the ground's normal, Quaternion.LookRotation of the normal crossed with up), else a
    /// random multiple of 22.5° from the zone's seed when its kind asks (the zone's first draw), else none. Rolled on
    /// the shared Unity random, which the caller puts back.
    /// </summary>
    private static float Yaw(ValheimGen.SeedLocations.Rule rule, ValheimGen.TerrainService terrain, Vector3 at, int zoneSeed)
    {
        if (rule.slopeRotation != 0)
        {
            Vector3 n = Normal(terrain, at.X, at.Z);
            return MathF.Atan2(-n.Z, n.X) * 180f / MathF.PI;
        }
        if (rule.randomRotation != 0)
        {
            Rnd.InitState(zoneSeed);
            return Rnd.Range(0, 16) * 22.5f;
        }
        return 0f;
    }

    /// <summary>The ground's normal at a world point, from the generator's heights a metre apart around it.</summary>
    private static Vector3 Normal(ValheimGen.TerrainService terrain, float x, float z)
    {
        float H(float wx, float wz)
        {
            var (zx, zz) = ChunkMath.SectorOf(wx, wz);
            float[] h = terrain.BaseZone(zx, zz);
            int ix = Math.Clamp((int)MathF.Round(wx - (zx * 64f - 32f)), 0, 64), iz = Math.Clamp((int)MathF.Round(wz - (zz * 64f - 32f)), 0, 64);
            return h[iz * 65 + ix];
        }
        return Vector3.Normalize(new Vector3(H(x - 1f, z) - H(x + 1f, z), 2f, H(x, z - 1f) - H(x, z + 1f)));
    }

    /// <summary>Prefab hash of the game's per-zone terrain compiler object.</summary>
    private static readonly int TerrainCompiler = StableHash.Of("_TerrainCompiler");

    /// <summary>
    /// The game's terrain compiler object of zone 0, 0 with nothing edited: its position in the short form, the
    /// prefab's object flags, and the TCData the writer encodes for an untouched zone.
    /// </summary>
    internal static byte[] EmptyTerrainObject()
    {
        ushort flags = Terrain.PrefabCatalog.Get(TerrainCompiler)?.Flags ?? 0x0D00;
        var z = ZdoData.Parse(ZdoBuilder.Blank(TerrainCompiler, flags, Vector3.Zero, Vector3.Zero, 0f));
        z.SmallPosition = true;
        z.SetBytes(StableHash.Of("TCData"), WorldWriter.EncodeTerrain(new ZoneEdit(0, 0)));
        return z.Serialize();
    }

    /// <summary>
    /// World.SaveWorldMetaData: a length, then the package (version, name, seed name, seed, unique id, generator
    /// version, "has a database", no starting global keys, and the trailing count the game writes after them).
    /// </summary>
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

    /// <summary>
    /// The .chunks index: version, total object count, chunk count, then per chunk its id, size 0, file version 1 and
    /// object count.
    /// </summary>
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

    /// <summary>One chunk file: version, object count, then the objects' save-format bytes back to back.</summary>
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

    /// <summary>
    /// RandEventSystem's save with no event running (timer, event name, time, position), then the list.
    /// </summary>
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
