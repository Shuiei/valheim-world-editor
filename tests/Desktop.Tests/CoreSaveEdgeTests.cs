using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// The world writer's edge cases: what it refuses (and then leaves every file alone), ground in a zone
// the game never edited, a failure halfway (the backup comes back byte for byte), several saves in a
// row, and everything at once.
public class SaveEdgeTests
{
	// Every file of the folder, by name.
	private static Dictionary<string, byte[]> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

	private static void AssertSame(Dictionary<string, byte[]> before, string dir)
	{
		var after = Files(dir);
		Assert.Equal(before.Keys.Order(), after.Keys.Order());
		foreach (var (name, bytes) in before)
		{
			Assert.True(bytes.AsSpan().SequenceEqual(after[name]), $"{name} changed");
		}
	}

	private static int Backups(string worldDir) => Directory.GetDirectories(Path.GetDirectoryName(worldDir)!).Length;

	// A zone that has world data (a chunk file) but no ground edits yet.
	private static (int X, int Z) FreshZone(WorldSave world)
	{
		var t = world.TerrainZones[0];
		for (int r = 1; r < 6; r++)
		{
			for (int dz = -r; dz <= r; dz++)
			{
				for (int dx = -r; dx <= r; dx++)
				{
					int x = t.ZoneX + dx, z = t.ZoneZ + dz;
					if (ChunkMath.Find(world.Chunks, x, z) != null && world.TerrainZones.All(q => q.ZoneX != x || q.ZoneZ != z))
					{
						return (x, z);
					}
				}
			}
		}
		throw new InvalidOperationException("no fresh zone near the edited one");
	}

	private static ZoneEdit Raised(int zx, int zz, float by, params int[] points)
	{
		var e = new ZoneEdit(zx, zz);
		foreach (int p in points)
		{
			e.Modified[p] = true;
			e.Level[p] = by;
		}
		return e;
	}

