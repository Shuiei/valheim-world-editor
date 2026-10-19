using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>What the vegetation step made: the objects written, the zones generated, the locations placed and the flat zone.</summary>
    /// <param name="Objects">Objects written into the chunk files.</param>
    /// <param name="Zones">Zones marked generated.</param>
    /// <param name="Placed">Locations placed from the seed.</param>
    /// <param name="FlatZone">The bare zone, when one was asked for.</param>
    private sealed record Growth(int Objects, int Zones, int Placed, (int X, int Z, float Span)? FlatZone);

    /// <summary>
    /// The game's vegetation on the seed's own ground, zone by zone, in base chunk files, and the seed's locations in
    /// those zones as the game would spawn them. A zone whose location the editor cannot make is left whole to the game:
    /// nothing grows in it, and it is not marked generated.
    /// </summary>
    /// <param name="folder">The world's folder (its files are read back for the ground).</param>
    /// <param name="main">The world's main file name without its extension.</param>
    /// <param name="db">The world's database, which gets the locations and generated zones.</param>
    /// <param name="written">The files written so far (the chunk files are added here).</param>
    /// <param name="seed">The seed.</param>
    /// <param name="radius">The generated radius in zones.</param>
    /// <param name="flat">Whether one zone is left bare.</param>
    /// <returns>What was made.</returns>
    private static Growth GrowVegetation(string folder, string main, ZoneDb db, List<string> written, int seed, int radius, bool flat)
    {
        var empty = WorldSave.Load(folder);
        var terrain = new ValheimGen.TerrainService(empty);
        var sites = Sites(terrain, seed, radius);
        var left = sites.Where(s => s.Objects == null).Select(s => s.Zone).ToHashSet();
        var spots = Regrow.Zones(terrain, new EditStore(empty), seed, -radius, -radius, radius, radius);
        (int X, int Z)? bare = null;
        (int X, int Z, float Span)? flatZone = null;
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
        var batch = new ChunkBatch();
        PlaceSpots(spots.Where(Grows), batch);
        int placed = 0;
        foreach (var site in sites)
        {
            if (site.Objects is { } made)
            {
                foreach (var (at, zdo) in made)
                {
                    batch.Add(at.X, at.Z, zdo);
                }
                db.Locations.Add((site.Hash, site.Position, true));
                placed++;
            }
        }
        int zones = PlaceZoneControls(db, batch, left, radius, terrain.Generator.Rules.ZoneSpawners);
        // The middle zone's terrain compiler object (no edit, written with the first chunk of the middle zone).
        batch.Put(ChunkMath.BaseChunk(0, 0), EmptyTerrainObject());
        batch.Objects++;
        WriteChunks(folder, main, db, batch, written);
        return new Growth(batch.Objects, zones, placed, flatZone);
    }

    /// <summary>Places the vegetation of the spots that may grow, each as the prefab's blank object.</summary>
    /// <param name="spots">The spots that may grow.</param>
    /// <param name="batch">The chunk batch receiving the objects.</param>
    private static void PlaceSpots(IEnumerable<Regrow.Spot> spots, ChunkBatch batch)
    {
        foreach (var s in spots)
        {
            int prefab = StableHash.Of(s.Name);
            if (Terrain.PrefabCatalog.Get(prefab) is { } info)
            {
                batch.Add(s.X, s.Z, ZdoBuilder.Blank(prefab, info.Flags, new Vector3(s.X, s.Y, s.Z), new Vector3(s.Rx, s.Ry, s.Rz), s.Scale));
            }
        }
    }

    /// <summary>
    /// The game's own objects of a generated zone: a zone control in each (its creatures spawn from it; none when the
    /// world's rules turn the spawners off), and marks the zone generated. Zones left to the game get neither.
    /// </summary>
    /// <param name="db">The world's database (the generated zones).</param>
    /// <param name="batch">The chunk batch receiving the zone controls.</param>
    /// <param name="left">The zones left to the game.</param>
    /// <param name="radius">The generated radius in zones.</param>
    /// <param name="spawners">Whether each zone gets its zone control (Expand World Data's "Zone spawners").</param>
    /// <returns>How many zones were generated.</returns>
    private static int PlaceZoneControls(ZoneDb db, ChunkBatch batch, HashSet<(int X, int Z)> left, int radius, bool spawners = true)
    {
        int zones = 0;
        ushort ctrlFlags = Terrain.PrefabCatalog.Get(ZoneCtrl)?.Flags ?? 0x0100;
        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (left.Contains((x, z)))
                {
                    continue;
                }
                if (spawners)
                {
                    batch.Add(x * 64f, z * 64f, ZdoBuilder.Blank(ZoneCtrl, ctrlFlags, new Vector3(x * 64f, 0f, z * 64f), Vector3.Zero, 0f));
                }
                db.Generated.Add(((short)x, (short)z));
                zones++;
            }
        }
        return zones;
    }

    /// <summary>Writes each chunk file with its objects, then the chunk index and the database.</summary>
    /// <param name="folder">The world's folder.</param>
    /// <param name="main">The world's main file name without its extension.</param>
    /// <param name="db">The world's database.</param>
    /// <param name="batch">The objects by chunk.</param>
    /// <param name="written">The files written so far (each chunk file is added here).</param>
    private static void WriteChunks(string folder, string main, ZoneDb db, ChunkBatch batch, List<string> written)
    {
        var index = new List<(ushort Chunk, int Count)>();
        foreach (var (chunk, list) in batch.Chunks)
        {
            string file = Path.Combine(folder, ChunkFile.NameFor(chunk, 0, 1));
            written.Add(file);
            WriteChunk(file, list);
            index.Add((chunk, list.Count));
        }
        WorldWriter.WriteIndex(main + ".chunks", SaveFileVersion, [.. index.Select(c => (c.Chunk, (byte)0, 1u, c.Count))]);
        db.Save(main + ".db2");
    }
}
