using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests.Unity;
using Xunit;
using static WorldEditor.Tests.Unity.UnityField;

namespace WorldEditor.Tests;

/// <summary>
/// What the editor reads of the game's files at its start (GameCatalogues), on a made-up game (CatalogueGame,
/// SceneGame): each kind read in the one pass over the bundles, the vegetation rules of the main scene, the cave rocks,
/// the names of object data from the game's code, the English texts; and what is said when the files are not as
/// expected.
/// </summary>
public class GameCataloguesTests
{
    /// <summary>
    /// Each kind's JSON for CatalogueGame, worked out by hand from what it holds (see each test's comments).
    /// </summary>
    private static readonly Dictionary<string, string> Expected = new()
    {
        ["prefabs"] = """
            {"ArmorStand":{"as":[[6,7],[11]],"d":0,"p":1,"t":0},"Beech":{"d":0,"p":1,"t":1},"Beech_Sapling":{"cult":1,"d":0,"gr":1.5,"grows":["Beech"],"p":1,"t":0},"Boar":{"c":1,"d":0,"p":1,"t":2},"Kiln":{"d":0,"p":1,"t":0},"Sword":{"ao":5,"ats":1,"d":0,"dur":200.0,"durl":50.0,"i":1,"it":3,"p":1,"t":0},"Wood":{"at":1,"d":0,"i":1,"it":1,"p":1,"t":0,"var":2},"guard_stone":{"d":1,"p":1,"t":0,"ward":32.0},"itemstand":{"d":0,"is":[1,3],"isi":["Wood"],"isu":["Sword"],"p":1,"t":0},"piece_chest":{"ch":2,"cw":5,"d":0,"p":1,"t":0},"piece_workbench":{"build":20.0,"d":0,"p":1,"t":0},"wood_wall":{"d":0,"p":1,"t":0}}
            """,
        ["pieces"] = """
            {"piece_workbench": {"cat": 1, "comfort": 0, "tool": "hammer"}, "wood_wall": {"cat": 2, "comfort": 1, "snap": [[1.0, 2.0, 0.0]], "box": [-1.0, 1.0, -0.5, 0.5, -0.5, 2.0], "tool": "hammer"}, "stone_floor": {"cat": 0, "comfort": 0, "box": [-0.5, 0.5, -0.5, 0.5, -0.1, 0.1]}}
            """,
        ["piece-support"] = """
            {"wood_wall":{"m":0,"s":1,"c":[0.0,1.0,0.0],"b":[[0.0,1.0,0.0,1.0,1.0,0.1,0,0,0,1,0,10],[0.0,2.0,-1.0,0.5,0.5,0.5,0.0,0.7071,0.0,0.7071,1,10],[0.0,2.0,0.0,0.25,1.0,0.25,0.0,0.7071,0.0,0.7071,1,10],[0.0,2.5,-0.5,0.5,0.5,0.0,0.0,0.7071,0.0,0.7071,1,10],[0.0,0.0,0.0,0.5,0.5,0.5,0.0,0.0,0.0,1.0,0,9],[0.0,0.0,0.0,4.5,4.5,4.5,0.0,0.0,0.0,1.0,0,0]]},"stone_floor":{"m":1,"s":0,"c":[0.0,0.0,0.0],"b":[[0.0,0.0,0.0,0.5,0.1,0.5,0,0,0,1,0,0]],"f":1}}
            """,
        ["piece-place"] = """
            {"piece_workbench":{"f":{},"s":[],"c":[]},"wood_wall":{"f":{"g":1,"nw":1},"s":[[1.0,2.0,0.0]],"c":[["b",0.0,1.0,0.0,1.0,1.0,0.1,0,0,0,1],["s",0.0,2.0,-1.0,0.5],["c",0.0,2.75,0.0,0.0,1.25,0.0,0.25],["m",1,[0.0,2.0,0.0,0.0,2.0,-1.0,0.0,3.0,0.0],[0,1,2]]]},"stone_floor":{"f":{},"s":[],"c":[["b",0.0,0.0,0.0,1.0,0.1,1.0,0,0,0,1]]}}
            """,
        ["piece-cost"] = """
            {"items":{"Wood":"Wood"},"pieces":{"piece_workbench":{"n":"Workbench","r":[["Wood",10,true]]},"stone_floor":{"n":"","r":[]},"wood_wall":{"n":"Wood wall, sturdy","r":[["Wood",4,true]],"st":"Workbench"}}}
            """,
        ["dungeon-rooms"] = """
            {"dungeons":{"DG_Test":{"algorithm":1,"themes":2,"maxRooms":10,"minRooms":3,"tileWidth":8.0,"zoneSize":[64.0,64.0,64.0],"customInterior":0,"baseSeed":0,"doorChance":0.5,"doors":[["dungeon_door","crypt_door",0.0]]}},"rooms":{"crypt_room":{"theme":2,"size":[8,4,8],"endCap":0,"entrance":0,"divider":0,"endCapPrio":0,"weight":2.0,"perimeter":0,"enabled":1,"openings":[["crypt_door",1,1,0,0.0,0.0,4.0,0.0,0.0,0.0,1.0]],"contents":[["Chest",1.0,0.0,1.0,0.0,0.7071,0.0,0.7071,0],["Torch",0.0,0.0,0.0,0.0,0.0,0.0,1.0,3]],"spawns":[[1,50.0,0,2,-10000.0,10000.0]],"picks":[[6,0,[[4,1.0],[5,3.0]],-10000.0,10000.0]],"nodes":[-1,-1,-1,1,-1,-1,-1]}},"locations":{"Village":{"clearArea":1,"randomDamage":0,"interior":0,"customInterior":0,"contents":[["Shrine",0.0,0.0,-3.0,0.0,0.0,0.0,1.0,0]],"spawns":[],"picks":[],"nodes":[-1],"teleports":[],"interiorAt":null}}}
            """,
    };

