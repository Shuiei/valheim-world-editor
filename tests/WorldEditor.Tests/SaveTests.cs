using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// The save reader and writer on the test world: what is saved must read back exactly, and nothing
// else may change.
public class SaveTests
{
	[Fact]
	public void LoadsTheTestWorld()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		Assert.Equal("CITest", world.Name);
		Assert.Equal("GbXfypqU3G", world.SeedName);
		Assert.Equal(2, world.SaveNumber);
		Assert.Equal(122, world.ObjectCount);
		Assert.Single(world.TerrainZones);
		Assert.True(world.Templates.ContainsKey(Fixtures.Hash("Beech1")));
		Assert.Contains(world.Objects, o => o.Prefab == Fixtures.Hash("piece_chest_wood"));
	}

	[Fact]
	public void TerrainEditsRoundTrip()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		TerrainZone zone = world.TerrainZones[0];
		ZoneEdit edit = edits.Get(zone.ZoneX, zone.ZoneZ)!.Clone();
		// Raise a block of points by 1.5 m and paint one paved.
		var cells = Enumerable.Range(0, EditStore.Cells).Where(i => i % 65 > 20 && i % 65 < 30 && i / 65 > 20 && i / 65 < 30).ToList();
		foreach (int i in cells)
		{
			edit.Modified[i] = true;
			edit.Level[i] += 1.5f;
		}
		edit.PaintModified[cells[0]] = true;
		edit.Paint[cells[0] * 4 + 2] = 1f;
		edits.Put(edit);
		WorldWriter.Result r = WorldWriter.Save(world, edits.All().Where(e => e.Changed).ToList());
		Assert.True(r.Saved, r.Message);
		Assert.Equal(1, r.ZonesWritten);
		Assert.True(Directory.Exists(r.BackupDirectory), "a backup of the world is made first");

		WorldSave again = WorldSave.Load(w.Dir);
		Assert.Equal(3, again.SaveNumber);
		Assert.Equal(world.ObjectCount, again.ObjectCount);
		TerrainZone z2 = again.TerrainZones.Single(t => t.ZoneX == zone.ZoneX && t.ZoneZ == zone.ZoneZ);
		foreach (int i in cells)
		{
			Assert.True(z2.ModifiedHeight[i]);
			Assert.Equal(edit.Level[i], z2.LevelDelta[i], 3);
		}
		Assert.Equal(1f, z2.Paint[cells[0]].Z, 2);
	}

	[Fact]
	public void GroundIsClampedToTheGameLimit()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		TerrainZone zone = world.TerrainZones[0];
		ZoneEdit edit = edits.Get(zone.ZoneX, zone.ZoneZ)!.Clone();
		edit.Modified[100] = true;
		edit.Level[100] = 50f;
		edits.Put(edit);
		Assert.InRange(edits.Get(zone.ZoneX, zone.ZoneZ)!.Level[100], -8f, 8f);
	}

	[Fact]
	public void ZonesThatAreNotGeneratedAreSkipped()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		var edit = new ZoneEdit(60, 60);
		edit.Modified[5] = true;
		edit.Level[5] = 1f;
		edits.Put(edit);
		WorldWriter.Result r = WorldWriter.Save(world, edits.All().Where(e => e.Changed).ToList());
		Assert.False(r.Saved);
		Assert.Contains(r.Skipped, s => s.Contains("not generated"));
		Assert.Equal(2, WorldSave.Load(w.Dir).SaveNumber);
	}

	[Fact]
	public void DeletedObjectsAreGone()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var tree = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
		WorldWriter.Result r = WorldWriter.Save(world, new List<ZoneEdit>(), deleted: new[] { tree.Id });
		Assert.True(r.Saved, r.Message);
		WorldSave again = WorldSave.Load(w.Dir);
		Assert.Equal(world.ObjectCount - 1, again.ObjectCount);
		Assert.DoesNotContain(again.Objects, o => o.Prefab == tree.Prefab && Vector3.Distance(o.Position, tree.Position) < 0.01f);
	}

	[Fact]
	public void NewObjectsAreSavedWhereTheyWerePlaced()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		Vector3 near = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1")).Position;
		var copy = new NewObject(-1, Fixtures.Hash("Beech1"), near + new Vector3(2, 0, 2), new Vector3(0, 90, 0), 1.2f);
		// A kind the world has none of: built blank from the prefab catalogue.
		var blank = new NewObject(-2, Fixtures.Hash("sapling_turnip"), near + new Vector3(-2, 0, 2), Vector3.Zero, 0f);
		Assert.False(world.Templates.ContainsKey(blank.Prefab));
		Assert.True(world.CanCreate(blank.Prefab));
		WorldWriter.Result r = WorldWriter.Save(world, new List<ZoneEdit>(), added: new[] { copy, blank });
		Assert.True(r.Saved, r.Message);
		Assert.Equal(2, r.ObjectsAdded);

		WorldSave again = WorldSave.Load(w.Dir);
		Assert.Equal(world.ObjectCount + 2, again.ObjectCount);
		var c = again.Objects.Single(o => o.Prefab == copy.Prefab && Vector3.Distance(o.Position, copy.Position) < 0.01f);
		Assert.Equal(90f, c.Rotation.Y, 0);
		Assert.Equal(1.2f, c.Scale.X, 2);
		Assert.Contains(again.Objects, o => o.Prefab == blank.Prefab && Vector3.Distance(o.Position, blank.Position) < 0.01f);
	}

	[Fact]
	public void CopiesAreFreshButMovesKeepTheirData()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
		ObjectRef source = world.ObjectRefs[chest];
		byte[] src = File.ReadAllBytes(Path.Combine(world.Directory, source.File.FileName));
		byte[] moved = ZdoBuilder.Build(src, source, source.File.WorldVersion, new Vector3(1, 50, 1), Vector3.Zero, 0f, fresh: false);
		byte[] fresh = ZdoBuilder.Build(src, source, source.File.WorldVersion, new Vector3(1, 50, 1), Vector3.Zero, 0f, fresh: true);
		byte[] original = src[(int)source.DataStart..(int)source.End];
		Assert.True(moved.Length - DataStart(moved) >= original.Length, "a move keeps all of the object's data");
		Assert.True(fresh.Length <= moved.Length, "a copy keeps no more than the builder and scale");
	}

	[Fact]
	public void ZoneResetRemovesGeneratedObjects()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var tree = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
		int zx = (int)MathF.Floor((tree.Position.X + 32f) / 64f), zz = (int)MathF.Floor((tree.Position.Z + 32f) / 64f);
		int inZone = world.Objects.Count(o => (int)MathF.Floor((o.Position.X + 32f) / 64f) == zx && (int)MathF.Floor((o.Position.Z + 32f) / 64f) == zz);
		Assert.True(inZone > 0);
		WorldWriter.Result r = WorldWriter.Save(world, new List<ZoneEdit>(), resets: new[] { new ZoneReset(zx, zz, KeepBuildings: true, Ground: true) });
		Assert.True(r.Saved, r.Message);
		WorldSave again = WorldSave.Load(w.Dir);
		Assert.DoesNotContain(again.Objects, o => (int)MathF.Floor((o.Position.X + 32f) / 64f) == zx && (int)MathF.Floor((o.Position.Z + 32f) / 64f) == zz && o.Prefab == tree.Prefab);
	}

	// Bytes before the data sections: flags (2), position (12), prefab (4), rotation (0, 2 or 4).
	private static int DataStart(byte[] zdo)
	{
		ushort flags = BitConverter.ToUInt16(zdo, 0);
		int p = 2 + ((flags & 0x2000) != 0 ? 4 : 12) + 4;
		if ((flags & 0x1000) != 0)
		{
			p += (BitConverter.ToUInt16(zdo, p) & 0x8000) != 0 ? 2 : 4;
		}
		return p;
	}
}
