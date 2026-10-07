using System.Globalization;
using TerrainEditor.Save;

namespace TerrainEditor.App;

// How an editor session ended: the program closes, the user picks another world, or it could not
// start (the error is in SessionControl.Error).
public enum SessionEnd { Exit, SwitchWorld, Failed }

// Links an editor session to the app around it.
public sealed class SessionControl
{
	public TaskCompletionSource<SessionEnd> Stop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public string? Error { get; set; }

	// Folder with the files extracted from the user's own game (served after wwwroot), or null.
	public string? GameLookDir { get; set; }

	// Endpoints of the app (game-look setup, switching worlds) added to the session's server.
	public Action<WebApplication>? AddEndpoints { get; set; }
}

// One opened world: loads it (from disk or from the running game) and serves the map and the 3D
// editor until Stop completes. Also runs the command-line checks (--verify, --summary...).
public static class EditorSession
{
	public static async Task<SessionEnd> RunAsync(string[] args, SessionControl ctl)
	{
		int verifyIndex = Array.IndexOf(args, "--verify");
		if (verifyIndex >= 0 && verifyIndex + 1 < args.Length)
		{
			ValheimGen.DumpVerifier.Run(args[verifyIndex + 1], verifyIndex + 2 < args.Length ? args[verifyIndex + 2] : "5DCcdIcuYJ");
			return SessionEnd.Exit;
		}

		int port = 5180;
		int portIndex = Array.IndexOf(args, "--port");
		if (portIndex >= 0 && portIndex + 1 < args.Length)
		{
			port = int.Parse(args[portIndex + 1], CultureInfo.InvariantCulture);
		}
		string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
		string[] valued = { "--port", "--live", "--token" };
		// The world folder is the first argument that is neither an option nor an option's value.
		string defaultWorld = "";
		string? worldArg = args.Where((a, i) => !a.StartsWith("--") && !(i > 0 && valued.Contains(args[i - 1]))).FirstOrDefault();
		string worldDir = worldArg ?? defaultWorld;
		string? liveUrl = Option("--live");
		LiveBridge? live = liveUrl == null ? null : new LiveBridge(liveUrl, Option("--token") ?? Environment.GetEnvironmentVariable("WORLD_BRIDGE_TOKEN") ?? "");
		if (live == null && !Directory.Exists(worldDir))
		{
			Console.Error.WriteLine($"World folder not found: {worldDir}");
			ctl.Error = $"World folder not found: {worldDir}";
			return SessionEnd.Failed;
		}

		Console.WriteLine(live != null ? $"Loading the live world from {live.Url} ..." : $"Loading world from {worldDir} ...");
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		WorldSave.ModifierPrefabs = TerrainEditor.Terrain.TerrainModifiers.NetworkPrefabHashes.ToHashSet();
		WorldSave world;
		try
		{
			world = live != null ? await live.LoadWorld() : WorldSave.Load(worldDir);
		}
		catch (Exception ex) when (live != null)
		{
			Console.Error.WriteLine($"Could not read the live world: {ex.Message}");
			Console.Error.WriteLine("Is the server running with WorldEditorBridge, the SSH tunnel open, and the token right?");
			ctl.Error = $"Could not reach the game: {ex.Message}. Is the server running with BepInEx and WorldEditorBridge, the SSH tunnel open, and the token right?";
			return SessionEnd.Failed;
		}
		Console.WriteLine(world.IsLive
			? $"Live world '{world.Name}': {world.ObjectCount:N0} objects, {world.TerrainZones.Count} zones with terrain edits ({stopwatch.ElapsedMilliseconds} ms)."
			: $"Loaded save #{world.SaveNumber}: {world.ObjectCount:N0} objects in {world.ChunkCount} chunks, {world.TerrainZones.Count} zones with terrain edits ({stopwatch.ElapsedMilliseconds} ms).");
		stopwatch.Restart();
		// Serializes saving and replacing the loaded world.
		var saveLock = new object();
		// Live mode: which object changes the running game already has.
		var liveSync = new LiveSync();
		var modifiers = new TerrainEditor.Terrain.TerrainModifiers(world);
		var terrain = new ValheimGen.TerrainService(world, modifiers);
		Console.WriteLine($"Location flattening: {modifiers.Count} terrain modifiers from {modifiers.LocationsWithModifiers} of {world.Locations.Count} locations and {world.Placed.Count(p => p.Location == 0)} other objects.");
		Console.WriteLine($"World '{world.Name}', seed {world.SeedName} ({world.Seed}): base terrain generator ready ({stopwatch.ElapsedMilliseconds} ms).");

		int ingameIndex = Array.IndexOf(args, "--verify-ingame");
		if (ingameIndex >= 0 && ingameIndex + 1 < args.Length)
		{
			// Compare the editor's ground (generated + location flattening + saved edits, combined like
			// TerrainComp.ApplyToHeightmap) with heightmaps recorded in the game by the TerrainCheck plugin.
			var lines = File.ReadAllLines(args[ingameIndex + 1]);
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
			return SessionEnd.Exit;
		}

		if (args.Contains("--selftest-save"))
		{
			// Never run the destructive self-test on an implicit or real world folder.
			if (worldArg == null || !worldDir.Contains("/tmp/"))
			{
				Console.Error.WriteLine("--selftest-save needs an explicit world COPY under /tmp; refusing to touch " + worldDir);
				return SessionEnd.Exit;
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
			return SessionEnd.Exit;
		}

		if (args.Contains("--inspect"))
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
			return SessionEnd.Exit;
		}

		if (args.Contains("--summary"))
		{
			stopwatch.Restart();
			int overviewBytes = terrain.OverviewPng.Length;
			Console.WriteLine($"Overview map rendered: {overviewBytes:N0} bytes ({stopwatch.ElapsedMilliseconds} ms).");
			foreach (TerrainZone zone in world.TerrainZones.OrderByDescending(z => z.ModifiedHeight.Count(m => m)).Take(15))
			{
				var deltas = Enumerable.Range(0, zone.LevelDelta.Length).Where(i => zone.ModifiedHeight[i]).Select(i => zone.LevelDelta[i] + zone.SmoothDelta[i]).ToList();
				Console.WriteLine($"  zone ({zone.ZoneX},{zone.ZoneZ}) center ({zone.Center.X:0},{zone.Center.Z:0}): {deltas.Count} of {zone.ModifiedHeight.Length} heights edited" + (deltas.Count > 0 ? $" [{deltas.Min():0.00} .. {deltas.Max():0.00} m]" : "") + $", {zone.ModifiedPaint.Count(p => p)} of {zone.ModifiedPaint.Length} painted, grid {zone.HeightWidth}/{zone.PaintWidth}");
			}
			return SessionEnd.Exit;
		}

		// Serve the page from next to the executable, not from the current directory, so the editor
		// works no matter where it is started from.
		var builder = WebApplication.CreateBuilder(new WebApplicationOptions
		{
			ContentRootPath = AppContext.BaseDirectory,
			WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
		});
		builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
		builder.Logging.SetMinimumLevel(LogLevel.Warning);
		var app = builder.Build();
		app.UseDefaultFiles();
		// Shader sources (.glsl) are served as plain text, model meshes (.bin) as binary.
		var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
		contentTypes.Mappings[".glsl"] = "text/plain";
		contentTypes.Mappings[".bin"] = "application/octet-stream";
		// Page code is revalidated on every load (a cheap 304 when unchanged), so an updated editor is used
		// right away instead of a copy the browser kept; models keep the browser's normal caching.
		app.UseStaticFiles(new StaticFileOptions
		{
			ContentTypeProvider = contentTypes,
			OnPrepareResponse = ctx =>
			{
				if (!ctx.Context.Request.Path.StartsWithSegments("/models"))
				{
					ctx.Context.Response.Headers.CacheControl = "no-cache";
				}
			},
		});

		if (ctl.GameLookDir is string gameLookDir)
		{
			Directory.CreateDirectory(gameLookDir);
			app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(gameLookDir), ContentTypeProvider = contentTypes });
		}
		ctl.AddEndpoints?.Invoke(app);

		// Render the overview map in the background so the page opens immediately.
		_ = Task.Run(() => terrain.OverviewPng);
		app.MapGet("/api/overview.png", () => Results.Bytes(terrain.OverviewPng, "image/png"));

		var edits = new TerrainEditor.Editing.EditStore(world);
		var mapData = new ValheimGen.MapData(terrain, edits);
		// The game-style world map data takes a few seconds; start it right away.
		_ = Task.Run(() => mapData.Global);
		byte[]? globalMapBytes = null;
		app.MapGet("/api/map/global", () => Results.Bytes(globalMapBytes ??= mapData.Global.ToBytes(), "application/octet-stream"));
		app.MapGet("/api/map/detail", (int x0, int z0, int size) =>
		{
			if (size < 16 || size > 2048)
			{
				return Results.BadRequest("size must be 16..2048");
			}
			return Results.Bytes(mapData.Detail(x0, z0, size, edits.Version).ToBytes(), "application/octet-stream");
		});

		object ZoneSummary(TerrainEditor.Editing.ZoneEdit e)
		{
			var deltas = Enumerable.Range(0, e.Level.Length).Where(i => e.Modified[i]).Select(i => e.Level[i] + e.Smooth[i]).ToList();
			return new
			{
				x = e.ZoneX,
				z = e.ZoneZ,
				centerX = e.ZoneX * 64f,
				centerZ = e.ZoneZ * 64f,
				heights = deltas.Count,
				painted = e.PaintCount,
				min = deltas.Count > 0 ? deltas.Min() : 0f,
				max = deltas.Count > 0 ? deltas.Max() : 0f,
				changed = e.Changed
			};
		}

		app.MapGet("/api/world", () => new
		{
			directory = world.Directory,
			live = world.IsLive,
			name = world.Name,
			seedName = world.SeedName,
			mapRadius = ValheimGen.TerrainService.MapRadius,
			waterLevel = ValheimGen.TerrainService.WaterLevel,
			maxLevel = TerrainEditor.Editing.EditStore.MaxLevel,
			saveNumber = world.SaveNumber,
			objects = world.ObjectCount,
			chunks = world.ChunkCount,
			changedZones = edits.ChangedZoneCount,
			deletedObjects = world.IsLive ? liveSync.Pending(edits).Deleted : edits.DeletedCount,
			addedObjects = world.IsLive ? liveSync.Pending(edits).Added : edits.AddedCount,
			resetZones = edits.ResetCount,
			resetList = edits.Resets.Select(r => new[] { r.X, r.Z }),
			locations = world.Locations.Select(l => new { x = l.Position.X, z = l.Position.Z }),
			editVersion = edits.Version,
			mapSize = ValheimGen.MapData.GlobalSize,
			mapPixel = ValheimGen.MapData.GlobalPixel,
			zones = edits.All().Where(e => e.HeightCount + e.PaintCount > 0 || e.Changed).Select(ZoneSummary)
		});

		BlueprintEndpoints.Map(app, () => world.Name, name => world.CanCreate(StableHash.Of(name)));
		ObjectEndpoints.Map(app, () => world, edits, Pending);
		app.MapGet("/api/zones/stats", () => new { stride = ZoneStats.Stride, data = ZoneStats.Compute(world, edits, (x, z) => (int)terrain.BiomeAt(x * 64f, z * 64f)), resets = edits.Resets.Select(r => new[] { r.X, r.Z }) });
		app.MapGet("/api/search", (string q, string? what) => WorldSearch.Search(world, edits, q, what is "items" or "texts" ? what : "kinds"));

		app.MapGet("/api/zone/{x:int}/{z:int}", (int x, int z) =>
		{
			var e = edits.Get(x, z);
			if (e == null)
			{
				return Results.NotFound();
			}
			float[] baseHeights = terrain.BaseZone(x, z);
			return Results.Ok(new
			{
				biome = terrain.BiomeAt(x * 64f, z * 64f).ToString(),
				baseHeights,
				heights = Enumerable.Range(0, baseHeights.Length).Select(i => baseHeights[i] + (e.Modified[i] ? e.Level[i] + e.Smooth[i] : 0f)),
				heightWidth = TerrainEditor.Editing.EditStore.Grid,
				deltas = Enumerable.Range(0, e.Level.Length).Select(i => e.Modified[i] ? (float?)(e.Level[i] + e.Smooth[i]) : null),
				paintWidth = TerrainEditor.Editing.EditStore.Grid,
				paint = Enumerable.Range(0, e.PaintModified.Length).Select(i => e.PaintModified[i] ? new[] { e.Paint[i * 4], e.Paint[i * 4 + 1], e.Paint[i * 4 + 2], e.Paint[i * 4 + 3] } : null)
			});
		});

		// A rectangle of zones for the 3D editor: base terrain plus the current edits of each zone.
		app.MapGet("/api/region", (int x0, int z0, int x1, int z1) =>
		{
			if (x1 < x0 || z1 < z0 || x1 - x0 > 8 || z1 - z0 > 8)
			{
				return Results.BadRequest("Region must be between 1x1 and 9x9 zones.");
			}
			var keys = (from zz in Enumerable.Range(z0, z1 - z0 + 1) from zx in Enumerable.Range(x0, x1 - x0 + 1) select (zx, zz)).ToList();
			var bases = new float[keys.Count][];
			Parallel.For(0, keys.Count, i => bases[i] = terrain.BaseZone(keys[i].zx, keys[i].zz));
			// Locations whose runtime ground flattening may reach into this area.
			float minX = x0 * 64f - 32f - 40f, maxX = x1 * 64f + 32f + 40f, minZ = z0 * 64f - 32f - 40f, maxZ = z1 * 64f + 32f + 40f;
			var nearby = world.Locations.Where(l => l.Position.X >= minX && l.Position.X <= maxX && l.Position.Z >= minZ && l.Position.Z <= maxZ)
				.Select(l => new { x = l.Position.X, y = l.Position.Y, z = l.Position.Z });
			return Results.Ok(new
			{
				x0, z0, x1, z1,
				locations = nearby,
				grid = TerrainEditor.Editing.EditStore.Grid,
				zones = keys.Select((k, i) =>
				{
					var e = edits.Get(k.zx, k.zz) ?? new TerrainEditor.Editing.ZoneEdit(k.zx, k.zz);
					return new
					{
						x = k.zx,
						z = k.zz,
						biome = terrain.BiomeAt(k.zx * 64f, k.zz * 64f).ToString(),
						cornerBiomes = terrain.CornerBiomes(k.zx, k.zz),
						baseMask = terrain.BaseMask(k.zx, k.zz).Select(v => (byte)Math.Clamp((int)Math.Round(v * 255f), 0, 255)),
						existsInWorld = e.ExistsInWorld,
						generated = world.Zones?.Generated.Contains(((short)k.zx, (short)k.zz)) ?? true,
						reset = edits.Resets.FirstOrDefault(r => r.X == k.zx && r.Z == k.zz),
						biomes = terrain.VertexBiomes(k.zx, k.zz),
						baseHeights = bases[i],
						modified = e.Modified,
						level = e.Level,
						smooth = e.Smooth,
						paintModified = e.PaintModified,
						paint = e.Paint
					};
				})
			});
		});

		// Player-built pieces with their footprints, for drawing buildings on the map (float32 array).
		app.MapGet("/api/pieces", () =>
		{
			float[] data = TerrainEditor.Terrain.PieceCatalog.Encode(world, edits.Deleted);
			byte[] bytes = new byte[data.Length * 4];
			Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
			return Results.Bytes(bytes, "application/octet-stream");
		});

		// Non-piece objects (vegetation, rocks, pickables...) in a block of zones: prefab hashes, then per
		// object x, y, z, rotation x, y, z (Euler degrees), scale x, y, z (0 = prefab scale), the index into the hashes
		// and the object id. Objects deleted in the editor are left out.
		app.MapGet("/api/objects", (int x0, int z0, int x1, int z1) =>
		{
			float minX = x0 * 64f - 32f, maxX = x1 * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = z1 * 64f + 32f;
			List<int> types = new();
			Dictionary<int, int> typeIndex = new();
			List<float> data = new();
			HashSet<int> deleted = edits.Deleted;
			foreach (var (id, prefab, p, r, s) in world.Objects)
			{
				if (deleted.Contains(id))
				{
					continue;
				}
				if (p.X < minX || p.X >= maxX || p.Z < minZ || p.Z >= maxZ)
				{
					continue;
				}
				if (!typeIndex.TryGetValue(prefab, out int t))
				{
					typeIndex[prefab] = t = types.Count;
					types.Add(prefab);
				}
				data.AddRange(new[] { p.X, p.Y, p.Z, r.X, r.Y, r.Z, s.X, s.Y, s.Z, t, id });
			}
			// Objects added in the editor and not saved yet (negative ids), with what a later move needs:
			// the object they were copied from, whether they keep its data, whether they carry their own.
			List<object> addedInfo = new();
			foreach (var n in edits.Added)
			{
				var p = n.Position;
				if (p.X < minX || p.X >= maxX || p.Z < minZ || p.Z >= maxZ)
				{
					continue;
				}
				if (!typeIndex.TryGetValue(n.Prefab, out int t))
				{
					typeIndex[n.Prefab] = t = types.Count;
					types.Add(n.Prefab);
				}
				data.AddRange(new[] { p.X, p.Y, p.Z, n.Rotation.X, n.Rotation.Y, n.Rotation.Z, n.Scale, n.Scale, n.Scale, t, n.Id });
				addedInfo.Add(new { id = n.Id, sourceId = n.SourceId, fresh = n.Fresh, raw = n.Raw != null });
			}
			return Results.Json(new { types, data, added = addedInfo });
		});

		// How often each object prefab occurs in the whole world (for tooling).
		app.MapGet("/api/object-types", () => world.Objects.GroupBy(o => o.Prefab).Select(g => new { prefab = g.Key, count = g.Count() }).OrderByDescending(g => g.count));

		// Live mode: players in the running world (proxied from the bridge plugin), and a fresh snapshot.
		app.MapGet("/api/players", async () => live == null ? Results.Content("[]", "application/json") : Results.Content(await live.Players(), "application/json"));
		app.MapPost("/api/live/reload", async () =>
		{
			if (live == null)
			{
				return Results.BadRequest("not in live mode");
			}
			WorldSave fresh = await live.LoadWorld();
			lock (saveLock)
			{
				world = fresh;
				edits.ResetFrom(world);
				liveSync.Reset();
			}
			return Results.Ok(new { objects = world.ObjectCount, pending = Pending() });
		});

		// Prefab names for the last value of each /api/pieces entry.
		app.MapGet("/api/piece-types", () => TerrainEditor.Terrain.PieceCatalog.Names);

		// Write all changed zones to the world files (with backup and verification), then reload.
		app.MapPost("/api/save", async () =>
		{
			if (world.IsLive && live != null)
			{
				// Live mode: push the changed zones' terrain into the running game.
				var changedZones = edits.All().Where(e => e.Changed).ToList();
				try
				{
					List<string> done = new();
					if (changedZones.Count > 0)
					{
						string reply = await live.ApplyTerrain(changedZones.Select(e => (e.ZoneX, e.ZoneZ, WorldWriter.EncodeTerrain(e))).ToList());
						edits.MarkApplied(changedZones.Select(e => (e.ZoneX, e.ZoneZ)));
						Console.WriteLine($"Live: applied {changedZones.Count} zone(s): {reply}");
						done.Add($"{changedZones.Count} zone(s) of ground");
					}
					string objects = await liveSync.Apply(world, edits, live);
					if (objects != "")
					{
						Console.WriteLine($"Live: {objects}");
						done.Add(objects);
					}
					// Zone resets last: the game regenerates them (at once where players are), so the world is
					// read again from the game afterwards and the page reloads.
					bool reloaded = false;
					if (edits.ResetCount > 0)
					{
						var resets = edits.Resets;
						string reply = await live.ResetZones(resets);
						Console.WriteLine($"Live: reset {resets.Count} zone(s): {reply}");
						done.Add($"{resets.Count} zone(s) reset");
						WorldSave fresh = await live.LoadWorld();
						lock (saveLock)
						{
							world = fresh;
							edits.ResetFrom(world);
							liveSync.Reset();
						}
						reloaded = true;
					}
					string message = done.Count > 0 ? $"Applied to the running game: {string.Join("; ", done)}." : "Nothing to apply.";
					return Results.Ok(new { saved = done.Count > 0, live = true, reloaded, message, pending = Pending() });
				}
				catch (Exception ex)
				{
					return Results.Ok(new { saved = false, live = true, message = "Could not apply live: " + ex.Message, pending = Pending() });
				}
			}
			lock (saveLock)
			{
				var changed = edits.All().Where(e => e.Changed).ToList();
				var result = WorldWriter.Save(world, changed, edits.Deleted, edits.Added, edits.Resets);
				if (result.Saved)
				{
					world = WorldSave.Load(world.Directory);
					edits.ResetFrom(world);
				}
				Console.WriteLine($"Save: {result.Message} Backup: {result.BackupDirectory}");
				return Results.Ok(new { result.Saved, result.Message, result.BackupDirectory, result.ZonesWritten, result.ZonesCreated, result.Skipped, result.ObjectsDeleted, result.ObjectsAdded, result.ZonesReset, saveNumber = world.SaveNumber });
			}
		});

		// Throw away unsaved changes by reloading the world from disk.
		app.MapPost("/api/discard", async () =>
		{
			WorldSave fresh = live != null ? await live.LoadWorld() : WorldSave.Load(world.Directory);
			lock (saveLock)
			{
				world = fresh;
				liveSync.Reset();
				edits.ResetFrom(world);
				return Results.Ok(Pending());
			}
		});

		// The editor undid every pending change in these zones: they are back to the saved / applied state.
		app.MapPost("/api/zones/clean", (int[][] zones) =>
		{
			edits.MarkUnchanged(zones.Select(z => (z[0], z[1])));
			return Results.Ok(Pending());
		});

		// New objects placed in the editor (plant brush, paste, replace). Ids are negative and chosen by the browser.
		app.MapPost("/api/objects/add", (List<NewObjectUpload> list) =>
		{
			var known = list.Where(o => world.CanCreate(o.Prefab) || o.RawOf != null).ToList();
			// RawOf: an object of this session with its own data (edited, restored) that is moved: the copy keeps that data.
			edits.AddObjects(known.Select(o => new TerrainEditor.Editing.NewObject(o.Id, o.Prefab, new System.Numerics.Vector3(o.X, o.Y, o.Z), new System.Numerics.Vector3(o.Rx, o.Ry, o.Rz), o.Scale, o.SourceId, o.Fresh ?? true,
				o.RawOf is int r && edits.FindAdded(r)?.Raw is byte[] raw ? raw : null)));
			return Results.Ok(new { accepted = known.Count, rejected = list.Count - known.Count, pending = Pending() });
		});

		// Prefabs that can be created: an object of the kind (or a stand-in of its family) is in the world.
		app.MapGet("/api/templates", () => world.Creatable);

		// Names of every placeable game prefab, so the browser can offer kinds the world has none of yet.
		app.MapGet("/api/extra-names", () => TerrainEditor.Terrain.PrefabCatalog.Placeable.Select(p => p.Name));

		// Saplings and crops: name -> [grow radius (m), needs cultivated ground (0/1)].
		app.MapGet("/api/grow", () => TerrainEditor.Terrain.PrefabCatalog.Placeable.Where(p => p.GrowRadius > 0)
			.ToDictionary(p => p.Name, p => new[] { p.GrowRadius, p.NeedsCultivated ? 1f : 0f }));

		// Hand zones back to the world generator on save (undo = false), or cancel that (undo = true).
		app.MapPost("/api/reset-zones", (ResetRequest req) =>
		{
			foreach (int[] z in req.Zones)
			{
				edits.SetReset(new TerrainEditor.Editing.ZoneReset(z[0], z[1], req.KeepBuildings, req.Ground), !req.Undo);
			}
			return Results.Ok(Pending());
		});

		// Mark objects (ids from /api/pieces and /api/objects) as deleted, or bring them back (undo).
		app.MapPost("/api/delete", (DeleteRequest req) =>
		{
			// Negative ids are objects added in this session.
			edits.SetDeleted(req.Ids.Where(id => id < world.ObjectRefs.Count), !req.Restore);
			return Results.Ok(Pending());
		});

		// The browser sends the zones it changed after every brush stroke, undo or redo.
		app.MapPost("/api/zones", (List<ZoneUpload> uploads) =>
		{
			foreach (ZoneUpload u in uploads)
			{
				edits.Put(new TerrainEditor.Editing.ZoneEdit(u.X, u.Z)
				{
					Modified = u.Modified,
					Level = u.Level,
					Smooth = u.Smooth,
					PaintModified = u.PaintModified,
					Paint = u.Paint
				});
			}
			return Results.Ok(Pending());
		});

		// What is waiting to be saved.
		object Pending()
		{
			if (world.IsLive)
			{
				var (d, a) = liveSync.Pending(edits);
				return new { changedZones = edits.ChangedZoneCount, deletedObjects = d, addedObjects = a, resetZones = edits.ResetCount };
			}
			return new { changedZones = edits.ChangedZoneCount, deletedObjects = edits.DeletedCount, addedObjects = edits.AddedCount, resetZones = edits.ResetCount };
		}

		await app.StartAsync();
		Console.WriteLine($"Editor ready at http://127.0.0.1:{port}");
		ctl.Ready.TrySetResult();
		SessionEnd end = await ctl.Stop.Task;
		await app.StopAsync();
		await app.DisposeAsync();
		return end;
	}
}

record ZoneUpload(int X, int Z, bool[] Modified, float[] Level, float[] Smooth, bool[] PaintModified, float[] Paint);
record DeleteRequest(int[] Ids, bool Restore);
record NewObjectUpload(int Id, int Prefab, float X, float Y, float Z, float Rx, float Ry, float Rz, float Scale, int? SourceId, bool? Fresh, int? RawOf = null);
record ResetRequest(int[][] Zones, bool KeepBuildings, bool Ground, bool Undo);
