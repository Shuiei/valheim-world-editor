using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

// Restoring an area from a backup (like WorldEdit's //restore with snapshots): the editor's own backups
// (<World>_backup_terraineditor-<date>) and the game's (<World>_backup_auto-<date>) sit next to the world
// folder; any other copy of the same world can be chosen too. The ground and the objects of the area
// come back as they were in the backup, objects with all their data (chest contents, sign texts...).
public static class BackupEndpoints
{
	// The backups found next to a world folder, newest first (Backups, in the shared library).
	public static List<Backups.Info> Find(string worldDir) => Backups.Find(worldDir);

	// The backup world last opened (one at a time: worlds can be large).
	private sealed class Open
	{
		public required WorldSave World { get; init; }

		public required EditStore Ground { get; init; }
	}

	private static Open? _open;

	private static readonly object Lock = new();

	public static void Map(WebApplication app, Func<WorldSave> world, EditStore edits, Func<object> pending)
	{
		app.MapGet("/api/backups", () => world().IsLive ? new List<Backups.Info>() : Find(world().Directory));

		app.MapPost("/api/backup/open", (BackupOpen req) =>
		{
			if (string.IsNullOrWhiteSpace(req.Path) || !Directory.Exists(req.Path))
			{
				return Results.BadRequest("That folder does not exist.");
			}
			WorldSave backup;
			try
			{
				backup = WorldSave.Load(req.Path);
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
			{
				return Results.BadRequest($"That folder is not a world save that can be read: {ex.Message}");
			}
			WorldSave w = world();
			if (backup.Seed != w.Seed || backup.SeedName != w.SeedName)
			{
				return Results.BadRequest($"That is another world (seed {backup.SeedName}, this one is {w.SeedName}).");
			}
			lock (Lock)
			{
				_open = new Open { World = backup, Ground = new EditStore(backup) };
			}
			return Results.Ok(new { path = req.Path, name = backup.Name, saveNumber = backup.SaveNumber, objects = backup.ObjectCount, date = Directory.GetLastWriteTime(req.Path) });
		});

		// The backup's ground edits in a block of zones, in the format of /api/region.
		app.MapGet("/api/backup/region", (int x0, int z0, int x1, int z1) =>
		{
			Open? o = _open;
			if (o == null)
			{
				return Results.BadRequest("Open a backup first.");
			}
			if (x1 < x0 || z1 < z0 || x1 - x0 > 8 || z1 - z0 > 8)
			{
				return Results.BadRequest("Region must be between 1x1 and 9x9 zones.");
			}
			var zones = from zz in Enumerable.Range(z0, z1 - z0 + 1)
						from zx in Enumerable.Range(x0, x1 - x0 + 1)
						let e = o.Ground.Get(zx, zz) ?? new ZoneEdit(zx, zz)
						select new { x = zx, z = zz, modified = e.Modified, level = e.Level, smooth = e.Smooth, paintModified = e.PaintModified, paint = e.Paint };
			return Results.Ok(new { zones });
		});

		// The backup's objects in a block of zones (the kinds the editor shows: everything but terrain
		// and location bookkeeping), with the ids they have in the backup.
		app.MapGet("/api/backup/objects", (int x0, int z0, int x1, int z1) =>
		{
			Open? o = _open;
			if (o == null)
			{
				return Results.BadRequest("Open a backup first.");
			}
			float minX = x0 * 64f - 32f, maxX = x1 * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = z1 * 64f + 32f;
			bool Inside(System.Numerics.Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
			var objects = o.World.Objects.Where(b => Inside(b.Position))
				.Select(b => new { id = b.Id, prefab = b.Prefab, name = ObjectEndpoints.PrefabName(b.Prefab), x = b.Position.X, y = b.Position.Y, z = b.Position.Z, rx = b.Rotation.X, ry = b.Rotation.Y, rz = b.Rotation.Z, piece = false })
				.Concat(o.World.Pieces.Where(b => Inside(b.Position))
					.Select(b => new { id = b.Id, prefab = b.Prefab, name = ObjectEndpoints.PrefabName(b.Prefab), x = b.Position.X, y = b.Position.Y, z = b.Position.Z, rx = 0f, ry = b.RotationY, rz = 0f, piece = true }));
			return Results.Ok(objects);
		});

		// Brings objects of the backup back: each becomes a new object with all of its backup data.
		app.MapPost("/api/backup/restore", (BackupRestore req) =>
		{
			Open? o = _open;
			if (o == null)
			{
				return Results.BadRequest("Open a backup first.");
			}
			List<NewObject> list = new();
			foreach (var (backupId, newId) in req.Pairs.Select(p => (p.BackupId, p.NewId)))
			{
				if (newId >= 0 || backupId < 0 || backupId >= o.World.ObjectRefs.Count)
				{
					continue;
				}
				byte[] raw = o.World.ObjectBytes(backupId);
				ZdoData z = ZdoData.Parse(raw);
				list.Add(new NewObject(newId, z.Prefab, z.Position, z.Rotation, 0f, null, false, raw));
			}
			edits.AddObjects(list);
			return Results.Ok(new { restored = list.Count, pending = pending() });
		});
	}
}

public sealed record BackupOpen(string? Path);

public sealed record RestorePair(int BackupId, int NewId);

public sealed record BackupRestore(List<RestorePair> Pairs);
