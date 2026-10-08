using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// Objects built from another one (ZdoBuilder): a move keeps everything the object holds, a fresh copy
// keeps only its builder and scale; turning, tilting and scaling are written the game's way.
// Opening worlds sets the builder for new pieces (WorldSave.Builder): one world at a time.
[Collection("World files")]
public class CoreCopyTests
{
	// The test world's saved objects carry little data: a chest holding one of every kind of value the
	// game stores is saved into it first (through the editor's own writer), then used as the source.
	private static (WorldSave World, int Id, ZdoData Data, byte[] Source) Richest(TempWorld w)
	{
		WorldSave world = w.Load();
		int chestId = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
		var z = ZdoData.Parse(world.ObjectBytes(chestId));
		var inv = new InventoryData();
		inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 42 });
		z.SetBytes(StableHash.Of("items"), inv.Write());
		z.Set("strings", StableHash.Of("text"), "Hello there");
		z.Set("strings", StableHash.Of("tag"), "portal home");
		z.Set("ints", StableHash.Of("health_level"), "3");
		z.Set("longs", StableHash.Of("creator"), "123456789");
		z.Set("longs", StableHash.Of("spawntime"), "638000000000000000");
		z.Set("floats", StableHash.Of("health"), "750.5");
		z.Set("vec3", StableHash.Of("spawnpoint"), "1 2 3");
		z.Set("quats", StableHash.Of("look"), "0 0.7071 0 0.7071");
		z.Connection = new byte[] { 1, 0x78, 0x56, 0x34, 0x12 };
		var rich = new NewObject(-1, z.Prefab, z.Position + new Vector3(4, 0, 4), Vector3.Zero, 0f, Fresh: false, Raw: z.Serialize());
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: new[] { rich });
		Assert.True(r.Saved, r.Message);
		world = w.Load();
		int id = Enumerable.Range(0, world.ObjectRefs.Count).Single(i => ZdoData.Parse(world.ObjectBytes(i)).StringList.Any(s => s.Value == "Hello there"));
		var data = ZdoData.Parse(world.ObjectBytes(id));
		Assert.Equal(42, InventoryData.Read(data.GetBytes(StableHash.Of("items"))!).Items.Single().Stack);
		var o = world.ObjectRefs[id];
		return (world, id, data, File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName)));
	}

	private static void SameData(ZdoData a, ZdoData b)
	{
		Assert.Equal(a.FloatList, b.FloatList);
		Assert.Equal(a.QuatList, b.QuatList);
		Assert.Equal(a.IntList, b.IntList);
		Assert.Equal(a.LongList, b.LongList);
		Assert.Equal(a.StringList, b.StringList);
		Assert.Equal(a.ByteList.Select(i => (i.Key, Convert.ToBase64String(i.Value))), b.ByteList.Select(i => (i.Key, Convert.ToBase64String(i.Value))));
		Assert.Equal(a.Connection, b.Connection);
	}

	[Fact]
	public void AMoveKeepsEverythingTheObjectHolds()
	{
		using var w = new TempWorld();
		var (world, id, data, src) = Richest(w);
		var o = world.ObjectRefs[id];
		byte[] moved = ZdoBuilder.Build(src, o, o.File.WorldVersion, new Vector3(12.25f, 33.5f, -7), new Vector3(0, 90, 0), 0f);
		var z = ZdoData.Parse(moved);
		Assert.Equal(new Vector3(12.25f, 33.5f, -7), z.Position);
		Assert.Equal(90f, z.Rotation.Y, 1);
		Assert.Equal(o.Prefab, z.Prefab);
		SameData(data, z);
		Assert.Equal(data.Vec3List, z.Vec3List);
	}

	[Fact]
	public void AFreshCopyKeepsOnlyTheBuilderAndScale()
	{
		using var w = new TempWorld();
		var (world, id, data, src) = Richest(w);
		var o = world.ObjectRefs[id];
		var z = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 0f, fresh: true));
		int creator = StableHash.Of("creator"), scale = StableHash.Of("scale"), scalar = StableHash.Of("scaleScalar");
		Assert.All(z.LongList, i => Assert.Equal(creator, i.Key));
		Assert.All(z.Vec3List, i => Assert.Equal(scale, i.Key));
		Assert.All(z.FloatList, i => Assert.Equal(scalar, i.Key));
		Assert.Empty(z.IntList);
		Assert.Empty(z.StringList);
		Assert.Empty(z.ByteList);
		Assert.Empty(z.QuatList);
		Assert.Null(z.Connection);
		Assert.Equal(data.LongList.Where(i => i.Key == creator), z.LongList);
	}

	[Theory]
	[InlineData(0f, 45f, 0f)]
	[InlineData(10f, 200f, -15f)]
	[InlineData(-90f, 0f, 30f)]
	public void TurnsAndTiltsAreWrittenTheGamesWay(float x, float y, float z)
	{
		using var w = new TempWorld();
		var (world, id, _, src) = Richest(w);
		var o = world.ObjectRefs[id];
		var back = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.Zero, new Vector3(x, y, z), 0f));
		// The game keeps whole and half degrees, between 0 and 360.
		static float Norm(float a) => ((a % 360) + 360) % 360;
		Assert.Equal(Norm(x), Norm(back.Rotation.X), 0);
		Assert.Equal(Norm(y), Norm(back.Rotation.Y), 0);
		Assert.Equal(Norm(z), Norm(back.Rotation.Z), 0);
	}

	[Fact]
	public void NoTurnWritesNoRotation()
	{
		using var w = new TempWorld();
		var (world, id, _, src) = Richest(w);
		var o = world.ObjectRefs[id];
		byte[] bytes = ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.Zero, new Vector3(0, 360, 0), 0f);
		Assert.Equal(0, BitConverter.ToUInt16(bytes, 0) & 0x1000);
		Assert.Equal(Vector3.Zero, ZdoData.Parse(bytes).Rotation);
	}

	[Fact]
	public void AScaleReplacesTheOldOne()
	{
		using var w = new TempWorld();
		var (world, id, _, src) = Richest(w);
		var o = world.ObjectRefs[id];
		var z = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.Zero, Vector3.Zero, 1.5f));
		Assert.Equal(new Vector3(1.5f), z.Vec3List.Single(i => i.Key == StableHash.Of("scale")).Value);
		Assert.DoesNotContain(z.FloatList, i => i.Key == StableHash.Of("scaleScalar"));
	}

	[Fact]
	public void AKindTheWorldHasNoExampleOfIsBuiltBlank()
	{
		var bytes = ZdoBuilder.Blank(StableHash.Of("Rock_7"), 0x0100, new Vector3(5, 6, 7), new Vector3(0, 30, 0), 2f);
		var z = ZdoData.Parse(bytes);
		Assert.Equal(new Vector3(5, 6, 7), z.Position);
		Assert.Equal(30f, z.Rotation.Y, 1);
		Assert.Equal(new Vector3(2f), z.Vec3List.Single().Value);
		// Scale 1 is the prefab's own: nothing written.
		Assert.Empty(ZdoData.Parse(ZdoBuilder.Blank(StableHash.Of("Rock_7"), 0x0100, Vector3.Zero, Vector3.Zero, 1f)).Vec3List);
	}

	// What the editor hands to the writer: a moved object (Raw: its own bytes) keeps its data and
	// takes the new place; a placed piece without data is the chosen builder's.
	[Fact]
	public void MovedAndPlacedObjectsAreSavedTheirWay()
	{
		using var w = new TempWorld();
		var (world, id, data, _) = Richest(w);
		var moved = new NewObject(-1, world.ObjectRefs[id].Prefab, new Vector3(1, 2, 3), new Vector3(0, 10, 0), 0f, SourceId: id, Fresh: false, Raw: world.ObjectBytes(id));
		var z = ZdoData.Parse(world.NewObjectBytes(moved, o => File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName)))!);
		Assert.Equal(new Vector3(1, 2, 3), z.Position);
		SameData(data, z);
		long saved = WorldSave.Builder;
		try
		{
			WorldSave.Builder = 4242;
			var wall = new NewObject(-2, StableHash.Of("woodwall"), new Vector3(1, 2, 3), Vector3.Zero, 0f);
			var built = ZdoData.Parse(world.NewObjectBytes(wall, o => File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName)))!);
			Assert.Equal(4242L, built.LongList.Single(i => i.Key == StableHash.Of("creator")).Value);
			// A moved piece keeps its own builder.
			Assert.Equal(data.LongList.Where(i => i.Key == StableHash.Of("creator")), z.LongList.Where(i => i.Key == StableHash.Of("creator")));
		}
		finally
		{
			WorldSave.Builder = saved;
		}
	}

	[Fact]
	public void AnObjectHoldingEveryKindOfValueReadsAndWritesBackExactly()
	{
		using var w = new TempWorld();
		var (world, id, _, _) = Richest(w);
		byte[] bytes = world.ObjectBytes(id);
		Assert.True(bytes.AsSpan().SequenceEqual(ZdoData.Parse(bytes).Serialize()));
	}
}
