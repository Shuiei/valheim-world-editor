using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// The data inside a save at its edges: the zone list's keys, locations and trailing bytes; an
// object's values (two-byte counts, the old pre-chunk format, setting and removing values); the edit
// store with zones it cannot use; and objects copied from templates holding only some kinds of data.
public class CoreSaveDataTests
{
	// A zone list as the live bridge sends it: two generated zones, two global keys, three locations.
	private static ZoneDb Package()
	{
		using var ms = new MemoryStream();
		using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			w.Write(2);
			w.Write((short)1); w.Write((short)2);
			w.Write((short)-3); w.Write((short)4);
			w.Write(7);
			w.Write(2);
			w.Write("defeated_eikthyr");
			w.Write("nomap");
			w.Write(true);
			w.Write(3);
			foreach (var (hash, x, z, placed) in new[] { (11, 64f, 128f, true), (12, 70f, 130f, false), (13, -190f, 260f, true) })
			{
				w.Write(hash);
				w.Write(x); w.Write(30f); w.Write(z);
				w.Write(placed);
			}
		}
		return ZoneDb.FromPackage(ms.ToArray(), 1234.5);
	}

	[Fact]
	public void TheZoneListKeepsItsKeysAndLocationsThroughASave()
	{
		var db = Package();
		Assert.Equal(new[] { "defeated_eikthyr", "nomap" }, db.GlobalKeys);
		Assert.True(db.LocationsGenerated);
		Assert.Equal(7, db.LocationVersion);
		string path = Path.Combine(Path.GetTempPath(), "vwe-db-" + Guid.NewGuid().ToString("N")[..8] + ".db2");
		try
		{
			db.Save(path);
			var again = ZoneDb.Load(path);
			Assert.Equal(db.Generated, again.Generated);
			Assert.Equal(db.GlobalKeys, again.GlobalKeys);
			Assert.Equal(db.Locations, again.Locations);
			Assert.Equal(1234.5, again.NetTime);
			Assert.Equal(41, again.FileVersion);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void AResetZoneUnplacesOnlyItsOwnLocations()
	{
		var db = Package();
		// Zone 1, 2 holds the first two locations (x 64-70, z 128-130); the third is elsewhere.
		db.ResetZone(1, 2);
		Assert.DoesNotContain(((short)1, (short)2), db.Generated);
		Assert.Contains(((short)-3, (short)4), db.Generated);
		Assert.False(db.Locations[0].Placed);
		Assert.False(db.Locations[1].Placed);
		Assert.True(db.Locations[2].Placed);
	}

	[Fact]
	public void ManyValuesAreCountedInTwoBytes()
	{
		var z = new ZdoData { Prefab = 123, Position = new Vector3(1.5f, 2, 3) };
		for (int i = 0; i < 200; i++)
		{
			z.IntList.Add((i, i * 3));
		}
		var back = ZdoData.Parse(z.Serialize());
		Assert.Equal(200, back.IntList.Count);
		Assert.Equal((199, 597), back.IntList[^1]);
	}

	[Fact]
	public void ValuesAreSetReplacedAndRemoved()
	{
		var z = new ZdoData();
		int k = StableHash.Of("text");
		z.Set("strings", k, "one");
		z.Set("strings", k, "two");
		Assert.Equal((k, "two"), Assert.Single(z.StringList));
		z.Set("strings", k, null);
		Assert.Empty(z.StringList);
		// Removing what is not there is fine.
		z.Set("ints", k, null);
		z.Set("bytes", k, Convert.ToBase64String(new byte[] { 1, 2, 3 }));
		Assert.Equal(new byte[] { 1, 2, 3 }, z.GetBytes(k));
		z.Set("quats", k, "0 0 0 1");
		Assert.Equal(Quaternion.Identity, z.QuatList.Single().Value);
		Assert.Throws<ArgumentException>(() => z.Set("colours", k, "1"));
		Assert.Throws<FormatException>(() => z.Set("vec3", k, "1 2"));
	}

	[Fact]
	public void AnObjectOfAnOldSaveIsRead()
	{
		// Before chunked saves (version 40): a sector before the position, and a full rotation.
		using var ms = new MemoryStream();
		using (var w = new BinaryWriter(ms))
		{
			w.Write((ushort)(0x1000 | 0x10));
			w.Write((short)3); w.Write((short)-2);
			w.Write(10f); w.Write(31f); w.Write(-20f);
			w.Write(777);
			w.Write(0f); w.Write(45f); w.Write(0f);
			w.Write((byte)1);
			w.Write(5); w.Write(42);
		}
		var z = ZdoData.Parse(ms.ToArray(), 39);
		Assert.Equal(new Vector3(10, 31, -20), z.Position);
		Assert.Equal(777, z.Prefab);
		Assert.Equal(new Vector3(0, 45, 0), z.Rotation);
		Assert.Equal((5, 42), Assert.Single(z.IntList));
	}

	[Fact]
	public void TheStoreLeavesOutSavedZonesOfAnotherSize()
	{
		var world = new WorldSave { Directory = "none", SaveNumber = 1 };
		world.TerrainZones.Add(new TerrainZone
		{
			Center = new Vector3(64, 0, 0),
			ModifiedHeight = new bool[33 * 33], LevelDelta = new float[33 * 33], SmoothDelta = new float[33 * 33], ModifiedPaint = new bool[33 * 33], Paint = new Vector4[33 * 33],
		});
		var store = new EditStore(world);
		Assert.Null(store.Get(1, 0));
		Assert.Empty(store.All());
	}

	[Fact]
	public void ZonesMarkedUnchangedAreNoLongerPending()
	{
		var store = new EditStore(new WorldSave { Directory = "none", SaveNumber = 1 });
		var e = new ZoneEdit(2, 3);
		e.Modified[10] = true;
		e.Level[10] = 1;
		store.Put(e);
		Assert.Equal(1, store.ChangedZoneCount);
		int v = store.Version;
		store.MarkUnchanged(new[] { (2, 3), (9, 9) });
		Assert.Equal(0, store.ChangedZoneCount);
		Assert.True(store.Version > v);
	}

	[Fact]
	public void PaintMakesGroundDifferent()
	{
		var a = new ZoneEdit(0, 0);
		var b = a.Clone();
		Assert.True(a.SameGround(b));
		a.PaintModified[5] = b.PaintModified[5] = true;
		a.Paint[5 * 4] = 1;
		Assert.False(a.SameGround(b));
		b.Paint[5 * 4] = 1;
		Assert.True(a.SameGround(b));
		b.PaintModified[6] = true;
		Assert.False(a.SameGround(b));
		// Height split between level and smoothing: the same ground.
		a.Modified[7] = b.Modified[7] = true;
		a.Level[7] = 2;
		b.Level[7] = 1.5f;
		b.Smooth[7] = 0.5f;
		b.PaintModified[6] = false;
		Assert.True(a.SameGround(b));
	}

	[Fact]
	public void ZoneDataOfTheWrongSizeIsRefused()
	{
		var e = new ZoneEdit(0, 0) { Level = new float[10] };
		Assert.Throws<ArgumentException>(e.Sanitize);
	}

	// A chest holding only text and its builder, saved into a copy of the test world.
	private static (WorldSave World, int Id) TextChest(TempWorld w, string scale)
	{
		var world = w.Load();
		int chest = world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id;
		var z = ZdoData.Parse(world.ObjectBytes(chest));
		z.Set("strings", StableHash.Of("text"), "only text");
		z.Set("longs", StableHash.Of("creator"), "99");
		if (scale != "")
		{
			z.Set("vec3", StableHash.Of("scale"), scale);
		}
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: new[] { new NewObject(-1, z.Prefab, z.Position + new Vector3(4, 0, 4), Vector3.Zero, 0f, Fresh: false, Raw: z.Serialize()) });
		Assert.True(r.Saved, r.Message);
		world = w.Load();
		return (world, Enumerable.Range(0, world.ObjectRefs.Count).Single(i => ZdoData.Parse(world.ObjectBytes(i)).StringList.Any(s => s.Value == "only text")));
	}

	[Fact]
	public void ACopyOfAnObjectWithSomeDataKeepsJustThat()
	{
		using var w = new TempWorld();
		var (world, id) = TextChest(w, "");
		var o = world.ObjectRefs[id];
		byte[] src = File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName));
		var moved = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 0f));
		Assert.Equal("only text", moved.StringList.Single().Value);
		Assert.Equal(99, moved.LongList.Single().Value);
		Assert.Empty(moved.IntList);
		Assert.Empty(moved.FloatList);
		// A scale adds the vector section the chest did not have.
		var scaled = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 2f));
		Assert.Equal(new Vector3(2f), scaled.Vec3List.Single().Value);
	}

	[Fact]
	public void ANewScaleReplacesTheOneTheObjectHad()
	{
		using var w = new TempWorld();
		var (world, id) = TextChest(w, "3 3 3");
		var o = world.ObjectRefs[id];
		byte[] src = File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName));
		var keep = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 0f));
		Assert.Equal(new Vector3(3f), keep.Vec3List.Single().Value);
		var scaled = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 1.25f));
		Assert.Equal(new Vector3(1.25f), Assert.Single(scaled.Vec3List).Value);
	}
}
