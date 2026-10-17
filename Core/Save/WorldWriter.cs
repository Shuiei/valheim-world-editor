using TerrainEditor.App;
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
public static partial class WorldWriter
{
    /// <summary>Prefab hash of a player's tombstone, which zone resets never clear.</summary>
    public static readonly int TombstonePrefab = StableHash.Of("Player_tombstone");

    /// <summary>The outcome of a <c>Save</c>.</summary>
    /// <param name="Saved">Whether a new save was written.</param>
    /// <param name="Message">What was saved, or why nothing was, for the user, in the language in use.</param>
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

        /// <summary>The message in both languages (a result made with only a message: that text on both sides).</summary>
        public Said Report { get => field ?? Said.Raw(Message); init; }
    }

    /// <summary>A save refused or failed: nothing written, why in both languages.</summary>
    /// <param name="why">Why nothing was saved.</param>
    /// <param name="skipped">The edits that could not be saved so far.</param>
    private static Result Refused(Said why, List<string> skipped)
    {
        return new Result(false, why.Shown, null, 0, 0, skipped) { Report = why };
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
            return Refused(Said.Of("save.writer.noDb"), skipped);
        }
        // Terrain edits of zones whose ground is reset are dropped.
        HashSet<(int, int)> groundReset = [.. resets.Where(r => r.Ground).Select(r => (r.X, r.Z))];
        changed = [.. changed.Where(e => !groundReset.Contains((e.ZoneX, e.ZoneZ)))];
        if (changed.Count == 0 && deleted.Count == 0 && added.Count == 0 && resets.Count == 0)
        {
            return Refused(Said.Of("save.writer.nothing"), skipped);
        }
        if (options.Latest is int known && Newer(world.Directory, known) is Said newer)
        {
            return Refused(newer, skipped);
        }
        // Each file keeps the format it was read in, as the game leaves the chunk files it does not write again: the
        // formats the writer knows lay objects out the same, so objects copied or made here fit either.
        if (UnwritableFormat(world) is int format)
        {
            return Refused(Said.Of("save.writer.format", format, WorldVersion.OldestWritable, WorldVersion.Latest), skipped);
        }
        // New ground copies a terrain object whose position is in the short format (the layout the copy patches).
        bool anyTerrain = world.TerrainZones.Any(z => z.Source != null);
        TerrainZone? template = world.TerrainZones.FirstOrDefault(z => z.Source != null && (z.Source.Flags & ZdoData.SmallPositionFlag) != 0);
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
                var (zx, zz) = ChunkMath.SectorOf(n.Position.X, n.Position.Z);
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
            Log.Caught(ex, "making the save's changes (nothing saved)");
            return Refused(Said.Of("save.writer.failed", ex.Message), skipped);
        }
        if (written + created + removed + addedCount + resets.Count == 0)
        {
            return Refused(Said.Of("save.writer.nothingSaved"), skipped);
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
            WriteIndex(main + ".chunks", world.SaveVersion, [.. world.Chunks.Select(c => updated.TryGetValue(c, out var u) ? (c.Chunk, c.Size, u.Version, u.Count) : (c.Chunk, c.Size, c.Version, c.IndexCount))]);
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
            WriteAllBytesDurable(main + ".ok", BitConverter.GetBytes(world.SaveVersion));
            DirectorySync.Flush(dir);
            // 5. Read back and compare with what was meant to be written (nothing old removed yet).
            read = WorldSave.Load(dir);
            string? problem = Check(read, world, [.. changed.Where(e => !notSaved.Contains((e.ZoneX, e.ZoneZ)))], created + addedCount, removed, groundReset, resets);
            if (problem != null)
            {
                RemoveAll(newFiles);
                return Refused(Said.Of("save.writer.badReadBack", problem), skipped);
            }
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "writing the save's files (the new files are removed, nothing saved)");
            RemoveAll(newFiles);
            return Refused(Said.Of("save.writer.failed", ex.Message), skipped);
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
        var what = Said.Join(", ",
        [
            written + created > 0 ? Said.Of("save.writer.zones", written + created) : Said.Empty,
            deletedCount > 0 ? Said.Of("save.writer.deleted", deletedCount) : Said.Empty,
            addedCount > 0 ? Said.Of("save.writer.added", addedCount) : Said.Empty,
            resets.Count > 0 ? Said.Of("save.writer.resets", resets.Count, removed - deletedCount) : Said.Empty,
        ]);
        var saved = Said.Of("save.writer.saved", what, newNumber);
        return new Result(true, saved.Shown, null, written, created, skipped, removed, addedCount, resets.Count) { Files = newFiles, Number = newNumber, Read = read, Report = saved };
    }
}
