using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;

// Developer checks of the world generator and the save writer against the game (moved here from the
// old web app's command line; players never need them):
//   verify <dump> [seed]          the generator against a dump recorded in the game (DumpVerifier)
//   verify-ingame <world> <file>  the editor's ground against heightmaps recorded by TerrainCheck
//   selftest-save <world copy>    edits, saves and reloads a COPY of a world (under /tmp only)
//   inspect <world>               the chunk mapping and the layout of terrain objects
//   summary <world>               the overview map and the most edited zones
if (args.Length < 2)
{
	Console.Error.WriteLine("usage: WorldCheck verify <dump> [seed] | verify-ingame <world> <file> | selftest-save <world copy> | inspect <world> | summary <world>");
	return 2;
}
if (args[0] == "verify")
{
	var r = ValheimGen.DumpVerifier.Run(args[1], args.Length > 2 ? args[2] : "5DCcdIcuYJ");
	return 0;
}
string worldDir = args[1];
if (!Directory.Exists(worldDir))
{
	Console.Error.WriteLine($"World folder not found: {worldDir}");
	return 1;
}
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
WorldSave.ModifierPrefabs = TerrainEditor.Terrain.TerrainModifiers.NetworkPrefabHashes.ToHashSet();
WorldSave world = WorldSave.Load(worldDir);
WorldSave.Builder = Builders.Default(world);
Console.WriteLine($"Loaded save #{world.SaveNumber}: {world.ObjectCount:N0} objects in {world.ChunkCount} chunks, {world.TerrainZones.Count} zones with terrain edits ({stopwatch.ElapsedMilliseconds} ms).");
stopwatch.Restart();
var modifiers = new TerrainEditor.Terrain.TerrainModifiers(world);
var terrain = new ValheimGen.TerrainService(world, modifiers);
Console.WriteLine($"World '{world.Name}', seed {world.SeedName} ({world.Seed}): {modifiers.Count} terrain modifiers ({stopwatch.ElapsedMilliseconds} ms).");
switch (args[0])
{
	case "verify-ingame":
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("verify-ingame needs the TerrainCheck file");
			return 2;
		}
		// Compare the editor's ground (generated + location flattening + saved edits, combined like
			// TerrainComp.ApplyToHeightmap) with heightmaps recorded in the game by the TerrainCheck plugin.
			var lines = File.ReadAllLines(args[2]);
			Console.WriteLine(lines.FirstOrDefault(l => l.StartsWith('#')) ?? "");
			int zonesExact = 0, zonesClose = 0, zonesOff = 0, rawBetter = 0;
			double maxWith = 0, maxWithout = 0;
			var report = new List<string>();
			foreach (string line in lines.Where(l => l.StartsWith("Z ")))
			{
				var p = line.Split(' ');
				int zx = int.Parse(p[1]), zz = int.Parse(p[2]);
				float[] game = p.Skip(3).Select(h => BitConverter.Int32BitsToSingle(int.Parse(h, System.Globalization.NumberStyles.HexNumber))).ToArray();
				var zone = world.TerrainZones.FirstOrDefault(t => t.ZoneX == zx && t.ZoneZ == zz);
				float[] Combine(float[] ground)
				{
					float[] r = (float[])ground.Clone();
					if (zone != null)
					{
						for (int i = 0; i < r.Length; i++)
						{
							float l = zone.LevelDelta[i], sm = zone.SmoothDelta[i];
							if (l != 0f || sm != 0f) r[i] = Math.Clamp(ground[i] + l + sm, ground[i] - 8f, ground[i] + 8f);
						}
					}
					return r;
				}
				float[] with = Combine(terrain.BaseZone(zx, zz)), without = Combine(terrain.RawZone(zx, zz));
				double dWith = game.Zip(with, (a, b) => (double)Math.Abs(a - b)).Max(), dWithout = game.Zip(without, (a, b) => (double)Math.Abs(a - b)).Max();
				maxWith = Math.Max(maxWith, dWith); maxWithout = Math.Max(maxWithout, dWithout);
				int mods = modifiers.InZone(zx, zz).Count;
				if (dWith < 1e-4) zonesExact++; else if (dWith < 0.01) zonesClose++; else { zonesOff++; report.Add($"  zone ({zx},{zz}): max diff {dWith:0.000} m with flattening, {dWithout:0.000} m without; {mods} modifiers, edits: {zone != null}"); }
				if (dWithout > dWith + 1e-3) rawBetter++;
			}
			Console.WriteLine($"Zones compared: {zonesExact + zonesClose + zonesOff}: {zonesExact} exact, {zonesClose} within 1 cm, {zonesOff} off");
			Console.WriteLine($"Max difference: {maxWith:0.0000} m with location flattening, {maxWithout:0.0000} m without ({rawBetter} zones improved by the flattening)");
			report.Take(30).ToList().ForEach(Console.WriteLine);
		return 0;
	}
	case "selftest-save":
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
			var e1 = store.Get(exist.ZoneX, exist.ZoneZ)!;
			for (int i = 0; i < 300; i++) { e1.Modified[i] = true; e1.Level[i] = 1.25f; }
			e1.PaintModified[2000] = true; e1.Paint[8000] = 0f; e1.Paint[8001] = 0f; e1.Paint[8002] = 1f; e1.Paint[8003] = 1f;
			store.Put(e1);
			(int zx, int zz) fresh = (from z in Enumerable.Range(-6, 13) from x in Enumerable.Range(-6, 13) let k = (x, z)
				where !world.TerrainZones.Any(t => t.ZoneX == k.x && t.ZoneZ == k.z) && ChunkMath.Find(world.Chunks, k.x, k.z) != null select k).First();
			var e2 = new TerrainEditor.Editing.ZoneEdit(fresh.zx, fresh.zz);
			for (int i = 2000; i < 2100; i++) { e2.Modified[i] = true; e2.Level[i] = -2f; e2.Smooth[i] = 0.5f; }
			store.Put(e2);
			var e3 = new TerrainEditor.Editing.ZoneEdit(150, 150);
			e3.Modified[5] = true; e3.Level[5] = 3f;
			store.Put(e3);
			var hashesBefore = Directory.GetFiles(world.Directory, "*.chunk").ToDictionary(Path.GetFileName, f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
			var result = WorldWriter.Save(world, store.All().Where(z => z.Changed).ToList());
			Console.WriteLine($"Result: saved={result.Saved} '{result.Message}' written={result.ZonesWritten} created={result.ZonesCreated} skipped=[{string.Join("; ", result.Skipped)}]");
			Console.WriteLine($"Backup: {result.BackupDirectory}");
			var after = WorldSave.Load(world.Directory);
			Console.WriteLine($"After: save #{after.SaveNumber}, {after.ObjectCount:N0} objects (was {world.ObjectCount:N0}), {after.TerrainZones.Count} terrain zones (was {world.TerrainZones.Count})");
			var z1 = after.TerrainZones.First(t => t.ZoneX == exist.ZoneX && t.ZoneZ == exist.ZoneZ);
			var z2 = after.TerrainZones.FirstOrDefault(t => t.ZoneX == fresh.zx && t.ZoneZ == fresh.zz);
			Console.WriteLine($"Existing zone ({exist.ZoneX},{exist.ZoneZ}): point 0 level {z1.LevelDelta[0]}, point 299 {z1.LevelDelta[299]}, paint 2000 {z1.Paint[2000]}; untouched point 4000 same as before: {z1.LevelDelta[4000] == exist.LevelDelta[4000] && z1.ModifiedHeight[4000] == exist.ModifiedHeight[4000]}");
			Console.WriteLine($"New zone ({fresh.zx},{fresh.zz}): {(z2 == null ? "MISSING" : $"found in {z2.Source!.File.FileName}, point 2050 level {z2.LevelDelta[2050]} smooth {z2.SmoothDelta[2050]}, {z2.ModifiedHeight.Count(m => m)} edited points")}");
			Console.WriteLine($"Ungenerated zone (150,150) written: {after.TerrainZones.Any(t => t.ZoneX == 150 && t.ZoneZ == 150)}");
			var hashesAfter = Directory.GetFiles(world.Directory, "*.chunk").ToDictionary(Path.GetFileName, f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
			int same = hashesBefore.Count(kv => hashesAfter.TryGetValue(kv.Key, out var h) && h == kv.Value);
			Console.WriteLine($"Chunk files byte-identical to before: {same} of {hashesBefore.Count}; new files: {string.Join(", ", hashesAfter.Keys.Except(hashesBefore.Keys))}; removed: {string.Join(", ", hashesBefore.Keys.Except(hashesAfter.Keys))}");
			Console.WriteLine($"Main files now: {string.Join(", ", Directory.GetFiles(world.Directory, "_main.*").Select(Path.GetFileName).OrderBy(n => n))}");
		return 0;
	}
	case "inspect":
	{
		// Checks used while building the writer: chunk mapping and the layout of terrain objects.
			int ok = 0, bad = 0;
			foreach (TerrainZone z in world.TerrainZones)
			{
				var (sx, sz) = ChunkMath.SectorOf(z.Center.X, z.Center.Z);
				ChunkFile? expected = ChunkMath.Find(world.Chunks, sx, sz);
				if (expected == z.Source!.File) ok++;
				else { bad++; Console.WriteLine($"  zone ({z.ZoneX},{z.ZoneZ}) is in {z.Source.File.FileName}, mapping says {expected?.FileName ?? "none"}"); }
			}
			Console.WriteLine($"Chunk mapping: {ok} terrain objects in the expected file, {bad} elsewhere.");
			Console.WriteLine($"Locations: {world.Locations.Count}; chunk files: {string.Join(", ", world.Chunks.Select(c => $"{c.FileName}({c.Count}/{c.IndexCount})"))}");
			foreach (TerrainZone z in world.TerrainZones.Take(3))
			{
				var src = z.Source!;
				byte[] bytes = File.ReadAllBytes(Path.Combine(world.Directory, src.File.FileName));
				Console.WriteLine($"  terrain object zone ({z.ZoneX},{z.ZoneZ}) prefab {z.Prefab} flags 0x{src.Flags:x4} pos ({z.Center.X},{z.Center.Y},{z.Center.Z}) object {src.End - src.Start} B, TCData {src.DataEnd - src.DataStart - 4} B");
				Console.WriteLine($"    header bytes: {Convert.ToHexString(bytes, (int)src.Start, (int)(src.DataStart - src.Start))}  tail bytes after data: {src.End - src.DataEnd}");
			}
		return 0;
	}
	case "summary":
	{
		stopwatch.Restart();
			int overviewBytes = terrain.OverviewPng.Length;
			Console.WriteLine($"Overview map rendered: {overviewBytes:N0} bytes ({stopwatch.ElapsedMilliseconds} ms).");
			foreach (TerrainZone zone in world.TerrainZones.OrderByDescending(z => z.ModifiedHeight.Count(m => m)).Take(15))
			{
				var deltas = Enumerable.Range(0, zone.LevelDelta.Length).Where(i => zone.ModifiedHeight[i]).Select(i => zone.LevelDelta[i] + zone.SmoothDelta[i]).ToList();
				Console.WriteLine($"  zone ({zone.ZoneX},{zone.ZoneZ}) center ({zone.Center.X:0},{zone.Center.Z:0}): {deltas.Count} of {zone.ModifiedHeight.Length} heights edited" + (deltas.Count > 0 ? $" [{deltas.Min():0.00} .. {deltas.Max():0.00} m]" : "") + $", {zone.ModifiedPaint.Count(p => p)} of {zone.ModifiedPaint.Length} painted, grid {zone.HeightWidth}/{zone.PaintWidth}");
			}
		return 0;
	}
	default:
		Console.Error.WriteLine("unknown check " + args[0]);
		return 2;
}