	[Fact]
	public void NothingToSaveTouchesNothing()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var before = Files(w.Dir);
		int backups = Backups(w.Dir);
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>());
		Assert.False(r.Saved);
		Assert.Equal("Nothing to save.", r.Message);
		Assert.Null(r.BackupDirectory);
		Assert.Equal(backups, Backups(w.Dir));
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void AnEmptyEditOfAZoneWithoutGroundIsNotSaved()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var (x, z) = FreshZone(world);
		var before = Files(w.Dir);
		var r = WorldWriter.Save(world, new[] { new ZoneEdit(x, z) });
		Assert.False(r.Saved);
		Assert.Equal("Nothing could be saved.", r.Message);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void GroundInAZoneTheGameNeverEditedIsCreated()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var (x, z) = FreshZone(world);
		var edit = Raised(x, z, 2.5f, 100, 101, 2000);
		edit.PaintModified[300] = true;
		edit.Paint[300 * 4] = 1f;
		var r = WorldWriter.Save(world, new[] { edit });
		Assert.True(r.Saved, r.Message);
		Assert.Equal(0, r.ZonesWritten);
		Assert.Equal(1, r.ZonesCreated);
		var again = WorldSave.Load(w.Dir);
		Assert.Equal(world.ObjectCount + 1, again.ObjectCount);
		var t = again.TerrainZones.Single(q => q.ZoneX == x && q.ZoneZ == z);
		Assert.True(t.ModifiedHeight[100] && t.ModifiedHeight[2000]);
		Assert.Equal(2.5f, t.LevelDelta[2000], 4);
		Assert.False(t.ModifiedHeight[102]);
		Assert.True(t.ModifiedPaint[300]);
		Assert.Equal(1f, t.Paint[300].X, 3);
		// The zone that had ground before is as it was.
		var old = world.TerrainZones[0];
		Assert.Equal(old.LevelDelta, again.TerrainZones.Single(q => q.ZoneX == old.ZoneX && q.ZoneZ == old.ZoneZ).LevelDelta);
	}

	[Fact]
	public void NewObjectsThatCannotBeSavedAreSkippedWithAReason()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var before = Files(w.Dir);
		var tree = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
		var unknown = new NewObject(-1, Fixtures.Hash("NoSuchThing_xyz"), tree.Position, Vector3.Zero, 1f);
		var far = new NewObject(-2, Fixtures.Hash("Beech1"), new Vector3(9000, 0, 9000), Vector3.Zero, 1f);
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: new[] { unknown, far });
		Assert.False(r.Saved);
		Assert.Contains(r.Skipped, s => s.Contains("unknown kind of object"));
		Assert.Contains(r.Skipped, s => s.Contains("is not generated yet"));
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void DeletingAnIdThatDoesNotExistIsIgnored()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var before = Files(w.Dir);
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), deleted: new[] { -5, world.ObjectRefs.Count + 10 });
		Assert.False(r.Saved);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void AnotherSaveFormatIsRefusedUntouched()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var before = Files(w.Dir);
		world.Chunks[0].WorldVersion = 40;
		var t = world.TerrainZones[0];
		var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, 1, 500) });
		Assert.False(r.Saved);
		Assert.Contains("save format 40", r.Message);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void AFailureHalfwayRestoresTheWorldByteForByte()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		// The world's metadata file disappears after loading: copying it fails once the chunks are written.
		File.Delete(Path.Combine(w.Dir, $"_main.{world.SaveNumber}.fwl2"));
		var before = Files(w.Dir);
		var t = world.TerrainZones[0];
		var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, 1, 500) });
		Assert.False(r.Saved);
		Assert.StartsWith("Saving failed", r.Message);
		Assert.Contains("Nothing changed", r.Message);
		Assert.Null(r.BackupDirectory);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void SavesInARowLeaveOnlyTheLatestFiles()
	{
		using var w = new TempWorld();
		var t = w.Load().TerrainZones[0];
		for (int n = 0; n < 3; n++)
		{
			WorldSave world = w.Load();
			var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, n + 1, 500 + n) });
			Assert.True(r.Saved, r.Message);
			var again = WorldSave.Load(w.Dir);
			Assert.Equal(world.SaveNumber + 1, again.SaveNumber);
			// Only the new save number's files, and each chunk file once (the replaced version is gone).
			var names = Directory.GetFiles(w.Dir).Select(Path.GetFileName).ToList();
			Assert.All(names.Where(f => f!.StartsWith("_main.")), f => Assert.StartsWith($"_main.{again.SaveNumber}.", f));
			Assert.Equal(4, names.Count(f => f!.StartsWith("_main.")));
			var chunks = names.Where(f => f!.EndsWith(".chunk")).Select(f => f![..f!.LastIndexOf('_')]).ToList();
			Assert.Equal(chunks.Distinct().Count(), chunks.Count);
			Assert.Equal(n + 1f, again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500 + n], 4);
		}
	}

	// An open world saving again and again (like Apply live): each save is made from the save it was
	// opened from, which stays; each earlier save of the session goes; leaving prunes down to the latest.
	[Fact]
	public void AnOpenWorldSavesFromItsBaseAndPrunesWhenLeft()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		int b = world.SaveNumber;
		var t = world.TerrainZones[0];
		IReadOnlyList<string>? last = null;
		for (int n = 0; n < 3; n++)
		{
			var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, n + 1, 500 + n) }, options: new WorldWriter.Options(KeepBase: true, Drop: last));
			Assert.True(r.Saved, r.Message);
			last = r.Files;
			var saves = Directory.GetFiles(w.Dir, "_main.*.ok").Select(f => int.Parse(Path.GetFileName(f).Split('.')[1])).Order().ToList();
			// The base and this save only.
			Assert.Equal(new[] { b, b + 1 + n }, saves);
			var again = WorldSave.Load(w.Dir);
			// Made from the base: only this save's raise, not the earlier ones.
			var zone = again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ);
			Assert.Equal(n + 1f, zone.LevelDelta[500 + n], 4);
			if (n > 0)
			{
				Assert.NotEqual(n + 0f, zone.LevelDelta[500 + n - 1], 4);
			}
		}
		Assert.Equal(b, WorldSave.Load(w.Dir).SaveNumber - 3);
		WorldWriter.Prune(w.Dir);
		var names = Directory.GetFiles(w.Dir).Select(Path.GetFileName).ToList();
		Assert.Equal(4, names.Count(f => f!.StartsWith("_main.", StringComparison.Ordinal)));
		Assert.All(names.Where(f => f!.StartsWith("_main.", StringComparison.Ordinal)), f => Assert.StartsWith($"_main.{b + 3}.", f));
		var after = WorldSave.Load(w.Dir);
		Assert.Equal(after.Chunks.Count, names.Count(f => f!.EndsWith(".chunk", StringComparison.Ordinal)));
	}

	[Fact]
	public void AGroundResetDropsThatZonesGroundEdits()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var t = world.TerrainZones[0];
		var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, 3, 500) }, resets: new[] { new ZoneReset(t.ZoneX, t.ZoneZ, true, true) });
		Assert.True(r.Saved, r.Message);
		Assert.Equal(0, r.ZonesWritten);
		var again = WorldSave.Load(w.Dir);
		Assert.DoesNotContain(again.TerrainZones, q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ);
		Assert.DoesNotContain(((short)t.ZoneX, (short)t.ZoneZ), again.Zones!.Generated);
	}

	[Fact]
	public void AResetThatKeepsTheGroundKeepsItsEdits()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var t = world.TerrainZones[0];
		var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, 3, 500) }, resets: new[] { new ZoneReset(t.ZoneX, t.ZoneZ, true, false) });
		Assert.True(r.Saved, r.Message);
		Assert.Equal(1, r.ZonesWritten);
		var again = WorldSave.Load(w.Dir);
		Assert.Equal(3f, again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500], 4);
	}

	[Fact]
	public void ZonesCannotBeResetWithoutTheWorldsZoneList()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		typeof(WorldSave).GetProperty(nameof(WorldSave.Zones))!.SetValue(world, null);
		var before = Files(w.Dir);
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), resets: new[] { new ZoneReset(0, 0, true, false) });
		Assert.False(r.Saved);
		Assert.Contains(".db2", r.Message);
		AssertSame(before, w.Dir);
	}

	[Fact]
	public void GroundObjectsAndResetsInOneSave()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var t = world.TerrainZones[0];
		var (fx, fz) = FreshZone(world);
		var trees = world.Objects.Where(o => o.Prefab == Fixtures.Hash("Beech1")).Take(2).ToList();
		var planted = new NewObject(-1, Fixtures.Hash("Beech1"), trees[0].Position + new Vector3(2, 0, 2), new Vector3(0, 45, 0), 1.2f);
		// A reset far from the rest, in a generated zone without buildings.
		var resetZone = world.Zones!.Generated.Select(g => ((int)g.X, (int)g.Z)).First(g => Math.Abs(g.Item1 - t.ZoneX) > 3 && world.ObjectRefs.All(o => o.Zone != g || !o.IsPiece));
		int inReset = world.ObjectRefs.Count(o => o.Zone == resetZone && !o.IsTerrain && o.Prefab != WorldWriter.TombstonePrefab);
		var r = WorldWriter.Save(world, new[] { Raised(t.ZoneX, t.ZoneZ, 1, 600), Raised(fx, fz, -2, 700) }, new[] { trees[1].Id }, new[] { planted },
			new[] { new ZoneReset(resetZone.Item1, resetZone.Item2, true, false) });
		Assert.True(r.Saved, r.Message);
		Assert.Equal(1, r.ZonesWritten);
		Assert.Equal(1, r.ZonesCreated);
		Assert.Equal(1, r.ObjectsAdded);
		Assert.Equal(1 + inReset, r.ObjectsDeleted);
		var again = WorldSave.Load(w.Dir);
		Assert.Equal(world.ObjectCount + 1 + 1 - 1 - inReset, again.ObjectCount);
		Assert.Contains(again.Objects, o => o.Prefab == planted.Prefab && Vector3.Distance(o.Position, planted.Position) < 0.01f);
		Assert.DoesNotContain(again.Objects, o => Vector3.Distance(o.Position, trees[1].Position) < 0.001f && o.Prefab == trees[1].Prefab);
		Assert.Equal(-2f, again.TerrainZones.Single(q => q.ZoneX == fx && q.ZoneZ == fz).LevelDelta[700], 4);
	}
}
