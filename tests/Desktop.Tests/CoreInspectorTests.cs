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

// Searching the whole world for kinds, items in containers and texts.
public class SearchTests
{
	[Fact]
	public void FindsObjectsOfAKind()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		var r = WorldSearch.Search(world, edits, "beech", "kinds");
		int beeches = world.Objects.Count(o => o.Prefab == Fixtures.Hash("Beech1"));
		Assert.Equal(beeches, r.Counts["Beech1"]);
		Assert.All(r.Hits, h => Assert.Contains("eech", h.Name));
		// Deleted objects are not found, new ones are.
		edits.SetDeleted(new[] { r.Hits[0].Id }, true);
		edits.AddObjects(new[] { new NewObject(-5, Fixtures.Hash("Beech1"), new Vector3(1, 2, 3), Vector3.Zero, 0f) });
		var again = WorldSearch.Search(world, edits, "Beech1", "kinds");
		Assert.Equal(beeches, again.Total);
		Assert.Contains(again.Hits, h => h.Id == -5);
		// Bookkeeping objects only when asked for.
		Assert.DoesNotContain(WorldSearch.Search(world, edits, "e", "kinds").Hits, h => h.Name.StartsWith('_'));
		Assert.NotEmpty(WorldSearch.Search(world, edits, "_zonectrl", "kinds").Hits);
	}

	[Fact]
	public void FindsItemsInContainersAndTexts()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
		ZdoData z = ZdoData.Parse(world.ObjectBytes(chest));
		var inv = new InventoryData();
		inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 30 });
		inv.Items.Add(new InventoryData.Item { Prefab = Fixtures.Hash("Wood"), Stack = 12, X = 1 });
		z.SetBytes(StableHash.Of("items"), inv.Write());
		z.Set("strings", StableHash.Of("text"), "Odin's stash");
		edits.AddObjects(new[] { new NewObject(-1, z.Prefab, z.Position, z.Rotation, 0f, null, false, z.Serialize()) });
		var items = WorldSearch.Search(world, edits, "wood", "items");
		var hit = Assert.Single(items.Hits);
		Assert.Equal(-1, hit.Id);
		Assert.Equal("Wood ×42", hit.Match);
		Assert.Equal(42, items.Counts["Wood"]);
		var texts = WorldSearch.Search(world, edits, "odin", "texts");
		Assert.Equal("text: Odin's stash", Assert.Single(texts.Hits).Match);
		Assert.Empty(WorldSearch.Search(world, edits, "", "kinds").Hits);
	}
}

// The map's zone filter: generated zones with their buildings, edits and distance to buildings.
public class ZoneStatsTests
{
	[Fact]
	public void GeneratedZonesInTheWorldAreListed()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		int[] data = ZoneStats.Compute(world, edits, (x, z) => 1);
		int n = data.Length / ZoneStats.Stride;
		Assert.True(n > 0);
		var rows = Enumerable.Range(0, n).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray()).ToList();
		// Zones generated far outside the world (around an empty server's reference point, as in this
		// test world) are left out; zones with objects are in, generated or not.
		Assert.All(rows, r => Assert.True(Math.Abs(r[0]) < 170 && Math.Abs(r[1]) < 170));
		Assert.True(world.Zones!.Generated.All(g => Math.Abs(g.X) > 170));
		Assert.All(rows, r => Assert.Equal(0, r[7]));
		// The edited zone is marked, and every zone counts its objects.
		Assert.Contains(rows, r => r[4] == 1);
		Assert.Equal(world.ObjectRefs.Count(o => !o.IsTerrain && Math.Abs(o.Zone.X) < 170), rows.Sum(r => r[5]));
	}

	[Fact]
	public void DistanceToBuildingsCountsZones()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		int[] data = ZoneStats.Compute(world, edits, (x, z) => 1);
		var rows = Enumerable.Range(0, data.Length / ZoneStats.Stride).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray()).ToList();
		if (world.Pieces.Count == 0)
		{
			Assert.All(rows, r => Assert.Equal(-1, r[6]));
			return;
		}
		foreach (var r in rows)
		{
			int expected = world.Pieces.Select(p => { var (x, z) = world.ObjectRefs[p.Id].Zone; return Math.Max(Math.Abs(x - r[0]), Math.Abs(z - r[1])); }).Min();
			Assert.Equal(expected, r[6]);
		}
	}
}

