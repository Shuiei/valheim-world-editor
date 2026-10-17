using TerrainEditor.Save;

/// <summary>The selftest-save check: edits, saves and reloads a COPY of a world, then checks the files with an independent reload.</summary>
internal static class SaveSelfTestCheck
{
    /// <summary>Runs the writer self-test on a world copy under /tmp and prints what the reload shows; returns the exit code.</summary>
    /// <param name="worldDir">The world folder, which must be a copy under /tmp.</param>
    /// <param name="world">The world loaded from that folder.</param>
    internal static int Run(string worldDir, WorldSave world)
    {
        // Never run the destructive self-test on a real world folder.
        if (!Path.GetFullPath(worldDir).StartsWith("/tmp/"))
        {
            Console.Error.WriteLine("selftest-save needs a world COPY under /tmp; refusing to touch " + worldDir);
            return 1;
        }
        // Writer self-test on a COPY of a world: edit an existing zone, create a new terrain object,
        // try an ungenerated zone, save, then check the files with an independent reload.
        var store = new TerrainEditor.Editing.EditStore(world);
        var exist = world.TerrainZones.OrderByDescending(z => z.ModifiedHeight.Count(m => m)).First();
        EditExistingZone(store, exist);
        var (zx, zz) = FindFreeZoneWithChunk(world);
        var e2 = new TerrainEditor.Editing.ZoneEdit(zx, zz);
        for (int i = 2000; i < 2100; i++) { e2.Modified[i] = true; e2.Level[i] = -2f; e2.Smooth[i] = 0.5f; }
        store.Put(e2);
        var e3 = new TerrainEditor.Editing.ZoneEdit(150, 150);
        e3.Modified[5] = true; e3.Level[5] = 3f;
        store.Put(e3);
        var hashesBefore = ChunkHashes(world.Directory);
        var result = WorldWriter.Save(world, [.. store.All().Where(z => z.Changed)]);
        Console.WriteLine($"Result: saved={result.Saved} '{result.Message}' written={result.ZonesWritten} created={result.ZonesCreated} skipped=[{string.Join("; ", result.Skipped)}]");
        Console.WriteLine($"Backup: {result.BackupDirectory}");
        var after = WorldSave.Load(world.Directory);
        Console.WriteLine($"After: save #{after.SaveNumber}, {after.ObjectCount:N0} objects (was {world.ObjectCount:N0}), {after.TerrainZones.Count} terrain zones (was {world.TerrainZones.Count})");
        var z1 = after.TerrainZones.First(t => t.ZoneX == exist.ZoneX && t.ZoneZ == exist.ZoneZ);
        var z2 = after.TerrainZones.FirstOrDefault(t => t.ZoneX == zx && t.ZoneZ == zz);
        Console.WriteLine($"Existing zone ({exist.ZoneX},{exist.ZoneZ}): point 0 level {z1.LevelDelta[0]}, point 299 {z1.LevelDelta[299]}, paint 2000 {z1.Paint[2000]}; untouched point 4000 same as before: {z1.LevelDelta[4000] == exist.LevelDelta[4000] && z1.ModifiedHeight[4000] == exist.ModifiedHeight[4000]}");
        Console.WriteLine($"New zone ({zx},{zz}): {(z2 == null ? "MISSING" : $"found in {z2.Source!.File.FileName}, point 2050 level {z2.LevelDelta[2050]} smooth {z2.SmoothDelta[2050]}, {z2.ModifiedHeight.Count(m => m)} edited points")}");
        Console.WriteLine($"Ungenerated zone (150,150) written: {after.TerrainZones.Any(t => t.ZoneX == 150 && t.ZoneZ == 150)}");
        var hashesAfter = ChunkHashes(world.Directory);
        int same = hashesBefore.Count(kv => hashesAfter.TryGetValue(kv.Key, out string? h) && h == kv.Value);
        Console.WriteLine($"Chunk files byte-identical to before: {same} of {hashesBefore.Count}; new files: {string.Join(", ", hashesAfter.Keys.Except(hashesBefore.Keys))}; removed: {string.Join(", ", hashesBefore.Keys.Except(hashesAfter.Keys))}");
        Console.WriteLine($"Main files now: {string.Join(", ", Directory.GetFiles(world.Directory, "_main.*").Select(Path.GetFileName).OrderBy(n => n))}");
        return 0;
    }

    /// <summary>Edits an existing zone's first 300 heights and paint points, so that the save has to change it.</summary>
    /// <param name="store">The edit store that the zone's edit is put back into.</param>
    /// <param name="exist">The zone that already has terrain edits.</param>
    private static void EditExistingZone(TerrainEditor.Editing.EditStore store, TerrainZone exist)
    {
        var e1 = store.Get(exist.ZoneX, exist.ZoneZ)!;
        for (int i = 0; i < 300; i++) { e1.Modified[i] = true; e1.Level[i] = 1.25f; }
        e1.PaintModified[2000] = true; e1.Paint[8000] = 0f; e1.Paint[8001] = 0f; e1.Paint[8002] = 1f; e1.Paint[8003] = 1f;
        store.Put(e1);
    }

    /// <summary>The first zone in the 13 by 13 zones around the middle that has a chunk file but no terrain edits yet.</summary>
    /// <param name="world">The world whose chunks and terrain zones are searched.</param>
    private static (int X, int Z) FindFreeZoneWithChunk(WorldSave world)
    {
        return (from z in Enumerable.Range(-6, 13)
                from x in Enumerable.Range(-6, 13)
                let k = (x, z)
                where !world.TerrainZones.Any(t => t.ZoneX == k.x && t.ZoneZ == k.z) && ChunkMath.Find(world.Chunks, k.x, k.z) != null
                select k).First();
    }

    /// <summary>The SHA-256 of each chunk file in the folder, by file name.</summary>
    /// <param name="dir">The world folder.</param>
    private static Dictionary<string, string> ChunkHashes(string dir)
    {
        return Directory.GetFiles(dir, "*.chunk").ToDictionary(f => Path.GetFileName(f), f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
    }
}
