using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

// Writes terrain edits back into a chunked world save, following ZNet.SaveWorldThread:
//   1. changed chunk files are written under a new version number; untouched objects are copied
//      byte for byte, terrain objects get new TCData, new terrain objects are appended
//   2. a new save number (the highest there + 1): _main.<n>.chunks (updated index), .db2 and .fwl2
//   3. _main.<n>.ok is written last: the game only trusts a save that has it
//   4. the save is read back and compared with the edits; on any mismatch (or failure) the new files
//      are removed and the world is as it was: nothing of the old save is touched before this
//   5. the save it was made from and the chunk files it replaced are removed (KeepBase: kept, for the
//      open world's next saves, which are made from it too), and the files of Drop (an earlier save
//      of the same session) with them
// No backup folder: nothing is removed before the new save has read back right.
public static class WorldWriter
{
	private const int SaveFileVersion = 41;

	public static readonly int TombstonePrefab = StableHash.Of("Player_tombstone");

	public sealed record Result(bool Saved, string Message, string? BackupDirectory, int ZonesWritten, int ZonesCreated, List<string> Skipped, int ObjectsDeleted = 0, int ObjectsAdded = 0, int ZonesReset = 0)
	{
		// The files the save wrote (its _main files and new chunk files).
		public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
		// The save number written (-1: none).
		public int Number { get; init; } = -1;
	}

	// KeepBase: the save it is made from stays (an open world saves again from it). Drop: files of an
	// earlier save of the same session, removed once this one has read back right. Latest: the newest
	// save number the caller knows of (its own last save, or the one it opened); a newer one in the
	// folder was made by the game since, and saving would throw it away, so nothing is saved.
	public sealed record Options(bool KeepBase = false, IReadOnlyCollection<string>? Drop = null, int? Latest = null);

