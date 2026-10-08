using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// The shared data code on copies of the test world: zone statistics with player-built pieces (distance to
// buildings), searching objects, container contents and texts (saved, added and live), an object's data
// for the inspector, the blueprint store, heightmap pictures, the saved server list and backups.
// Opening worlds and setting the builder of new pieces: one world at a time.
[Collection("World files")]
public class CoreAppDataTests
{
	private static readonly int Wall = StableHash.Of("wood_wall_half"), Beech = StableHash.Of("Beech1");

	// ---- Zone statistics.

	private static int[][] Rows(int[] data) => Enumerable.Range(0, data.Length / ZoneStats.Stride).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray()).ToArray();

	[Fact]
	public void ZoneStatsCountBuildingsAndTheDistanceToThem()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var tree = world.Objects.First(o => o.Prefab == Beech);
		// Two player-built walls in the tree's zone (built by player 777).
		long before = WorldSave.Builder;
		try
		{
			WorldSave.Builder = 777;
			var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: new[]
			{
				new NewObject(-1, Wall, tree.Position + new Vector3(2, 0, 2), Vector3.Zero, 0f),
				new NewObject(-2, Wall, tree.Position + new Vector3(4, 0, 2), Vector3.Zero, 0f),
			});
			Assert.True(r.Saved, r.Message);
		}
		finally
		{
			WorldSave.Builder = before;
		}
		world = w.Load();
		Assert.Equal(2, world.Pieces.Count);
		var home = world.ObjectRefs[world.Pieces[0].Id].Zone;
		var edits = new EditStore(world);
		var rows = Rows(ZoneStats.Compute(world, edits, (x, z) => x == home.X && z == home.Z ? 8 : 1));
		var at = rows.Single(r => r[0] == home.X && r[1] == home.Z);
		Assert.Equal(8, at[2]);
		Assert.Equal(2, at[3]);
		Assert.Equal(0, at[6]);
		// Every other zone: as many zones away as the larger of the x and z steps.
		Assert.All(rows.Where(r => r != at), r => Assert.Equal(Math.Max(Math.Abs(r[0] - home.X), Math.Abs(r[1] - home.Z)), r[6]));
		// Deleted walls are not buildings any more.
		edits.SetDeleted(world.Pieces.Select(p => p.Id), true);
		var after = Rows(ZoneStats.Compute(world, edits, (_, _) => 1));
		Assert.All(after, r => Assert.Equal(0, r[3]));
		Assert.All(after, r => Assert.Equal(-1, r[6]));
	}

	[Fact]
	public void ZoneStatsNeedTheWorldsZoneList()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		typeof(WorldSave).GetProperty(nameof(WorldSave.Zones))!.SetValue(world, null);
		Assert.Empty(ZoneStats.Compute(world, new EditStore(world), (_, _) => 1));
	}

	// ---- Search.

	// The test world's objects carry little data: a chest with contents and a sign text is saved first.
	private static WorldSave WithChest(TempWorld w, out int chestId)
	{
		WorldSave world = w.Load();
		int chest = world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id;
		var z = ZdoData.Parse(world.ObjectBytes(chest));
		var inv = new InventoryData();
		inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 42 });
		inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 8, X = 1 });
		inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Stone"), Stack = 5, X = 2 });
		z.SetBytes(ObjectData.ItemsKey, inv.Write());
		z.Set("strings", StableHash.Of("text"), "Welcome home");
		// A second chest whose contents cannot be read.
		var broken = ZdoData.Parse(world.ObjectBytes(chest));
		broken.SetBytes(ObjectData.ItemsKey, new byte[] { 9, 9, 9 });
		var r = WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: new[]
		{
			new NewObject(-1, z.Prefab, z.Position + new Vector3(3, 0, 3), Vector3.Zero, 0f, Fresh: false, Raw: z.Serialize()),
			new NewObject(-2, z.Prefab, z.Position + new Vector3(6, 0, 3), Vector3.Zero, 0f, Fresh: false, Raw: broken.Serialize()),
		});
		Assert.True(r.Saved, r.Message);
		world = w.Load();
		chestId = Enumerable.Range(0, world.ObjectRefs.Count).Single(i => ZdoData.Parse(world.ObjectBytes(i)).StringList.Any(s => s.Value == "Welcome home"));
		return world;
	}

	[Fact]
	public void ContainersAreFoundByWhatTheyHold()
	{
		using var w = new TempWorld();
		WorldSave world = WithChest(w, out int chest);
		var edits = new EditStore(world);
		var r = WorldSearch.Search(world, edits, "WOOD", "items");
		var hit = Assert.Single(r.Hits);
		Assert.Equal(chest, hit.Id);
		Assert.Equal("Wood ×50", hit.Match);
		Assert.Equal(50, r.Counts["Wood"]);
		Assert.Empty(WorldSearch.Search(world, edits, "Silver", "items").Hits);
		// Deleted: no longer found.
		edits.SetDeleted(new[] { chest }, true);
		Assert.Empty(WorldSearch.Search(world, edits, "wood", "items").Hits);
	}

	[Fact]
	public void TextsAreFound()
	{
		using var w = new TempWorld();
		WorldSave world = WithChest(w, out int chest);
		var r = WorldSearch.Search(world, new EditStore(world), "welcome", "texts");
		var hit = Assert.Single(r.Hits);
		Assert.Equal(chest, hit.Id);
		Assert.Equal("text: Welcome home", hit.Match);
		Assert.Empty(WorldSearch.Search(world, new EditStore(world), "nothing like it", "texts").Hits);
	}

	[Fact]
	public void ObjectsAddedInTheEditorAreFoundToo()
	{
		using var w = new TempWorld();
		WorldSave world = WithChest(w, out int chest);
		var edits = new EditStore(world);
		var copy = ZdoData.Parse(world.ObjectBytes(chest));
		copy.Set("strings", StableHash.Of("text"), "Copied sign");
		edits.AddObjects(new[]
		{
			new NewObject(-5, copy.Prefab, copy.Position + new Vector3(1, 0, 0), Vector3.Zero, 0f, Fresh: false, Raw: copy.Serialize()),
			new NewObject(-6, Beech, copy.Position + new Vector3(2, 0, 0), Vector3.Zero, 1f),
		});
		Assert.Contains(WorldSearch.Search(world, edits, "copied", "texts").Hits, h => h.Id == -5);
		Assert.Contains(WorldSearch.Search(world, edits, "wood", "items").Hits, h => h.Id == -5);
		Assert.Contains(WorldSearch.Search(world, edits, "beech1", "kinds").Hits, h => h.Id == -6);
	}

	[Fact]
	public void SearchingNothingFindsNothingAndManyHitsAreCut()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		var none = WorldSearch.Search(world, edits, "   ", "kinds");
		Assert.Equal(0, none.Total);
		Assert.Empty(none.Hits);
		var all = WorldSearch.Search(world, edits, "e", "kinds");
		Assert.True(all.Total > 2);
		var cut = WorldSearch.Search(world, edits, "e", "kinds", limit: 2);
		Assert.Equal(2, cut.Hits.Count);
		Assert.True(cut.Truncated);
		Assert.Equal(all.Total, cut.Total);
	}

	[Fact]
	public void TheLiveWorldIsSearchedInItsSnapshot()
	{
		WorldSave live = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
		var edits = new EditStore(live);
		Assert.NotEmpty(WorldSearch.Search(live, edits, "beech", "kinds").Hits);
		// Containers and texts are read from the snapshot's bytes (no files).
		var items = WorldSearch.Search(live, edits, "zzz-no-such-item", "items");
		Assert.Empty(items.Hits);
		WorldSearch.Search(live, edits, "zzz", "texts");
	}

	// ---- An object's data for the inspector.

	[Fact]
	public void AnObjectsBytesComeFromTheSaveOrTheSession()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var edits = new EditStore(world);
		Assert.NotNull(ObjectData.Bytes(world, edits, 0));
		Assert.Null(ObjectData.Bytes(world, edits, world.ObjectRefs.Count));
		Assert.Null(ObjectData.Bytes(world, edits, -9));
		var tree = world.Objects.First(o => o.Prefab == Beech);
		edits.AddObjects(new[] { new NewObject(-9, Beech, tree.Position + Vector3.UnitX, Vector3.Zero, 1f) });
		var z = ZdoData.Parse(ObjectData.Bytes(world, edits, -9)!);
		Assert.Equal(Beech, z.Prefab);
		// Deleted again, it is still there for undo.
		edits.SetDeleted(new[] { -9 }, true);
		Assert.NotNull(ObjectData.Bytes(world, edits, -9));
	}

	[Fact]
	public void ContentsAreBuiltFromItemsWithinTheGamesLimits()
	{
		var inv = ObjectData.BuildInventory(new()
		{
			new ItemUpload("Wood", null, Stack: 0, Quality: 99999, X: 300, Y: -4, WorldLevel: 900, CrafterId: "123", CrafterName: "Ragnar"),
			new ItemUpload(null, StableHash.Of("Stone"), Stack: 70000, CrafterId: "not a number"),
		});
		var wood = inv.Items[0];
		Assert.Equal((1, 65535, 255, 0, 255, 123L, "Ragnar"), (wood.Stack, wood.Quality, wood.X, wood.Y, wood.WorldLevel, wood.CrafterId, wood.CrafterName));
		Assert.Equal(65535, inv.Items[1].Stack);
		Assert.Equal(0, inv.Items[1].CrafterId);
		var ex = Assert.Throws<ArgumentException>(() => ObjectData.BuildInventory(new() { new ItemUpload("NoSuchItem", null) }));
		Assert.Contains("NoSuchItem", ex.Message);
		Assert.Throws<ArgumentException>(() => ObjectData.BuildInventory(new() { new ItemUpload(null, 12345) }));
	}

	[Fact]
	public void AnEditedObjectGetsItsValuesAndContents()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		var bytes = world.ObjectBytes(world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id);
		var z = ObjectData.Edited(bytes, new() { new FieldChange("strings", "text", "Hi"), new FieldChange("ints", "health_level", "3") }, new() { new ItemUpload("Wood", null, Stack: 7) });
		Assert.Equal("Hi", z.StringList.Single(s => s.Key == StableHash.Of("text")).Value);
		Assert.Equal(7, InventoryData.Read(z.GetBytes(ObjectData.ItemsKey)!).Items.Single().Stack);
		// Nothing asked: the same object.
		Assert.Equal(bytes, ObjectData.Edited(bytes, null, null).Serialize());
	}

	// ---- The blueprint store.

	private static JsonObject Clip(int objects = 1, bool ground = false) => new()
	{
		["w"] = 2, ["h"] = 1,
		["rel"] = new JsonArray(ground ? 120 : -32768, -32768),
		["objects"] = new JsonArray(Enumerable.Range(0, objects).Select(_ => (JsonNode)new JsonObject { ["name"] = "Beech1" }).ToArray()),
	};

	[Fact]
	public void BlueprintsAreSavedListedReadAndDeleted()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var store = new BlueprintStore(dir);
			Assert.Empty(store.List());
			string id = store.Save("  My house ", "Midgard", "data:thumb", Clip(3, ground: true), "planbuild");
			Assert.Equal("My house", id);
			store.Save("Shed", null, null, Clip());
			// Not blueprints: other JSON, broken JSON, a half-written file.
			File.WriteAllText(Path.Combine(dir, "other.json"), "{\"name\":\"x\"}");
			File.WriteAllText(Path.Combine(dir, "broken.json"), "{ nope");
			File.WriteAllText(Path.Combine(dir, "Shed.json.tmp"), "{");
			// A blueprint with a bad date: the file's own time is used.
			File.WriteAllText(Path.Combine(dir, "Dated.json"), "{\"name\":\"Dated\",\"created\":\"yesterday\",\"clip\":{\"w\":1,\"h\":1,\"objects\":[]}}");
			var list = store.List();
			Assert.Equal(new[] { "Dated", "My house", "Shed" }, list.Select(b => b.Name).ToArray());
			var house = list[1];
			Assert.Equal((3, true, "Midgard", "data:thumb", "planbuild"), (house.Objects, house.Ground, house.World, house.Thumb, house.Source));
			Assert.False(list[2].Ground);
			Assert.True(store.Exists(" My house "));
			Assert.False(store.Exists("Barn"));
			Assert.Contains("\"name\":\"My house\"", store.Read("My house"));
			// Ids that would leave the folder or are not file names are refused.
			Assert.Null(store.Read("../My house"));
			Assert.Null(store.Read(""));
			Assert.False(store.Delete("../x"));
			Assert.False(store.Delete("nothing"));
			Assert.True(store.Delete("Shed"));
			Assert.Null(store.Read("Shed"));
			// The same name again replaces it.
			store.Save("My house", null, null, Clip(5));
			Assert.Equal(5, store.List().Single(b => b.Id == "My house").Objects);
		}
		finally
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}
		}
	}

	[Fact]
	public void ABlueprintNeedsANameAndACopy()
	{
		var store = new BlueprintStore(Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..8]));
		Assert.Throws<ArgumentException>(() => store.Save("  ", null, null, Clip()));
		Assert.Throws<ArgumentException>(() => store.Save("x", null, null, new JsonObject { ["w"] = 1 }));
		Assert.Throws<ArgumentException>(() => store.Save("x", null, null, new JsonObject { ["objects"] = new JsonArray(), ["w"] = 1 }));
	}

	[Theory]
	[InlineData("Tower: north/east", "Tower_ north_east")]
	[InlineData("..", "blueprint")]
	[InlineData("  ...  ", "blueprint")]
	[InlineData("a.b-c_d e", "a.b-c_d e")]
	public void BlueprintFileNamesAreSafe(string name, string id) => Assert.Equal(id, BlueprintStore.IdFor(name));

	[Fact]
	public void LongBlueprintNamesAreCut() => Assert.Equal(80, BlueprintStore.IdFor(new string('x', 200)).Length);

	[Fact]
	public void TheDefaultStoreIsInTheDataFolder() => Assert.EndsWith("blueprints", BlueprintStore.Default.Directory);

	// ---- Heightmaps.

	[Fact]
	public void FlatGroundStillMakesAHeightmap()
	{
		float[] flat = Enumerable.Repeat(30f, 4).ToArray();
		byte[] png = Heightmaps.Encode(2, 2, flat, "flat", out float min, out float max);
		Assert.Equal(30f, min);
		Assert.Equal(30.01f, max, 4);
		var p = Heightmaps.Read(png, 2, 2);
		Assert.Equal((2, 2, 30f, "flat"), (p.Width, p.Height, p.Min!.Value, p.Area));
		Assert.All(p.Values, v => Assert.Equal(0f, v));
	}

	[Fact]
	public void APictureWithoutTheEditorsHeightsIsJustBrightness()
	{
		byte[] png = Png.WriteGray16(2, 1, new ushort[] { 0, 65535 });
		var p = Heightmaps.Read(png, 3, 1);
		Assert.Null(p.Min);
		Assert.Null(p.Max);
		Assert.Null(p.Area);
		Assert.Equal(new[] { 0f, 0.5f, 1f }, p.Values);
		// One point: the picture's first pixel.
		Assert.Equal(new[] { 0f }, Heightmaps.Read(png, 1, 1).Values);
	}

	// ---- Saved servers.

	[Fact]
	public void TheServerListIsReadLeniently()
	{
		string file = Path.Combine(Path.GetTempPath(), "vwe-servers-" + Guid.NewGuid().ToString("N")[..8] + ".cfg");
		string? old = ServerConfig.PathOverride;
		ServerConfig.PathOverride = file;
		try
		{
			Assert.Empty(ServerConfig.Load());
			File.WriteAllText(file, string.Join("\n", new[]
			{
				"Host = before any section", "; a comment", "# another",
				"[Main]", "Host = 203.0.113.1", "User = valheim", "SshPort = not a number", "BridgePort = x", "Password =", "KeyFile = ~/.ssh/k", "no equals sign here",
				"[No user]", "Host = 203.0.113.2",
				"[  Spaced  ]", "Host = 203.0.113.3", "User = u", "SshPort = 2222", "BridgePort = 5190", "Token = t", "HostKey = SHA256:x", "GameFolder = /srv/v", "Unknown = 1",
			}));
			var list = ServerConfig.Load();
			Assert.Equal(new[] { "Main", "Spaced" }, list.Select(s => s.Name).ToArray());
			Assert.Equal((22, 5182, null, "~/.ssh/k"), (list[0].SshPort, list[0].BridgePort, list[0].Password, list[0].KeyFile));
			Assert.Equal((2222, 5190, "t", "SHA256:x", "/srv/v"), (list[1].SshPort, list[1].BridgePort, list[1].Token, list[1].HostKey, list[1].GameFolder));
			// A server remembered without a name is named after its address; ] in a name cannot end the section.
			ServerConfig.Remember(new ServerConfig.Server { Host = "198.51.100.7", User = "admin" });
			ServerConfig.Remember(new ServerConfig.Server { Name = "Odd] name", Host = "198.51.100.8", User = "admin" });
			var again = ServerConfig.Load();
			Assert.Equal("Odd) name", again[0].Name);
			Assert.Equal("198.51.100.7", again[1].Name);
			Assert.Equal(4, again.Count);
		}
		finally
		{
			ServerConfig.PathOverride = old;
			File.Delete(file);
		}
	}

	// ---- Backups.

	[Fact]
	public void BackupsNextToAWorldAreListedNewestFirst()
	{
		using var w = new TempWorld();
		string parent = Path.GetDirectoryName(w.Dir)!;
		string editor = Path.Combine(parent, "CITest_backup_terraineditor-20261001-100000");
		string game = Path.Combine(parent, "CITest_backup_auto-20261002100000");
		TempWorld.CopyDir(w.Dir, editor);
		TempWorld.CopyDir(w.Dir, game);
		Directory.SetLastWriteTime(editor, new DateTime(2026, 10, 1));
		Directory.SetLastWriteTime(game, new DateTime(2026, 10, 2));
		// Not backups: no save in it, or another world's.
		Directory.CreateDirectory(Path.Combine(parent, "CITest_backup_empty"));
		TempWorld.CopyDir(w.Dir, Path.Combine(parent, "Other_backup_auto-1"));
		var found = Backups.Find(w.Dir + Path.DirectorySeparatorChar);
		Assert.Equal(new[] { ("game", game), ("editor", editor) }, found.Select(b => (b.Kind, b.Path)).ToArray());
		Assert.Empty(Backups.Find(Path.Combine(parent, "nowhere", "World")));
		Assert.Empty(Backups.Find("/"));
	}
}
