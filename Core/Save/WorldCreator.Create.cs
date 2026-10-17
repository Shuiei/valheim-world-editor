using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
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
        // What is written is taken away again should anything fail (no zone fit to be the flat one, a full disk): a
        // part-written world would stop the next try in this folder.
        bool madeFolder = !Directory.Exists(folder);
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        try
        {
            int seed = SeedOf(seedName);
            string main = Path.Combine(folder, "_main.1");
            written.AddRange([main + ".fwl2", main + ".chunks", main + ".db2"]);
            WriteMetadata(main + ".fwl2", name, seedName, seed, uid ?? Random.Shared.NextInt64(1, long.MaxValue));
            WorldWriter.WriteIndex(main + ".chunks", SaveFileVersion, []);
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
                    string file = Path.Combine(folder, ChunkFile.NameFor(chunk, 0, 1));
                    written.Add(file);
                    WriteChunk(file, list);
                    index.Add((chunk, list.Count));
                }
                WorldWriter.WriteIndex(main + ".chunks", SaveFileVersion, [.. index.Select(c => (c.Chunk, (byte)0, 1u, c.Count))]);
                db.Save(main + ".db2");
            }
            // Written last, like the game: only then is the save complete. Every file and its name are on disk before it
            // (a crash must not leave a marker vouching for files that are not), the marker's name after it.
            written.Add(main + ".ok");
            DirectorySync.Flush(folder);
            WorldWriter.WriteAllBytesDurable(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
            DirectorySync.Flush(folder);
            return new Created(folder, seed, zones, objects, flatZone is var (cx, cz, _) ? (cx, cz) : null, flatZone?.Span ?? 0, placed);
        }
        catch
        {
            Unwrite(folder, written, madeFolder);
            throw;
        }
    }
}