    [Fact]
    public void EachKindReadAtTheStartComesFromTheBundles()
    {
        using var game = CatalogueGame.Make();
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);
        var kinds = GameCatalogues.ReadAtStart(bundles, [.. Expected.Keys]);
        // Prefabs: networked roots only (the location and the prop are not), creatures, items with what stands need
        // (attach children, durability, variants), stands with the items named across bundles, a sapling with what it
        // grows into once. Pieces: the box around active solid box colliders (the trigger, the disabled and the
        // inactive one left out), snap points even inactive, the hammer's menu for networked ones. Support: every
        // collider as a box in the piece's frame (the turned child's sphere, capsule and mesh bounds), with its layer.
        // Place: colliders on the build ray's layers (the "ghost" child's layer is not). Cost: English names, the
        // station, resources without an item or an amount left out. Rooms: the opening, what the room makes (names
        // cut at " (", the disabled one left out), the random spawn with its node instead, the pick of two. Locations:
        // the Location's flags and what it makes (its networked child), as a room's.
        foreach (var (kind, json) in Expected)
        {
            Assert.Equal(json, kinds[kind]);
        }
    }

    [Fact]
    public void TerrainModifiersAreReadUpToTheirPrefab()
    {
        using var game = CatalogueGame.Make();
        game.Save();
        var json = JsonNode.Parse(GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), ["terrain-modifiers"])["terrain-modifiers"])!;
        // The prop is neither a location nor networked: not kept.
        Assert.Equal(["Village", "Kiln"], json.AsObject().Select(kv => kv.Key));
        var village = json["Village"]!;
        Assert.True(village["location"]!.GetValue<bool>());
        // The inactive child's and the disabled one left out; in the order of a walk down the prefab.
        var mods = village["modifiers"]!.AsArray();
        Assert.Equal([1, 4], mods.Select(m => m!["sortOrder"]!.GetValue<long>()));
        // (2, 1, 0) under a child at (10, 0, 0) turned 90° about y.
        Assert.Equal([10.0, 1.0, -2.0], mods[0]!["pos"]!.AsArray().Select(v => v!.GetValue<double>()));
        Assert.Equal(4.5, mods[0]!["levelRadius"]!.GetValue<double>());
        Assert.Equal(0.5, mods[0]!["paintStrength"]!.GetValue<double>());
        // On the root of a networked prefab: at its origin, in whole numbers.
        var kiln = json["Kiln"]!;
        Assert.False(kiln["location"]!.GetValue<bool>());
        Assert.Equal("[0,0,0]", kiln["modifiers"]![0]!["pos"]!.ToJsonString());
    }

    [Fact]
    public void AKindNotReadAtTheStartIsRefused()
    {
        using var game = new TestGame();
        game.Save();
        Assert.Throws<ArgumentException>(() => GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), ["vegetation"]));
    }

    [Fact]
    public void BundlesWithoutTypeTreesAreReadAsHavingNoScriptFields()
    {
        // Their objects' fields are not described: their pieces are not found, and nothing fails.
        using var game = new TestGame();
        var bare = game.Bundle("bare", typeTrees: false);
        bare.Prefab("bare_wall", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("Piece", new Fields { ["m_category"] = 2 });
            r.Box((0, 0, 0), (1, 1, 1));
        });
        game.Save();
        var kinds = GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), [.. GameCatalogues.AtStart.Where(k => k != "piece-cost")]);
        Assert.Equal("{}", kinds["prefabs"]);
        Assert.Equal("{}", kinds["piece-place"]);
    }

    [Fact]
    public void AScriptsBundleReadInThePassStaysOpenForLaterScripts()
    {
        // The scripts' bundle is kept open from the first piece on, and is one of the pass's bundles too: read there
        // (AssetsTools gives the same instance again) and let go, it must stay open for the last piece's classes.
        using var game = new TestGame();
        var scripts = game.Bundle("m_scripts");
        var first = game.Bundle("a_pieces");
        first.ScriptsFrom = scripts;
        first.Prefab("first_floor", r =>
        {
            r.Mono("Piece", new Fields { ["m_category"] = 0, ["m_resources"] = new List<Fields>() });
            r.Box((0, 0, 0), (1, 0.2, 1));
        });
        var last = game.Bundle("z_pieces");
        last.ScriptsFrom = scripts;
        last.Prefab("last_floor", r =>
        {
            r.Mono("Piece", new Fields { ["m_category"] = 0, ["m_resources"] = new List<Fields>() });
            r.Mono("WearNTear", new Fields { ["m_materialType"] = 1, ["m_supports"] = false });
            r.Box((0, 0, 0), (1, 0.2, 1));
        });
        game.Save();
        var kinds = GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), ["pieces", "piece-support"]);
        Assert.Equal(["first_floor", "last_floor"], JsonNode.Parse(kinds["pieces"])!.AsObject().Select(kv => kv.Key));
        Assert.Equal(["last_floor"], JsonNode.Parse(kinds["piece-support"])!.AsObject().Select(kv => kv.Key));
    }

    [Fact]
    public void ObjectsLaidOutOtherwiseAreSaidRatherThanReadWrong()
    {
        // A Transform with a field more before its children (another Unity's layout): read raw, the piece's object
        // looks like a prefab's root, which its type tree says it is not.
        using var game = new TestGame();
        var b = game.Bundle("odd");
        var transform = Struct("Transform", "Base", Ref("m_GameObject"), Quaternion("m_LocalRotation"), Vector3("m_LocalPosition"), Vector3("m_LocalScale"),
            Of("SInt64", "m_Extra"), List("m_Children", Ref("data")), Ref("m_Father"));
        long go = b.Reserve(), tr = b.Reserve();
        long piece = b.Mono(go, "Piece", new Fields { ["m_category"] = 1 });
        b.Assets.Add(UnityClass.GameObject, UnityTypes.GameObject, new Fields
        {
            ["m_Component"] = new List<Fields> { new() { ["component"] = Fields.Ref(tr) }, new() { ["component"] = Fields.Ref(piece) } },
            ["m_Name"] = "odd_piece",
            ["m_IsActive"] = true,
        }, pathId: go);
        b.Assets.Add(UnityClass.Transform, transform, new Fields { ["m_GameObject"] = Fields.Ref(go), ["m_Father"] = Fields.Ref(4321) }, pathId: tr);
        game.Save();
        var e = Assert.Throws<InvalidDataException>(() => GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), ["pieces"]));
        Assert.Contains("not laid out", e.Message);
    }

    [Fact]
    public void TheVegetationRulesComeFromTheMainScene()
    {
        var order = GameLocations.ListOrder().Order;
        using var game = SceneGame.Make(order);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);
        Assert.Equal([SceneGame.Main, SceneGame.Shared], GameLocations.MainSceneBundles(bundles));
        var rules = JsonNode.Parse(GameCatalogues.VegetationJson(bundles))!.AsArray();
        // ZoneSystem's own first (the rule whose prefab is not found left out), then the lists by sort order.
        Assert.Equal(["Bushes", "Rocks", "Berries"], rules.Take(3).Select(r => r!["name"]!.GetValue<string>()));
        Assert.Equal(["Bush", "Rock", "Berries"], rules.Take(3).Select(r => r!["prefab"]!.GetValue<string>()));
        // The draws when they wake: Bush a LodFadeInOut with its LODGroup and a bird's two (the inactive child's rock
        // sleeps); Rock its MineRock5 (inactive, but the root wakes; its LodFadeInOut has no LODGroup); Berries the
        // Pickable that respawns, not its child's.
        Assert.Equal([3L, 1L, 1L], rules.Take(3).Select(r => r!["draws"]!.GetValue<long>()));
        Assert.Equal(1.0, rules[0]!["min"]!.GetValue<double>());
        Assert.Equal(3 + order.Sum(o => o.Vegetation) + 1, rules.Count);
        Assert.Equal("Extra", rules[^1]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void ASceneWithoutZoneSystemIsSaid()
    {
        using var game = new TestGame();
        game.Bundle("scene").WithContents = false;
        game.Manifest("00000000000000000000000000000001", "scene", "Assets/Scenes/main.unity");
        game.Save();
        Assert.Throws<InvalidDataException>(() => GameCatalogues.VegetationJson(new GameBundles(game.BundlesDir, null)));
    }

    [Fact]
    public void TheCaveRocksAreTheirMeshesScaled()
    {
        using var game = new TestGame();
        var b = game.Bundle("rocks");
        b.Prefab("rock4_forest");
        b.Prefab("rock3_mountain");
        new TestMesh { Name = "Rock4", Positions = [1, 2, 3, 0.5f, 0, 0, 0, 0, -0.25f], Submeshes = [[0, 1, 2]] }.Add(b);
        new TestMesh { Name = "Rock3", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0], Submeshes = [[0, 1, 2], [1, 3, 2]], Wide = true }.Add(b);
        game.Save();
        var json = JsonNode.Parse(GameCatalogues.CaveRocksJson(new GameBundles(game.BundlesDir, null)))!;
        // Scaled 4 times; triangles wound the other way (as through an OBJ file).
        Assert.Equal("[[4.0,8.0,12.0],[2.0,0.0,0.0],[0.0,0.0,-1.0]]", json["rock4_forest"]!["v"]!.ToJsonString());
        Assert.Equal("[2,1,0]", json["rock4_forest"]!["t"]!.ToJsonString());
        Assert.Equal("[2,1,0,2,3,1]", json["rock3_mountain"]!["t"]!.ToJsonString());
    }

    [Fact]
    public void ACaveRockMissingIsSaid()
    {
        using var game = new TestGame();
        var b = game.Bundle("rocks");
        b.Prefab("rock4_forest");
        game.Save();
        var e = Assert.Throws<InvalidDataException>(() => GameCatalogues.CaveRocksJson(new GameBundles(game.BundlesDir, null)));
        Assert.Contains("Rock4", e.Message);
        using var none = new TestGame();
        none.Save();
        Assert.Throws<InvalidDataException>(() => GameCatalogues.CaveRocksJson(new GameBundles(none.BundlesDir, null)));
    }

    /// <summary>
    /// A name of object data the test assembly's code has as a literal (the game's code: the test's own here).
    /// </summary>
    private const string DataName = "vwe_test_data_name";

    [Fact]
    public void TheNamesOfObjectDataComeFromTheGamesCode()
    {
        using var game = new TestGame();
        game.Code(typeof(GameCataloguesTests).Assembly.Location);
        var names = JsonNode.Parse(GameCatalogues.ZdoKeysJson(game.Root))!;
        static string Key(string n) => StableHash.Of(n).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(DataName, names[Key(DataName)]!.GetValue<string>());
        Assert.Equal(DataName + "_u", names[Key(DataName + "_u")]!.GetValue<string>());
        // Built at run time by the game.
        Assert.Equal("pu_id3", names[Key("pu_id3")]!.GetValue<string>());
        Assert.Equal("2_item", names[Key("2_item")]!.GetValue<string>());
        // Not a name: spaces first, too long.
        Assert.Null(names[Key(" no")]);
        using var none = new TestGame();
        Assert.Throws<FileNotFoundException>(() => GameCatalogues.ZdoKeysJson(none.Root));
    }

    [Fact]
    public void TheEnglishTextsAreReadWithoutTypeTreesToo()
    {
        // resources.assets as the game ships it: no type trees, its texts read as laid out.
        using var game = CatalogueGame.Make();
        game.Resources([("localization_english", "item_wood,Timber\npiece_woodwall,\"Wall\nof wood\"\npiece_workbench,Bench\n,blank key\n")], typeTrees: false);
        game.Save();
        string cost = GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), ["piece-cost"])["piece-cost"];
        Assert.Contains("\"Wood\":\"Timber\"", cost);
        Assert.Contains("\"n\":\"Wall\\nof wood\"", cost);
        Assert.Contains("\"st\":\"Bench\"", cost);
    }

    [Fact]
    public void ALineBreakInAnUnquotedTextIsSaid()
    {
        // As Python's csv reads them: a carriage return inside an unquoted field.
        using var game = CatalogueGame.Make();
        game.Resources([("localization_english", "item_wood,Ti\rmber\n")]);
        game.Save();
        Assert.Throws<InvalidDataException>(() => GameCatalogues.ReadAtStart(new GameBundles(game.BundlesDir, null), ["piece-cost"]));
    }
}
