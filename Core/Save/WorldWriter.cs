using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

/// <summary>
/// Writes terrain edits back into a chunked world save, following ZNet.SaveWorldThread:
/// <list type="number">
/// <item><description>
/// changed chunk files are written under a new version number; untouched objects are copied byte for byte, terrain
/// objects get new TCData, new terrain objects are appended
/// </description></item>
/// <item><description>
/// a new save number (the highest there + 1): _main.&lt;n&gt;.chunks (updated index), .db2 and .fwl2
/// </description></item>
/// <item><description>
/// _main.&lt;n&gt;.ok is written last: the game only trusts a save that has it; each file is flushed to disk, and the
/// folder's names before and after the marker (DirectorySync, Linux and macOS)
/// </description></item>
/// <item><description>
/// the save is read back and compared with the edits; on any mismatch (or failure) the new files are removed and the
/// world is as it was: nothing of the old save is touched before this
/// </description></item>
/// <item><description>
/// the save it was made from and the chunk files it replaced are removed (KeepBase: kept, for the open world's next
/// saves, which are made from it too), and the files of Drop (an earlier save of the same session) with them
/// </description></item>
/// </list>
/// No backup folder: nothing is removed before the new save has read back right.
/// </summary>
public static class WorldWriter
{
    /// <summary>The only save format version the writer writes (and accepts to change).</summary>
    private const int SaveFileVersion = 41;

    /// <summary>Prefab hash of a player's tombstone, which zone resets never clear.</summary>
    public static readonly int TombstonePrefab = StableHash.Of("Player_tombstone");

    /// <summary>The outcome of a <c>Save</c>.</summary>
    /// <param name="Saved">Whether a new save was written.</param>
    /// <param name="Message">What was saved, or why nothing was, for the user.</param>
    /// <param name="BackupDirectory">Always null: the writer makes no backup folder.</param>
    /// <param name="ZonesWritten">Zones whose existing terrain objects got new ground data.</param>
    /// <param name="ZonesCreated">Zones that got a new terrain object.</param>
    /// <param name="Skipped">Edits that could not be saved, each with the reason.</param>
    /// <param name="ObjectsDeleted">Objects left out, those cleared by zone resets included.</param>
    /// <param name="ObjectsAdded">New objects written.</param>
    /// <param name="ZonesReset">Zones handed back to the world generator.</param>
    public sealed record Result(bool Saved, string Message, string? BackupDirectory, int ZonesWritten, int ZonesCreated, List<string> Skipped, int ObjectsDeleted = 0, int ObjectsAdded = 0, int ZonesReset = 0)
    {
        /// <summary>The files the save wrote (its _main files and new chunk files).</summary>
        public IReadOnlyList<string> Files { get; init; } = [];
        /// <summary>The save number written (-1: none).</summary>
        public int Number { get; init; } = -1;
        /// <summary>The new save as it was read back to check it (null when nothing was saved): no need to read it again.</summary>
        public WorldSave? Read { get; init; }
    }

    /// <summary>How a save treats the files around it.</summary>
    /// <param name="KeepBase">The save it is made from stays (an open world saves again from it).</param>
    /// <param name="Drop">
    /// Files of an earlier save of the same session, removed once this one has read back right.
    /// </param>
    /// <param name="Latest">
    /// The newest save number the caller knows of (its own last save, or the one it opened); a newer one in the folder
    /// was made by the game since, and saving would throw it away, so nothing is saved.
    /// </param>
    public sealed record Options(bool KeepBase = false, IReadOnlyCollection<string>? Drop = null, int? Latest = null);

