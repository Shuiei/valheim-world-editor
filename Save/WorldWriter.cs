using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

// Writes terrain edits back into a chunked world save, following ZNet.SaveWorldThread:
//   1. full backup of the world folder (editor-specific, next to the world folder)
//   2. changed chunk files are written under a new version number; untouched objects are copied
//      byte for byte, terrain objects get new TCData, new terrain objects are appended
//   3. a new save number: _main.<n+1>.chunks (updated index), .db2 and .fwl2 (copied)
//   4. _main.<n+1>.ok is written last: the game only trusts a save that has it
//   5. the previous save number's files and the replaced chunk files are removed
// Afterwards the save is read back and compared with the edits; on any mismatch the backup is
// restored.
public static class WorldWriter
{
	private const int SaveFileVersion = 41;

	public sealed record Result(bool Saved, string Message, string? BackupDirectory, int ZonesWritten, int ZonesCreated, List<string> Skipped, int ObjectsDeleted = 0, int ObjectsAdded = 0, int ZonesReset = 0);

	// deleted: ids (WorldSave.ObjectRefs) of objects to leave out of the new save.
	// added: new objects copied from a template object of the same prefab. resets: zones handed back
	// to the world generator (their objects are removed and the zone is marked as not generated).
	public static Result Save(WorldSave world, IReadOnlyList<ZoneEdit> changed, IReadOnlyCollection<int>? deleted = null, IReadOnlyList<NewObject>? added = null, IReadOnlyList<ZoneReset>? resets = null)
	{
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
		if (world.Chunks.Any(c => c.WorldVersion != SaveFileVersion))
		{
			return new Result(false, $"This world uses save format {world.Chunks.First().WorldVersion}; the writer only supports {SaveFileVersion}.", null, 0, 0, skipped);
		}
		Dictionary<(int, int), TerrainZone> existing = world.TerrainZones.Where(z => z.Source != null).ToDictionary(z => (z.ZoneX, z.ZoneZ));
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
			if (resetByZone.TryGetValue(o.Zone, out ZoneReset? r) && !(r.KeepBuildings && o.IsPiece) && !(o.IsTerrain && !r.Ground))
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
			if (existing.TryGetValue((edit.ZoneX, edit.ZoneZ), out TerrainZone? zone))
			{
				(patches.TryGetValue(zone.Source!.File, out var p) ? p : patches[zone.Source.File] = new())[zone.Source.Start] = (zone.Source, data);
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
			ObjectRef? model = world.ModelFor(n.Prefab, n.SourceId);
			if (model == null)
			{
				skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: no object of that kind in the world to copy");
				continue;
			}
			ChunkFile? target = ChunkMath.Find(world.Chunks, zx, zz);
			if (target == null)
			{
				skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: zone {zx}, {zz} is not generated yet");
				continue;
			}
			if (!sources.TryGetValue(model.File, out byte[]? src))
			{
				sources[model.File] = src = File.ReadAllBytes(Path.Combine(world.Directory, model.File.FileName));
			}
			byte[] bytes = ZdoBuilder.Build(src, model, model.File.WorldVersion, n.Position, n.Rotation, n.Scale);
			(additions.TryGetValue(target, out var list) ? list : additions[target] = new()).Add(bytes);
			addedCount++;
		}
		if (written + created + removed + addedCount + resets.Count == 0)
		{
			return new Result(false, "Nothing could be saved.", null, 0, 0, skipped);
		}

		string backup = Backup(world.Directory);
		string dir = world.Directory;
		int newNumber = world.SaveNumber + 1;
		List<string> newFiles = new();
		try
		{
			// 2. Changed chunk files under a new version.
			Dictionary<ChunkFile, (uint Version, int Count)> updated = new();
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
				int count = WriteChunk(Path.Combine(dir, file.FileName), path, file, p, a, r);
				newFiles.Add(path);
				updated[file] = (version, count);
			}
			// 3. New save number: index, world data, metadata.
			string main = Path.Combine(dir, $"_main.{newNumber}");
			WriteIndex(main + ".chunks", world.Chunks, updated);
			newFiles.Add(main + ".chunks");
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
			newFiles.Add(main + ".db2");
			newFiles.Add(main + ".fwl2");
			// 4. Commit marker, written last like the game does.
			WriteAllBytesDurable(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
			newFiles.Add(main + ".ok");
			// 5. Remove the previous save number and the replaced chunk files.
			foreach (string ext in new[] { ".ok", ".chunks", ".db2", ".fwl2" })
			{
				File.Delete(Path.Combine(dir, $"_main.{world.SaveNumber}{ext}"));
			}
			foreach (ChunkFile file in updated.Keys)
			{
				File.Delete(Path.Combine(dir, file.FileName));
			}
			// 6. Read back and compare with what was meant to be written.
			string? problem = Verify(dir, world, changed.Where(e => !notSaved.Contains((e.ZoneX, e.ZoneZ))).ToList(), created + addedCount, removed, groundReset, resets);
			if (problem != null)
			{
				Restore(backup, dir);
				return new Result(false, "The saved world did not read back correctly (" + problem + "). The backup was restored; nothing changed.", backup, 0, 0, skipped);
			}
		}
		catch (Exception ex)
		{
			Restore(backup, dir);
			return new Result(false, "Saving failed (" + ex.Message + "). The backup was restored; nothing changed.", backup, 0, 0, skipped);
		}
		string what = string.Join(", ", new[] { written + created > 0 ? $"{written + created} zone(s)" : null, deleted.Count > 0 ? $"{deleted.Count} deleted object(s)" : null, addedCount > 0 ? $"{addedCount} new object(s)" : null, resets.Count > 0 ? $"{resets.Count} reset zone(s) ({removed - deleted.Count} objects cleared)" : null }.Where(x => x != null));
		return new Result(true, $"Saved {what} to save #{newNumber}.", backup, written, created, skipped, removed, addedCount, resets.Count);
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

	private static string Backup(string dir)
	{
		string trimmed = dir.TrimEnd(Path.DirectorySeparatorChar);
		string backup = $"{trimmed}_backup_terraineditor-{DateTime.Now:yyyyMMdd-HHmmss}";
		Directory.CreateDirectory(backup);
		foreach (string file in Directory.GetFiles(trimmed))
		{
			File.Copy(file, Path.Combine(backup, Path.GetFileName(file)));
		}
		return backup;
	}

	private static void Restore(string backup, string dir)
	{
		foreach (string file in Directory.GetFiles(dir))
		{
			File.Delete(file);
		}
		foreach (string file in Directory.GetFiles(backup))
		{
			File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
		}
	}

	private static string? Verify(string dir, WorldSave before, IReadOnlyList<ZoneEdit> saved, int created, int removed, HashSet<(int, int)> groundReset, IReadOnlyList<ZoneReset> resets)
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
		foreach (ZoneEdit e in saved)
		{
			TerrainZone? z = after.TerrainZones.FirstOrDefault(t => t.ZoneX == e.ZoneX && t.ZoneZ == e.ZoneZ);
			if (z == null)
			{
				return $"zone {e.ZoneX}, {e.ZoneZ} is missing";
			}
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
		var changed = saved;
		// Every other terrain zone must be untouched.
		foreach (TerrainZone z in before.TerrainZones.Where(t => changed.All(e => e.ZoneX != t.ZoneX || e.ZoneZ != t.ZoneZ) && !groundReset.Contains((t.ZoneX, t.ZoneZ))))
		{
			TerrainZone? a = after.TerrainZones.FirstOrDefault(t => t.ZoneX == z.ZoneX && t.ZoneZ == z.ZoneZ);
			if (a == null || !a.LevelDelta.SequenceEqual(z.LevelDelta) || !a.ModifiedHeight.SequenceEqual(z.ModifiedHeight) || !a.ModifiedPaint.SequenceEqual(z.ModifiedPaint))
			{
				return $"unchanged zone {z.ZoneX}, {z.ZoneZ} was altered";
			}
		}
		return after.TerrainZones.Count >= before.TerrainZones.Count - groundReset.Count ? null : "terrain zones were lost";
	}
}
