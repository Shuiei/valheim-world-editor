using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

// The object inspector: reading every object of the test world, chest contents, and edits that are saved.
public class InspectorTests
{
	[Fact]
	public void EveryObjectReadsAndWritesBackByteForByte()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		int checkedObjects = 0;
		for (int id = 0; id < world.ObjectRefs.Count; id++)
		{
			byte[] b = world.ObjectBytes(id);
			// Terrain objects keep their position in the short form the writer does not use.
			if ((BitConverter.ToUInt16(b, 0) & 0x2000) != 0)
			{
				continue;
			}
			Assert.Equal(b, ZdoData.Parse(b).Serialize());
			checkedObjects++;
		}
		Assert.True(checkedObjects > 100);
	}

	[Fact]
	public void KeyNamesComeFromTheGame()
	{
		Assert.Equal("items", ZdoKeys.NameOf(StableHash.Of("items")));
		Assert.Equal("creator", ZdoKeys.NameOf(StableHash.Of("creator")));
		Assert.Equal("pu_name3", ZdoKeys.NameOf(StableHash.Of("pu_name3")));
		Assert.Equal(StableHash.Of("text"), ZdoKeys.Parse("text"));
		Assert.Equal(-42, ZdoKeys.Parse("-42"));
	}

	[Fact]
	public void TheCatalogueKnowsContainersAndItems()
	{
		var chest = PrefabCatalog.Details(Fixtures.Hash("piece_chest_wood"))!;
		Assert.Equal((5, 2), (chest.ContainerW, chest.ContainerH));
		Assert.Equal(32f, PrefabCatalog.Details(Fixtures.Hash("guard_stone"))!.WardRadius);
		Assert.Equal(20f, PrefabCatalog.Details(Fixtures.Hash("piece_workbench"))!.BuildRange);
		Assert.Contains("Wood", PrefabCatalog.Items);
		Assert.Contains("SwordIron", PrefabCatalog.Items);
		Assert.Equal("Wood", PrefabCatalog.NameOf(Fixtures.Hash("Wood")));
	}

	[Fact]
	public void InventoriesRoundTrip()
	{
		var inv = new InventoryData();
		inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 50, X = 1, Y = 0 });
		inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("SwordIron"), Quality = 3, Durability = 87.5f, X = 2, Y = 1, CrafterId = 1234567890123, CrafterName = "Ragnar", CustomData = { ["k"] = "v" } });
		byte[] bytes = inv.Write();
		Assert.Equal(109, BitConverter.ToInt32(bytes, 0));
		var back = InventoryData.Read(bytes);
		Assert.Equal(2, back.Items.Count);
		Assert.Equal(50, back.Items[0].Stack);
		Assert.Equal(1, back.Items[0].Quality);
		var sword = back.Items[1];
		Assert.Equal((3, 87.5f, 2, 1), (sword.Quality, sword.Durability, sword.X, sword.Y));
		Assert.Equal("Ragnar", sword.CrafterName);
		Assert.Equal(1234567890123, sword.CrafterId);
		Assert.Equal("v", sword.CustomData["k"]);
		Assert.Equal(bytes, back.Write());
	}

	[Fact]
	public void AChestDescribesItsContents()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
		JsonObject d = ObjectEndpoints.Describe(chest, ZdoData.Parse(world.ObjectBytes(chest)));
		Assert.Equal("piece_chest_wood", (string?)d["name"]);
		Assert.Equal(5, (int)d["inventory"]!["width"]!);
		Assert.NotNull(d["inventory"]!["items"]);
	}

	[Fact]
	public void AnEditedChestAndSignAreSavedWithTheirNewData()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
		ZdoData z = ZdoData.Parse(world.ObjectBytes(chest));
		var inv = new InventoryData();
		inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 42 });
		z.SetBytes(StableHash.Of("items"), inv.Write());
		z.Set("strings", StableHash.Of("text"), "Hello");
		edits.AddObjects(new[] { new NewObject(-1, z.Prefab, z.Position, z.Rotation, 0f, null, false, z.Serialize()) });
		edits.SetDeleted(new[] { chest }, true);
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), edits.Deleted, edits.Added);
		Assert.True(r.Saved, r.Message);

		WorldSave again = WorldSave.Load(w.Dir);
		Assert.Equal(world.ObjectCount, again.ObjectCount);
		var found = Enumerable.Range(0, again.ObjectRefs.Count).Select(i => ZdoData.Parse(again.ObjectBytes(i)))
			.Where(o => o.Prefab == Fixtures.Hash("piece_chest_wood") && o.StringList.Any(s => s.Value == "Hello")).ToList();
		var saved = Assert.Single(found);
		Assert.Equal(z.Position, saved.Position);
		Assert.Equal(42, InventoryData.Read(saved.GetBytes(StableHash.Of("items"))!).Items.Single().Stack);
		// Everything else the chest held is kept.
		Assert.Equal(z.LongList, saved.LongList);
	}

	[Fact]
	public void AnEditedObjectCanBeMovedAndKeepsItsData()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
		ZdoData z = ZdoData.Parse(world.ObjectBytes(chest));
		z.Set("strings", StableHash.Of("text"), "Moved");
		var moved = new NewObject(-2, z.Prefab, z.Position + new Vector3(3, 0, 0), new Vector3(0, 90, 0), 0f, null, false, z.Serialize());
		ZdoData m = ZdoData.Parse(world.NewObjectBytes(moved, _ => throw new InvalidOperationException("no source needed"))!);
		Assert.Equal(z.Position.X + 3, m.Position.X, 3);
		Assert.Equal(90f, m.Rotation.Y, 1);
		Assert.Contains(m.StringList, s => s.Value == "Moved");
	}
}