    /// <summary>Writes a new save of the world with these changes, or nothing at all (see the type's steps).</summary>
    /// <param name="world">The world as loaded; its save is the base of the new one.</param>
    /// <param name="changed">Zones whose ground was edited.</param>
    /// <param name="deleted">Ids (WorldSave.ObjectRefs) of objects to leave out of the new save.</param>
    /// <param name="added">
    /// New objects (WorldSave.NewObjectBytes): their own data when they carry it (Raw), else a copy of an
    /// object of the same prefab in the world, else a blank object from the game's data.
    /// </param>
    /// <param name="resets">
    /// Zones handed back to the world generator (their objects are removed and the zone is marked as not generated).
    /// </param>
    /// <param name="options">File handling; null for the defaults.</param>
    public static Result Save(WorldSave world, IReadOnlyList<ZoneEdit> changed, IReadOnlyCollection<int>? deleted = null, IReadOnlyList<NewObject>? added = null, IReadOnlyList<ZoneReset>? resets = null, Options? options = null)
    {
        options ??= new Options();
        List<string> skipped = [];
        deleted ??= [];
        added ??= [];
        resets ??= [];
        if (resets.Count > 0 && world.Zones == null)
        {
            return new Result(false, "Zones can't be reset: the world's .db2 file could not be read.", null, 0, 0, skipped);
        }
        // Terrain edits of zones whose ground is reset are dropped.
        HashSet<(int, int)> groundReset = [.. resets.Where(r => r.Ground).Select(r => (r.X, r.Z))];
        changed = [.. changed.Where(e => !groundReset.Contains((e.ZoneX, e.ZoneZ)))];
        if (changed.Count == 0 && deleted.Count == 0 && added.Count == 0 && resets.Count == 0)
        {
            return new Result(false, "Nothing to save.", null, 0, 0, skipped);
        }
        if (options.Latest is int known && Newer(world.Directory, known) is string newer)
        {
            return new Result(false, newer, null, 0, 0, skipped);
        }
        if (world.Chunks.FirstOrDefault(c => c.WorldVersion != SaveFileVersion) is { } other)
        {
            return new Result(false, $"This world uses save format {other.WorldVersion}; the writer only supports {SaveFileVersion}.", null, 0, 0, skipped);
        }
        // New ground copies a terrain object whose position is in the short format (the layout the copy patches).
        bool anyTerrain = world.TerrainZones.Any(z => z.Source != null);
        TerrainZone? template = world.TerrainZones.FirstOrDefault(z => z.Source != null && (z.Source.Flags & ShortPosition) != 0);
        Dictionary<ChunkFile, HashSet<long>> removals = [];
        void Remove(ObjectRef o) => (removals.TryGetValue(o.File, out var set) ? set : removals[o.File] = []).Add(o.Start);
        foreach (int id in deleted)
        {
            if (id >= 0 && id < world.ObjectRefs.Count)
            {
                Remove(world.ObjectRefs[id]);
            }
        }
        // Objects the deletions take out: ids past the list and repeated ids take out none.
        int deletedCount = removals.Values.Sum(s => s.Count);
        Dictionary<(int, int), ZoneReset> resetByZone = resets.ToDictionary(r => (r.X, r.Z));
        foreach (ObjectRef o in world.ObjectRefs)
        {
            // Players' tombstones (with what they carried) are never cleared.
            if (resetByZone.TryGetValue(o.Zone, out ZoneReset? r) && !(r.KeepBuildings && o.IsPiece) && !(o.IsTerrain && !r.Ground) && o.Prefab != TombstonePrefab)
            {
                Remove(o);
            }
        }
        int removed = removals.Values.Sum(s => s.Count);
        // A zone can hold two terrain objects with data (the game makes them now and then): each gets
        // the zone's ground, so whichever one the game uses has it. Those deleted are not patched (removal wins): a
        // zone whose terrain objects all go gets a new one.
        Dictionary<(int, int), List<TerrainZone>> existing = world.TerrainZones
            .Where(z => z.Source != null && !(removals.TryGetValue(z.Source.File, out var gone) && gone.Contains(z.Source.Start)))
            .GroupBy(z => (z.ZoneX, z.ZoneZ)).ToDictionary(g => g.Key, g => g.ToList());

        // Plan: which chunk files change, and how.
        Dictionary<ChunkFile, Dictionary<long, (ZdoLocation Loc, byte[] Data)>> patches = [];     // object start -> new TCData
        HashSet<(int, int)> notSaved = [];
        Dictionary<ChunkFile, List<byte[]>> additions = [];
        int written = 0, created = 0, addedCount = 0;
        // The template terrain object's own bytes, read from its chunk file at the first new ground.
        byte[]? templateBytes = null;
        // Each base chunk's file looked up once for the new ground and objects.
        var findChunk = ChunkMath.Finder(world.Chunks);
        // Reading the template and the models can fail (a chunk file gone, data that does not parse): nothing is
        // written yet, and the caller gets a result, not an exception.
        try
        {
            foreach (ZoneEdit edit in changed)
            {
                byte[] data = EncodeTerrain(edit);
                if (existing.TryGetValue((edit.ZoneX, edit.ZoneZ), out List<TerrainZone>? zones))
                {
                    foreach (TerrainZone zone in zones)
                    {
                        (patches.TryGetValue(zone.Source!.File, out var p) ? p : patches[zone.Source.File] = [])[zone.Source.Start] = (zone.Source, data);
                    }
                    written++;
                    continue;
                }
                if (edit.HeightCount + edit.PaintCount == 0)
                {
                    notSaved.Add((edit.ZoneX, edit.ZoneZ));
                    continue;
                }
                ChunkFile? target = findChunk(edit.ZoneX, edit.ZoneZ);
                if (target == null)
                {
                    skipped.Add(ChunkMath.InWorld(edit.ZoneX, edit.ZoneZ) ? $"zone {edit.ZoneX}, {edit.ZoneZ}: not generated yet (no world data there; visit it in game first)" : $"zone {edit.ZoneX}, {edit.ZoneZ}: outside the world");
                    notSaved.Add((edit.ZoneX, edit.ZoneZ));
                    continue;
                }
                if (template == null)
                {
                    skipped.Add(anyTerrain
                        ? $"zone {edit.ZoneX}, {edit.ZoneZ}: the world's terrain objects are in an unexpected layout (position not in the short format), so none can be copied"
                        : $"zone {edit.ZoneX}, {edit.ZoneZ}: the world has no terrain object to copy yet (edit the ground once in game first)");
                    notSaved.Add((edit.ZoneX, edit.ZoneZ));
                    continue;
                }
                templateBytes ??= TemplateBytes(world, template.Source!);
                (additions.TryGetValue(target, out var a) ? a : additions[target] = []).Add(NewTerrainObject(template.Source!, templateBytes, edit, data));
                created++;
            }
            // New objects: a copy of the first object of the same prefab, in the chunk file of their zone.
            Func<ObjectRef, byte[]> source = world.SourceReader();
            foreach (NewObject n in added)
            {
                int zx = (int)MathF.Floor((n.Position.X + 32f) / 64f), zz = (int)MathF.Floor((n.Position.Z + 32f) / 64f);
                ChunkFile? target = findChunk(zx, zz);
                if (target == null)
                {
                    skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: zone {zx}, {zz} {(ChunkMath.InWorld(zx, zz) ? "is not generated yet" : "is outside the world")}");
                    continue;
                }
                byte[]? bytes = world.NewObjectBytes(n, source);
                if (bytes == null)
                {
                    skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: unknown kind of object");
                    continue;
                }
                (additions.TryGetValue(target, out var list) ? list : additions[target] = []).Add(bytes);
                addedCount++;
            }
        }
        catch (Exception ex)
        {
            return new Result(false, "Saving failed (" + ex.Message + "). Nothing changed.", null, 0, 0, skipped);
        }
        if (written + created + removed + addedCount + resets.Count == 0)
        {
            return new Result(false, "Nothing could be saved.", null, 0, 0, skipped);
        }

        string dir = world.Directory;
        int newNumber = Math.Max(world.SaveNumber, LatestNumber(dir)) + 1;
        List<string> newFiles = [];
        WorldSave? read = null;
        Dictionary<ChunkFile, (uint Version, int Count)> updated = [];
        try
        {
            // 2. Changed chunk files under a new version.
            foreach (ChunkFile file in patches.Keys.Union(additions.Keys).Union(removals.Keys))
            {
                patches.TryGetValue(file, out var p);
                additions.TryGetValue(file, out var a);
                removals.TryGetValue(file, out var r);
                uint version = file.Version + 1;
                while (File.Exists(Path.Combine(dir, ChunkFile.NameFor(file.Chunk, file.Size, version))))
                {
                    version++;
                }
                string path = Path.Combine(dir, ChunkFile.NameFor(file.Chunk, file.Size, version));
                newFiles.Add(path);
                int count = WriteChunk(Path.Combine(dir, file.FileName), path, file, p, a, r);
                updated[file] = (version, count);
            }
            // 3. New save number: index, world data, metadata.
            string main = Path.Combine(dir, $"_main.{newNumber}");
            // Each file is listed before it is written: a failure halfway removes it too.
            newFiles.AddRange([main + ".chunks", main + ".db2", main + ".fwl2", main + ".ok"]);
            WriteIndex(main + ".chunks", [.. world.Chunks.Select(c => updated.TryGetValue(c, out var u) ? (c.Chunk, c.Size, u.Version, u.Count) : (c.Chunk, c.Size, c.Version, c.IndexCount))]);
            if (resets.Count > 0)
            {
                var db = ZoneDb.Load(Path.Combine(dir, $"_main.{world.SaveNumber}.db2"));
                foreach (ZoneReset r in resets)
                {
                    db.ResetZone(r.X, r.Z);
                }
                db.Save(main + ".db2");
            }
            else
            {
                CopyDurable(Path.Combine(dir, $"_main.{world.SaveNumber}.db2"), main + ".db2");
            }
            CopyDurable(Path.Combine(dir, $"_main.{world.SaveNumber}.fwl2"), main + ".fwl2");
            // The new files' names on disk too (Linux, macOS), before the marker that vouches for them.
            DirectorySync.Flush(dir);
            // 4. Commit marker, written last like the game does; its name on disk before anything old goes.
            WriteAllBytesDurable(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
            DirectorySync.Flush(dir);
            // 5. Read back and compare with what was meant to be written (nothing old removed yet).
            read = WorldSave.Load(dir);
            string? problem = Check(read, world, [.. changed.Where(e => !notSaved.Contains((e.ZoneX, e.ZoneZ)))], created + addedCount, removed, groundReset, resets);
            if (problem != null)
            {
                RemoveAll(newFiles);
                return new Result(false, "The saved world did not read back correctly (" + problem + "). Nothing changed.", null, 0, 0, skipped);
            }
        }
        catch (Exception ex)
        {
            RemoveAll(newFiles);
            return new Result(false, "Saving failed (" + ex.Message + "). Nothing changed.", null, 0, 0, skipped);
        }
        // 6. The save it was made from (unless kept) and an earlier save of the session go.
        var old = new List<string>();
        if (!options.KeepBase)
        {
            old.AddRange(MainExtensions.Select(ext => Path.Combine(dir, $"_main.{world.SaveNumber}{ext}")));
            old.AddRange(updated.Keys.Select(c => Path.Combine(dir, c.FileName)));
        }
        if (options.Drop != null)
        {
            old.AddRange(options.Drop.Where(f => !newFiles.Contains(f)));
        }
        RemoveAll(old);
        string what = string.Join(", ", new[] { written + created > 0 ? $"{written + created} zone(s)" : null, deletedCount > 0 ? $"{deletedCount} deleted object(s)" : null, addedCount > 0 ? $"{addedCount} new object(s)" : null, resets.Count > 0 ? $"{resets.Count} reset zone(s) ({removed - deletedCount} objects cleared)" : null }.Where(x => x != null));
        return new Result(true, $"Saved {what} to save #{newNumber}.", null, written, created, skipped, removed, addedCount, resets.Count) { Files = newFiles, Number = newNumber, Read = read };
    }

    /// <summary>Mirrors TerrainComp.Save: a GZip-compressed ZPackage.</summary>
    public static byte[] EncodeTerrain(ZoneEdit e)
    {
        using MemoryStream raw = new();
        using (BinaryWriter w = new(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(1);
            w.Write(1);                 // operations; the game only compares it to its previous value
            w.Write(e.ZoneX * 64f);     // last operation point and radius: used to refresh grass
            w.Write(0f);
            w.Write(e.ZoneZ * 64f);
            w.Write(64f);
            w.Write(EditStore.Cells);
            for (int i = 0; i < EditStore.Cells; i++)
            {
                w.Write(e.Modified[i]);
                if (e.Modified[i])
                {
                    w.Write(e.Level[i]);
                    w.Write(e.Smooth[i]);
                }
            }
            w.Write(EditStore.Cells);
            for (int i = 0; i < EditStore.Cells; i++)
            {
                w.Write(e.PaintModified[i]);
                if (e.PaintModified[i])
                {
                    for (int c = 0; c < 4; c++)
                    {
                        w.Write(e.Paint[i * 4 + c]);
                    }
                }
            }
        }
        using MemoryStream compressed = new();
        using (GZipStream gz = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(raw.GetBuffer(), 0, (int)raw.Length);
        }
        return compressed.ToArray();
    }

    /// <summary>The ZDO flag of a position saved as two shorts (the zone), which new terrain objects are copied from.</summary>
    private const ushort ShortPosition = 0x2000;

    /// <summary>
    /// Template objects' bytes read from their chunk files on this thread so far: a test seam, a save reads its
    /// template once however many zones get new ground.
    /// </summary>
    [field: ThreadStatic]
    internal static int TemplateReads { get; private set; }

    /// <summary>The template terrain object's own bytes (Start..End), read from its chunk file alone.</summary>
    /// <param name="world">The world the template is of.</param>
    /// <param name="src">Where the template object is.</param>
    private static byte[] TemplateBytes(WorldSave world, ZdoLocation src)
    {
        using FileStream fs = File.OpenRead(Path.Combine(world.Directory, src.File.FileName));
        byte[] own = new byte[src.End - src.Start];
        fs.Seek(src.Start, SeekOrigin.Begin);
        fs.ReadExactly(own);
        TemplateReads++;
        return own;
    }

    /// <summary>A new terrain object: the template object's bytes with this zone's position and data.</summary>
    /// <param name="src">Where the template object is (its offsets, in its chunk file).</param>
    /// <param name="own">The template object's bytes (<see cref="TemplateBytes"/>).</param>
    /// <param name="edit">The zone's edit.</param>
    /// <param name="data">The zone's encoded terrain data.</param>
    /// <exception cref="InvalidOperationException">The template's position is not in the short format.</exception>
    private static byte[] NewTerrainObject(ZdoLocation src, byte[] own, ZoneEdit edit, byte[] data)
    {
        if ((src.Flags & ShortPosition) == 0)
        {
            throw new InvalidOperationException("Unexpected terrain object layout (position is not in the short format).");
        }
        byte[] head = own[..(int)(src.DataStart - src.Start)];
        // Bytes 2..5 are the position as two shorts (x, z) of the zone centre.
        BitConverter.GetBytes((short)(edit.ZoneX * 64)).CopyTo(head, 2);
        BitConverter.GetBytes((short)(edit.ZoneZ * 64)).CopyTo(head, 4);
        using MemoryStream ms = new();
        ms.Write(head);
        ms.Write(BitConverter.GetBytes(data.Length));
        ms.Write(data);
        ms.Write(own, (int)(src.DataEnd - src.Start), (int)(src.End - src.DataEnd));
        return ms.ToArray();
    }

    /// <summary>
    /// Writes a chunk file anew: its objects copied byte for byte, less the removed ones, with patched TCData where
    /// given, then the additions.
    /// </summary>
    /// <param name="patches">New TCData by object start offset, or null.</param>
    /// <param name="additions">Objects to append, or null.</param>
    /// <param name="removals">Start offsets of objects to leave out, or null.</param>
    /// <param name="sourcePath">The chunk file as it is.</param>
    /// <param name="targetPath">The new chunk file; must not exist yet.</param>
    /// <param name="file">The chunk file as read.</param>
    /// <returns>How many objects the new file holds.</returns>
    private static int WriteChunk(string sourcePath, string targetPath, ChunkFile file, Dictionary<long, (ZdoLocation Loc, byte[] Data)>? patches, List<byte[]>? additions, HashSet<long>? removals)
    {
        byte[] source = File.ReadAllBytes(sourcePath);
        int count = file.Count + (additions?.Count ?? 0) - file.Objects.Count(o => removals?.Contains(o.Start) == true);
        using (FileStream fs = new(targetPath, FileMode.CreateNew, FileAccess.Write))
        using (BinaryWriter w = new(fs))
        {
            w.Write((short)file.WorldVersion);
            w.Write(count);
            foreach (var (start, end) in file.Objects)
            {
                if (removals != null && removals.Contains(start))
                {
                    continue;
                }
                if (patches != null && patches.TryGetValue(start, out var patch))
                {
                    var (loc, data) = patch;
                    w.Write(source, (int)start, (int)(loc.DataStart - start));
                    w.Write(data.Length);
                    w.Write(data);
                    w.Write(source, (int)loc.DataEnd, (int)(end - loc.DataEnd));
                }
                else
                {
                    w.Write(source, (int)start, (int)(end - start));
                }
            }
            foreach (byte[] obj in additions ?? [])
            {
                w.Write(obj);
            }
            w.Flush();
            fs.Flush(flushToDisk: true);
        }
        return count;
    }

    /// <summary>
    /// A .chunks index (version, total object count, chunk count, then per chunk its id, size, file version and object
    /// count), flushed to disk before returning: the save's and a new world's.
    /// </summary>
    internal static void WriteIndex(string path, IReadOnlyList<(ushort Chunk, byte Size, uint Version, int Count)> chunks)
    {
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        using BinaryWriter w = new(fs);
        w.Write((short)SaveFileVersion);
        w.Write(chunks.Sum(c => c.Count));
        w.Write(chunks.Count);
        foreach (var (chunk, size, version, count) in chunks)
        {
            w.Write(chunk);
            w.Write(size);
            w.Write(version);
            w.Write(count);
        }
        w.Flush();
        fs.Flush(flushToDisk: true);
    }

    /// <summary>Writes a file and flushes it to disk before returning.</summary>
    internal static void WriteAllBytesDurable(string path, byte[] bytes)
    {
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        fs.Write(bytes);
        fs.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Copies a file (over one there) and flushes the copy to disk before returning: the commit marker written after
    /// it must not be on disk before the files it vouches for.
    /// </summary>
    private static void CopyDurable(string source, string target)
    {
        using FileStream from = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream to = new(target, FileMode.Create, FileAccess.Write);
        from.CopyTo(to);
        to.Flush(flushToDisk: true);
    }

    /// <summary>The file extensions of one save number's _main files.</summary>
    private static readonly string[] MainExtensions = [".ok", ".chunks", ".db2", ".fwl2"];

    /// <summary>Deletes the files, ignoring any that cannot be deleted.</summary>
    private static void RemoveAll(IEnumerable<string> files)
    {
        foreach (string f in files)
        {
            try
            {
                File.Delete(f);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Why a save made from save number <c>known</c> would throw away a newer one in the folder (the game saved the
    /// world since it was read), or null when there is none. Only a complete save counts: one cut short long ago would
    /// block every save.
    /// </summary>
    public static string? Newer(string dir, int known) => WorldSave.CommittedSave(dir) is int newest && newest > known
        ? $"Valheim saved this world (save #{newest}) after the editor read it: saving now would throw away what was done in the game since. Nothing was saved. Leave the world and open it again to edit the game's save."
        : null;

    /// <summary>The highest save number in a world folder (-1: none).</summary>
    public static int LatestNumber(string dir) => Directory.GetFiles(dir, "_main.*.chunks")
        .Select(f => Path.GetFileName(f).Split('.')).Where(p => p.Length == 3 && int.TryParse(p[1], out _)).Select(p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(-1).Max();

    /// <summary>
    /// The world folder down to its latest save: earlier save numbers' files and the chunk files its index does not
    /// list go (an open world's base, once it is left). A newer save without its commit marker (cut short, or the game
    /// writing it right now) is left alone, and so are the chunk files then; so are files named _main.&lt;something
    /// else&gt;.
    /// </summary>
    public static void Prune(string dir)
    {
        if (LatestNumber(dir) < 0)
        {
            return;
        }
        Prune(WorldSave.Load(dir));
    }

    /// <summary>
    /// <see cref="Prune(string)"/> for a world just read from its folder's latest save: its chunk list is what stays.
    /// </summary>
    public static void Prune(WorldSave now)
    {
        string dir = now.Directory;
        int latest = now.SaveNumber;
        var keep = now.Chunks.Select(c => c.FileName).ToHashSet();
        var old = Directory.GetFiles(dir, "_main.*").Where(f => Path.GetFileName(f).Split('.') is [_, var n, _]
            && int.TryParse(n, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number) && number < latest);
        if (LatestNumber(dir) == latest)
        {
            old = old.Concat(Directory.GetFiles(dir, "*.chunk").Where(f => !keep.Contains(Path.GetFileName(f))));
        }
        RemoveAll([.. old]);
    }

    /// <summary>
    /// Reads the new save back and checks it against what was meant: reset zones no longer generated, the object count,
    /// the saved zones' ground, and every other zone's ground untouched.
    /// </summary>
    /// <returns>What is wrong, or null when the save is right.</returns>
    internal static string? Verify(string dir, WorldSave before, IReadOnlyList<ZoneEdit> saved, int created, int removed, HashSet<(int, int)> groundReset, IReadOnlyList<ZoneReset> resets) =>
        Check(WorldSave.Load(dir), before, saved, created, removed, groundReset, resets);

    /// <summary><see cref="Verify"/> on the new save already read back (<paramref name="after"/>).</summary>
    private static string? Check(WorldSave after, WorldSave before, IReadOnlyList<ZoneEdit> saved, int created, int removed, HashSet<(int, int)> groundReset, IReadOnlyList<ZoneReset> resets)
    {
        foreach (ZoneReset r in resets)
        {
            if (after.Zones == null || after.Zones.Generated.Contains(((short)r.X, (short)r.Z)))
            {
                return $"zone {r.X}, {r.Z} is still marked as generated";
            }
        }
        if (after.ObjectCount != before.ObjectCount + created - removed)
        {
            return $"{after.ObjectCount} objects, expected {before.ObjectCount + created - removed}";
        }
        var afterZones = after.TerrainZones.ToLookup(t => (t.ZoneX, t.ZoneZ));
        foreach (ZoneEdit e in saved)
        {
            if (!afterZones.Contains((e.ZoneX, e.ZoneZ)))
            {
                return $"zone {e.ZoneX}, {e.ZoneZ} is missing";
            }
            foreach (TerrainZone z in afterZones[(e.ZoneX, e.ZoneZ)])
            {
                for (int i = 0; i < EditStore.Cells; i++)
                {
                    if (z.ModifiedHeight[i] != e.Modified[i] || (e.Modified[i] && (z.LevelDelta[i] != e.Level[i] || z.SmoothDelta[i] != e.Smooth[i])) || z.ModifiedPaint[i] != e.PaintModified[i])
                    {
                        return $"zone {e.ZoneX}, {e.ZoneZ} differs at point {i}";
                    }
                    if (e.PaintModified[i] && (z.Paint[i] != new Vector4(e.Paint[i * 4], e.Paint[i * 4 + 1], e.Paint[i * 4 + 2], e.Paint[i * 4 + 3])))
                    {
                        return $"zone {e.ZoneX}, {e.ZoneZ} paint differs at point {i}";
                    }
                }
            }
        }
        var changed = saved.Select(e => (e.ZoneX, e.ZoneZ)).ToHashSet();
        // Every other terrain zone must be untouched.
        // (Two terrain objects in one zone: compared in order, the writer keeps the objects' order.)
        foreach (var group in before.TerrainZones.Where(t => !changed.Contains((t.ZoneX, t.ZoneZ)) && !groundReset.Contains((t.ZoneX, t.ZoneZ))).GroupBy(t => (t.ZoneX, t.ZoneZ)))
        {
            var now = afterZones[group.Key].ToList();
            int n = 0;
            foreach (TerrainZone z in group)
            {
                TerrainZone? a = n < now.Count ? now[n] : null;
                n++;
                if (a == null || !a.LevelDelta.SequenceEqual(z.LevelDelta) || !a.SmoothDelta.SequenceEqual(z.SmoothDelta) || !a.ModifiedHeight.SequenceEqual(z.ModifiedHeight)
                    || !a.ModifiedPaint.SequenceEqual(z.ModifiedPaint) || !a.Paint.SequenceEqual(z.Paint))
                {
                    return $"unchanged zone {z.ZoneX}, {z.ZoneZ} was altered";
                }
            }
        }
        // A zone whose ground is reset may lose its terrain objects (one or two each), no other.
        int resetObjects = before.TerrainZones.Count(t => groundReset.Contains((t.ZoneX, t.ZoneZ)));
        return after.TerrainZones.Count >= before.TerrainZones.Count - resetObjects ? null : "terrain zones were lost";
    }
}
