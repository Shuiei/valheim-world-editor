using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>
    /// Writes the planned files under a new save number, marks it committed and reads it back to check it. On any
    /// failure the new files are removed and the refusal is returned; nothing old is touched.
    /// </summary>
    /// <returns>The refusal, or null when the save is written and checked (the plan then holds its files and read-back).</returns>
    internal static Result? Commit(WorldSave world, Planned plan, IReadOnlyList<ZoneReset> resets)
    {
        string dir = world.Directory;
        plan.NewNumber = Math.Max(world.SaveNumber, LatestNumber(dir)) + 1;
        try
        {
            // 2. Changed chunk files under a new version.
            foreach (ChunkFile file in plan.Patches.Keys.Union(plan.Additions.Keys).Union(plan.Removals.Keys))
            {
                plan.Patches.TryGetValue(file, out var p);
                plan.Additions.TryGetValue(file, out var a);
                plan.Removals.TryGetValue(file, out var r);
                uint version = file.Version + 1;
                while (File.Exists(Path.Combine(dir, ChunkFile.NameFor(file.Chunk, file.Size, version))))
                {
                    version++;
                }
                string path = Path.Combine(dir, ChunkFile.NameFor(file.Chunk, file.Size, version));
                plan.NewFiles.Add(path);
                int count = WriteChunk(Path.Combine(dir, file.FileName), path, file, p, a, r);
                plan.Updated[file] = (version, count);
            }
            // 3. New save number: index, world data, metadata.
            string main = Path.Combine(dir, $"_main.{plan.NewNumber}");
            // Each file is listed before it is written: a failure halfway removes it too.
            plan.NewFiles.AddRange([main + ".chunks", main + ".db2", main + ".fwl2", main + ".ok"]);
            WriteIndex(main + ".chunks", world.SaveVersion, [.. world.Chunks.Select(c => plan.Updated.TryGetValue(c, out var u) ? (c.Chunk, c.Size, u.Version, u.Count) : (c.Chunk, c.Size, c.Version, c.IndexCount))]);
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
            plan.Read = WorldSave.Load(dir);
            string? problem = Check(plan.Read, world, [.. plan.Changed.Where(e => !plan.NotSaved.Contains((e.ZoneX, e.ZoneZ)))], plan.Created + plan.AddedCount, plan.Removed, plan.GroundReset, resets);
            if (problem != null)
            {
                RemoveAll(plan.NewFiles);
                return Refused(Said.Of("save.writer.badReadBack", problem), plan.Skipped);
            }
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "writing the save's files (the new files are removed, nothing saved)");
            RemoveAll(plan.NewFiles);
            return Refused(Said.Of("save.writer.failed", ex.Message), plan.Skipped);
        }
        return null;
    }

    /// <summary>Removes the save it was made from (unless kept), the chunk files it replaced and an earlier save of the session (Drop).</summary>
    internal static void RemoveOld(WorldSave world, Planned plan, Options options)
    {
        string dir = world.Directory;
        var old = new List<string>();
        if (!options.KeepBase)
        {
            old.AddRange(MainExtensions.Select(ext => Path.Combine(dir, $"_main.{world.SaveNumber}{ext}")));
            old.AddRange(plan.Updated.Keys.Select(c => Path.Combine(dir, c.FileName)));
        }
        if (options.Drop != null)
        {
            old.AddRange(options.Drop.Where(f => !plan.NewFiles.Contains(f)));
        }
        RemoveAll(old);
    }

    /// <summary>The message's list of what the save did (zones, deletions, additions, resets).</summary>
    internal static Said Summary(Planned plan, IReadOnlyList<ZoneReset> resets) =>
        Said.Join(", ",
        [
            plan.Written + plan.Created > 0 ? Said.Of("save.writer.zones", plan.Written + plan.Created) : Said.Empty,
            plan.DeletedCount > 0 ? Said.Of("save.writer.deleted", plan.DeletedCount) : Said.Empty,
            plan.AddedCount > 0 ? Said.Of("save.writer.added", plan.AddedCount) : Said.Empty,
            resets.Count > 0 ? Said.Of("save.writer.resets", resets.Count, plan.Removed - plan.DeletedCount) : Said.Empty,
        ]);
}