// New pieces are player built: they get the chosen builder as "creator", like pieces built in the game.
// Opening worlds sets the builder for new pieces (WorldSave.Builder): one world at a time.
[Collection("World files")]
public class BuilderTests
{
	private static readonly int CreatorKey = StableHash.Of("creator");

	private static long CreatorOf(byte[] bytes) => ZdoData.Parse(bytes).LongList.FirstOrDefault(l => l.Key == CreatorKey).Value;

	[Fact]
	public void NewBuildPiecesGetTheBuilderOtherKindsAndMovesDoNot()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		long before = WorldSave.Builder;
		try
		{
			WorldSave.Builder = 1234567890123L;
			byte[] Source(ObjectRef m) => File.ReadAllBytes(Path.Combine(world.Directory, m.File.FileName));
			// Copied from one in the world when it has one, else built blank.
			byte[] Make(string name) => world.NewObjectBytes(new NewObject(-5, Fixtures.Hash(name), new Vector3(10, 30, 10), Vector3.Zero, 0f), Source)!;
			Assert.Equal(1234567890123L, CreatorOf(Make("woodwall")));        // hammer
			Assert.Equal(1234567890123L, CreatorOf(Make("sapling_turnip")));  // cultivator
			Assert.Equal(0L, CreatorOf(Make("Beech1")));                      // not built by players
			// Food set out with the serving tray is an item made a piece: marked so, it stays put.
			int PieceFlag(byte[] b) => ZdoData.Parse(b).IntList.FirstOrDefault(i => i.Key == StableHash.Of("piece")).Value;
			byte[] chicken = Make("HoneyGlazedChicken");
			Assert.Equal(1, PieceFlag(chicken));
			Assert.Equal(1234567890123L, CreatorOf(chicken));                 // feaster
			Assert.Equal(0, PieceFlag(Make("woodwall")));
			// A moved object keeps what it had.
			int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
			ZdoData z = ZdoData.Parse(world.ObjectBytes(chest));
			var moved = new NewObject(-6, z.Prefab, z.Position + new Vector3(1, 0, 0), z.Rotation, 0f, chest, false);
			Assert.Equal(0L, CreatorOf(world.NewObjectBytes(moved, Source)!));
		}
		finally
		{
			WorldSave.Builder = before;
		}
	}

	[Fact]
	public void GrownCropsAndTreesKeepTheirSaplingsGrowRadius()
	{
		Assert.Equal(0.5f, TerrainEditor.Terrain.PrefabCatalog.GrownFrom["Pickable_Carrot"].Radius);
		Assert.Equal("Oak_Sapling", TerrainEditor.Terrain.PrefabCatalog.GrownFrom["Oak1"].Sapling);
		Assert.False(TerrainEditor.Terrain.PrefabCatalog.GrownFrom.ContainsKey("BlueberryBush"));
	}

	[Fact]
	public void ACharacterFileGivesItsNameAndPlayerId()
	{
		// ... per-world data, then the name, the player id and an empty start seed.
		byte[] name = System.Text.Encoding.UTF8.GetBytes("Shuiei");
		byte[] file = new byte[] { 1, 2, 3, 6 }.Concat(name).Concat(BitConverter.GetBytes(3349326647L)).Concat(new byte[] { 0, 1, 0, 0 }).ToArray();
		var c = TerrainEditor.App.Characters.Read(file, "shuiei");
		Assert.NotNull(c);
		Assert.Equal("Shuiei", c!.Name);
		Assert.Equal(3349326647L, c.Id);
		Assert.Null(TerrainEditor.App.Characters.Read(file, "someone"));
	}
}