	// deleted: ids (WorldSave.ObjectRefs) of objects to leave out of the new save.
	// added: new objects copied from a template object of the same prefab. resets: zones handed back
	// to the world generator (their objects are removed and the zone is marked as not generated).
	public static Result Save(WorldSave world, IReadOnlyList<ZoneEdit> changed, IReadOnlyCollection<int>? deleted = null, IReadOnlyList<NewObject>? added = null, IReadOnlyList<ZoneReset>? resets = null, Options? options = null)
	{
		options ??= new Options();
		List<string> skipped = new();
		deleted ??= Array.Empty<int>();
		added ??= Array.Empty<NewObject>();
		resets ??= Array.Empty<ZoneReset>();
		if (resets.Count > 0 && world.Zones == null)
		{
			return new Result(false, "Zones can't be reset: the world's .db2 file could not be read.", null, 0, 0, skipped);
		}
		// Terrain edits of zones whose ground is reset are dropped.
		HashSet<(int, int)> groundReset = resets.Where(r => r.Ground).Select(r => (r.X, r.Z)).ToHashSet();
		changed = changed.Where(e => !groundReset.Contains((e.ZoneX, e.ZoneZ))).ToList();
		if (changed.Count == 0 && deleted.Count == 0 && added.Count == 0 && resets.Count == 0)
		{
			return new Result(false, "Nothing to save.", null, 0, 0, skipped);
		}
		if (options.Latest is int known && Newer(world.Directory, known) is string newer)
		{
			return new Result(false, newer, null, 0, 0, skipped);
		}
		if (world.Chunks.Any(c => c.WorldVersion != SaveFileVersion))
		{
			return new Result(false, $"This world uses save format {world.Chunks.First().WorldVersion}; the writer only supports {SaveFileVersion}.", null, 0, 0, skipped);
		}
		// A zone can hold two terrain objects with data (the game makes them now and then): each gets
		// the zone's ground, so whichever one the game uses has it.
		Dictionary<(int, int), List<TerrainZone>> existing = world.TerrainZones.Where(z => z.Source != null).GroupBy(z => (z.ZoneX, z.ZoneZ)).ToDictionary(g => g.Key, g => g.ToList());
		TerrainZone? template = world.TerrainZones.FirstOrDefault(z => z.Source != null);
		Dictionary<ChunkFile, HashSet<long>> removals = new();
		void Remove(ObjectRef o) => (removals.TryGetValue(o.File, out var set) ? set : removals[o.File] = new()).Add(o.Start);
		foreach (int id in deleted)
		{
			if (id >= 0 && id < world.ObjectRefs.Count)
			{
				Remove(world.ObjectRefs[id]);
			}
		}
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

		// Plan: which chunk files change, and how.
		Dictionary<ChunkFile, Dictionary<long, (ZdoLocation Loc, byte[] Data)>> patches = new();     // object start -> new TCData
		HashSet<(int, int)> notSaved = new();
		Dictionary<ChunkFile, List<byte[]>> additions = new();
		int written = 0, created = 0;
		foreach (ZoneEdit edit in changed)
		{
			byte[] data = EncodeTerrain(edit);
			if (existing.TryGetValue((edit.ZoneX, edit.ZoneZ), out List<TerrainZone>? zones))
			{
				foreach (TerrainZone zone in zones)
				{
					(patches.TryGetValue(zone.Source!.File, out var p) ? p : patches[zone.Source.File] = new())[zone.Source.Start] = (zone.Source, data);
				}
				written++;
				continue;
			}
			if (edit.HeightCount + edit.PaintCount == 0)
			{
				notSaved.Add((edit.ZoneX, edit.ZoneZ));
				continue;
			}
			ChunkFile? target = ChunkMath.Find(world.Chunks, edit.ZoneX, edit.ZoneZ);
			if (target == null)
			{
				skipped.Add($"zone {edit.ZoneX}, {edit.ZoneZ}: not generated yet (no world data there; visit it in game first)");
				notSaved.Add((edit.ZoneX, edit.ZoneZ));
				continue;
			}
			if (template == null)
			{
				skipped.Add($"zone {edit.ZoneX}, {edit.ZoneZ}: the world has no terrain object to copy yet (edit the ground once in game first)");
				notSaved.Add((edit.ZoneX, edit.ZoneZ));
				continue;
			}
			(additions.TryGetValue(target, out var a) ? a : additions[target] = new()).Add(NewTerrainObject(world, template, edit, data));
			created++;
		}
		// New objects: a copy of the first object of the same prefab, in the chunk file of their zone.
		Dictionary<ChunkFile, byte[]> sources = new();
		int addedCount = 0;
		foreach (NewObject n in added)
		{
			int zx = (int)MathF.Floor((n.Position.X + 32f) / 64f), zz = (int)MathF.Floor((n.Position.Z + 32f) / 64f);
			ChunkFile? target = ChunkMath.Find(world.Chunks, zx, zz);
			if (target == null)
			{
				skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: zone {zx}, {zz} is not generated yet");
				continue;
			}
			byte[]? bytes = world.NewObjectBytes(n, m => sources.TryGetValue(m.File, out byte[]? src) ? src : sources[m.File] = File.ReadAllBytes(Path.Combine(world.Directory, m.File.FileName)));
			if (bytes == null)
			{
				skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: unknown kind of object");
				continue;
			}
			(additions.TryGetValue(target, out var list) ? list : additions[target] = new()).Add(bytes);
			addedCount++;
		}
		if (written + created + removed + addedCount + resets.Count == 0)
		{
			return new Result(false, "Nothing could be saved.", null, 0, 0, skipped);
		}

		string dir = world.Directory;
		int newNumber = Math.Max(world.SaveNumber, LatestNumber(dir)) + 1;
		List<string> newFiles = new();
		Dictionary<ChunkFile, (uint Version, int Count)> updated = new();
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
			newFiles.AddRange(new[] { main + ".chunks", main + ".db2", main + ".fwl2", main + ".ok" });
			WriteIndex(main + ".chunks", world.Chunks, updated);
			if (resets.Count > 0)
			{
				ZoneDb db = ZoneDb.Load(Path.Combine(dir, $"_main.{world.SaveNumber}.db2"));
				foreach (ZoneReset r in resets)
				{
					db.ResetZone(r.X, r.Z);
				}
				db.Save(main + ".db2");
			}
			else
			{
				File.Copy(Path.Combine(dir, $"_main.{world.SaveNumber}.db2"), main + ".db2", overwrite: true);
			}
			File.Copy(Path.Combine(dir, $"_main.{world.SaveNumber}.fwl2"), main + ".fwl2", overwrite: true);
			// 4. Commit marker, written last like the game does.
			WriteAllBytesDurable(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
			// 5. Read back and compare with what was meant to be written (nothing old removed yet).
			string? problem = Verify(dir, world, changed.Where(e => !notSaved.Contains((e.ZoneX, e.ZoneZ))).ToList(), created + addedCount, removed, groundReset, resets);
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
		string what = string.Join(", ", new[] { written + created > 0 ? $"{written + created} zone(s)" : null, deleted.Count > 0 ? $"{deleted.Count} deleted object(s)" : null, addedCount > 0 ? $"{addedCount} new object(s)" : null, resets.Count > 0 ? $"{resets.Count} reset zone(s) ({removed - deleted.Count} objects cleared)" : null }.Where(x => x != null));
		return new Result(true, $"Saved {what} to save #{newNumber}.", null, written, created, skipped, removed, addedCount, resets.Count) { Files = newFiles, Number = newNumber };
	}

	// Mirrors TerrainComp.Save: a GZip-compressed ZPackage.
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

	// A new terrain object: the template object's bytes with this zone's position and data.
	private static byte[] NewTerrainObject(WorldSave world, TerrainZone template, ZoneEdit edit, byte[] data)
	{
		ZdoLocation src = template.Source!;
		if ((src.Flags & 0x2000) == 0)
		{
			throw new InvalidOperationException("Unexpected terrain object layout (position is not in the short format).");
		}
		byte[] file = File.ReadAllBytes(Path.Combine(world.Directory, src.File.FileName));
		byte[] head = file[(int)src.Start..(int)src.DataStart];
		// Bytes 2..5 are the position as two shorts (x, z) of the zone centre.
		BitConverter.GetBytes((short)(edit.ZoneX * 64)).CopyTo(head, 2);
		BitConverter.GetBytes((short)(edit.ZoneZ * 64)).CopyTo(head, 4);
		using MemoryStream ms = new();
		ms.Write(head);
		ms.Write(BitConverter.GetBytes(data.Length));
		ms.Write(data);
		ms.Write(file, (int)src.DataEnd, (int)(src.End - src.DataEnd));
		return ms.ToArray();
	}

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
			foreach (byte[] obj in additions ?? new())
			{
				w.Write(obj);
			}
			w.Flush();
			fs.Flush(flushToDisk: true);
		}
		return count;
	}

	private static void WriteIndex(string path, List<ChunkFile> chunks, Dictionary<ChunkFile, (uint Version, int Count)> updated)
	{
		using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
		using BinaryWriter w = new(fs);
		int Count(ChunkFile c) => updated.TryGetValue(c, out var u) ? u.Count : c.IndexCount;
		w.Write((short)SaveFileVersion);
		w.Write(chunks.Sum(Count));
		w.Write(chunks.Count);
		foreach (ChunkFile c in chunks)
		{
			w.Write(c.Chunk);
			w.Write(c.Size);
			w.Write(updated.TryGetValue(c, out var u) ? u.Version : c.Version);
			w.Write(Count(c));
		}
		w.Flush();
		fs.Flush(flushToDisk: true);
	}

	private static void WriteAllBytesDurable(string path, byte[] bytes)
	{
		using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
		fs.Write(bytes);
		fs.Flush(flushToDisk: true);
	}

	private static readonly string[] MainExtensions = { ".ok", ".chunks", ".db2", ".fwl2" };

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

	// Why a save made from save number `known` would throw away a newer one in the folder (the game
	// saved the world since it was read), or null when there is none. Only a complete save counts: one
	// cut short long ago would block every save.
	public static string? Newer(string dir, int known) => WorldSave.CommittedSave(dir) is int newest && newest > known
		? $"Valheim saved this world (save #{newest}) after the editor read it: saving now would throw away what was done in the game since. Nothing was saved. Leave the world and open it again to edit the game's save."
		: null;

	// The highest save number in a world folder (-1: none).
	public static int LatestNumber(string dir) => Directory.GetFiles(dir, "_main.*.chunks")
		.Select(f => Path.GetFileName(f).Split('.')).Where(p => p.Length == 3 && int.TryParse(p[1], out _)).Select(p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(-1).Max();

	// The world folder down to its latest save: earlier save numbers' files and the chunk files its
	// index does not list go (an open world's base, once it is left). A newer save without its commit
	// marker (cut short, or the game writing it right now) is left alone, and so are the chunk files
	// then; so are files named _main.<something else>.
	public static void Prune(string dir)
	{
		if (LatestNumber(dir) < 0)
		{
			return;
		}
		WorldSave now = WorldSave.Load(dir);
		int latest = now.SaveNumber;
		var keep = now.Chunks.Select(c => c.FileName).ToHashSet();
		var old = Directory.GetFiles(dir, "_main.*").Where(f => Path.GetFileName(f).Split('.') is [_, var n, _]
			&& int.TryParse(n, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number) && number < latest);
		if (LatestNumber(dir) == latest)
		{
			old = old.Concat(Directory.GetFiles(dir, "*.chunk").Where(f => !keep.Contains(Path.GetFileName(f))));
		}
		RemoveAll(old.ToList());
	}

	internal static string? Verify(string dir, WorldSave before, IReadOnlyList<ZoneEdit> saved, int created, int removed, HashSet<(int, int)> groundReset, IReadOnlyList<ZoneReset> resets)
	{
		WorldSave after = WorldSave.Load(dir);
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
		var changed = saved;
		// Every other terrain zone must be untouched.
		// (Two terrain objects in one zone: compared in order, the writer keeps the objects' order.)
		foreach (var group in before.TerrainZones.Where(t => changed.All(e => e.ZoneX != t.ZoneX || e.ZoneZ != t.ZoneZ) && !groundReset.Contains((t.ZoneX, t.ZoneZ))).GroupBy(t => (t.ZoneX, t.ZoneZ)))
		{
			var now = afterZones[group.Key].ToList();
			int n = 0;
			foreach (TerrainZone z in group)
			{
				TerrainZone? a = n < now.Count ? now[n] : null;
				n++;
				if (a == null || !a.LevelDelta.SequenceEqual(z.LevelDelta) || !a.ModifiedHeight.SequenceEqual(z.ModifiedHeight) || !a.ModifiedPaint.SequenceEqual(z.ModifiedPaint))
				{
					return $"unchanged zone {z.ZoneX}, {z.ZoneZ} was altered";
				}
			}
		}
		return after.TerrainZones.Count >= before.TerrainZones.Count - groundReset.Count ? null : "terrain zones were lost";
	}
}
