using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The installed mods' pieces read without the game (ModScan): a bundle's pieces taken with their mod (those without a
/// Piece too, placed by their snap points and box), laid under the game's data and a live game's report, the mods'
/// names and English texts from their folders, the manifests' pieces, and the reading kept.
/// </summary>
[Collection("DataDir")]
public sealed class ModScanTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-modscan-" + Guid.NewGuid().ToString("N")[..8]);

    public ModScanTests()
    {
        AppSettings.DataDirOverride = Path.Combine(_dir, "data");
        ModScan.Forget();
        ModCatalogue.Forget();
    }

    /// <summary>Forgets the mods, puts the data folder, the manifests and the tests' copies back, and deletes the folder.</summary>
    public void Dispose()
    {
        ModScan.Forget();
        ModCatalogue.Forget();
        ModManifests.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>
    /// One bundle's reading: a wall with its Piece (prefab, place, a cost named by a token with no text), a dome without
    /// one (loose, no box), and a piece that is no networked prefab.
    /// </summary>
    private static Dictionary<string, string> Bundle() => new()
    {
        ["prefabs"] = """{"zz_wall":{"p":1,"d":0,"t":2}}""",
        ["pieces"] = """
            {"zz_wall":{"cat":2,"comfort":0,"snap":[[-1,0,0],[1,0,0]],"box":[-1,1,-0.1,0.1,0,2]},
             "zz_dome":{"cat":2,"comfort":0,"snap":[[-2,0,-2],[2,0,2],[0,3,0]],"loose":1,"tool":"hammer"},
             "zz_helper":{"cat":2,"comfort":0}}
            """,
        ["piece-place"] = """{"zz_wall":{"f":{},"s":[[-1,0,0],[1,0,0]],"c":[]}}""",
        ["piece-support"] = """{"zz_wall":{"m":0,"s":1,"c":[0,0,0],"b":[]}}""",
        ["piece-cost"] = """{"pieces":{"zz_wall":{"n":"$piece_zz_wall","r":[["Wood",2,1]],"st":"Workbench"}},"items":{"Wood":"Wood","Stone":"Stone"}}""",
    };

    [Fact]
    public void ABundlesPiecesAreTakenWithTheirMod()
    {
        var data = ModScan.Data.Empty;

        ModScan.Take(data, Bundle(), "Zeta Mod");

        Assert.Equal(["zz_wall", "zz_dome"], data.Mods.Keys.Order(StringComparer.Ordinal).Reverse());
        Assert.All(data.Mods.Values, m => Assert.Equal("Zeta Mod", m));
        Assert.Equal("hammer", data.Entries["pieces"]["zz_wall"]["tool"]!.GetValue<string>());
        Assert.Null(data.Entries["pieces"]["zz_dome"]["loose"]);
        // Without a Piece: saved as any piece, placed by its snap points and their box.
        Assert.Equal(2, data.Entries["prefabs"]["zz_dome"]["t"]!.GetValue<int>());
        Assert.Equal(3, data.Entries["piece-place"]["zz_dome"]["s"]!.AsArray().Count);
        Assert.True(data.Entries["piece-support"].ContainsKey("zz_wall"));
        Assert.False(data.Entries["piece-support"].ContainsKey("zz_dome"));
        // A token with no English text: no name (the prefab name shows); its items named.
        Assert.Null(data.Entries["piece-cost"]["zz_wall"]["n"]);
        Assert.Equal("Wood", data.Items["Wood"]);
        Assert.False(data.Items.ContainsKey("Stone"));
    }

    [Fact]
    public void APieceWithoutAPieceIsPlacedByItsBoxElseItsSnapPoints()
    {
        var boxed = ModScan.PlaceOf(JsonNode.Parse("""{"snap":[[0,0,0]],"box":[-1,1,-0.2,0.2,0,3]}""")!.AsObject())!;
        Assert.Equal("""["b",0,1.5,0,1,1.5,0.2,0,0,0,1]""", boxed["c"]![0]!.ToJsonString());
        // Flat snap points: 10 cm thick at least.
        var flat = ModScan.PlaceOf(JsonNode.Parse("""{"snap":[[-2,0,0],[2,0,0]]}""")!.AsObject())!;
        Assert.Equal("""["b",0,0,0,2,0.05,0.05,0,0,0,1]""", flat["c"]![0]!.ToJsonString());
        Assert.Null(ModScan.PlaceOf(JsonNode.Parse("""{"box":[0,1,0,1,0,1]}""")!.AsObject()));
    }

    [Fact]
    public void ThePiecesAreLaidUnderTheGamesDataAndALiveGamesReport()
    {
        GameData.UseCopies(Fixtures.Root);
        var data = ModScan.Data.Empty;
        ModScan.Take(data, Bundle(), "Zeta Mod");
        Assert.Same("{}", ModScan.Merge("prefabs", "{}"));

        GameData.UseModScan(data);

        int wall = StableHash.Of("zz_wall");
        Assert.Equal("zz_wall", PrefabCatalog.Get(wall)?.Name);
        Assert.Equal("hammer", PieceCatalog.Get(wall)?.Tool);
        Assert.True(ModPrefabs.IsModded("zz_wall"));
        Assert.Equal("Zeta Mod", ModPrefabs.ModOf("zz_wall"));
        Assert.False(ModPrefabs.IsModded("woodwall"));
        Assert.Null(ModPrefabs.ModOf("woodwall"));
        // The game's own entry stays: a reading never changes it.
        Assert.Equal("""{"x":1}""", ModScan.Merge("zdo-keys", """{"x":1}"""));
        var game = JsonNode.Parse(ModScan.Merge("prefabs", """{"zz_wall":{"p":0,"d":1,"t":0}}"""))!;
        Assert.Equal(0, game["zz_wall"]!["t"]!.GetValue<int>());
        Assert.Equal(2, game["zz_dome"]!["t"]!.GetValue<int>());
        // A live game's word on the same piece wins (its tool, its mod).
        GameData.AddMods("""
            {"prefabs":{"zz_wall":{"p":1,"d":0,"t":2}},"pieces":{"zz_wall":{"cat":4,"comfort":0,"tool":"ZetaHammer"}},"mods":{"zz_wall":"Zeta Live"}}
            """);
        Assert.Equal("ZetaHammer", PieceCatalog.Get(wall)?.Tool);
        Assert.Equal("Zeta Live", ModPrefabs.ModOf("zz_wall"));
    }

    [Fact]
    public void AModIsNamedFromItsFolder()
    {
        string plugins = Path.Combine(_dir, "BepInEx", "plugins");
        string fancy = Path.Combine(plugins, "Author-Fancy_Mod");
        Directory.CreateDirectory(Path.Combine(fancy, "lib"));
        File.WriteAllText(Path.Combine(fancy, "manifest.json"), """{"name":"Fancy_Mod","version_number":"1.0.0"}""");
        string other = Path.Combine(plugins, "Someone-Thing");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "manifest.json"), "not json");

        Assert.Equal("Fancy Mod", ModScan.ModName(Path.Combine(fancy, "lib", "Fancy.dll")));
        Assert.Equal(fancy, ModScan.ModFolder(Path.Combine(fancy, "lib", "Fancy.dll")));
        Assert.Equal("Thing", ModScan.ModName(Path.Combine(other, "Thing.dll")));
        Assert.Equal("Loose", ModScan.ModName(Path.Combine(plugins, "Loose.dll")));
        // Not under any plugins folder: its own folder.
        Assert.Equal(_dir, ModScan.ModFolder(Path.Combine(_dir, "x.dll")));
    }

    [Fact]
    public void AModsEnglishTextsAreRead()
    {
        string mod = Path.Combine(_dir, "mod");
        Directory.CreateDirectory(Path.Combine(mod, "Translations", "English"));
        File.WriteAllText(Path.Combine(mod, "Mod.English.json"), """{"piece_zz_wall":"Zeta wall","count":3}""");
        File.WriteAllText(Path.Combine(mod, "Translations", "English", "pieces.json"), """{"$piece_zz_dome":"Zeta dome"}""");
        File.WriteAllText(Path.Combine(mod, "Mod.German.json"), """{"piece_zz_wall":"Zeta-Wand"}""");
        File.WriteAllText(Path.Combine(mod, "Broken.English.json"), "{");

        var texts = ModScan.Texts(mod).ToList();

        Assert.Contains(("piece_zz_wall", "Zeta wall"), texts);
        Assert.Contains(("piece_zz_dome", "Zeta dome"), texts);
        Assert.Equal(2, texts.Count);
        Assert.Empty(ModScan.Texts(Path.Combine(_dir, "none")));
    }

    [Fact]
    public void TheManifestsPiecesAreAddedAndTheReadingKept()
    {
        string plugins = Path.Combine(_dir, "BepInEx", "plugins", "Author-RailMod");
        Directory.CreateDirectory(plugins);
        File.WriteAllText(Path.Combine(plugins, ModManifests.FileName), """
            { "version": 1, "prefabs": {
                "rm_track": { "group": "Track", "piece": { "name": "Track 2 m", "cost": { "Stone": 4 }, "station": "Stonecutter" } },
                "rm_post": { "piece": { "name": "Post" } },
                "rm_hidden": { "hidden": true, "piece": { "name": "Hidden" } },
                "woodwall": { "piece": { "name": "Not a mod's" } } } }
            """);
        var settings = new AppSettings { BepInExFolders = [Path.Combine(_dir, "BepInEx")] };
        ModManifests.Refresh(settings);

        var data = ModScan.Scan(settings, Path.Combine(_dir, "no game"), new HashSet<string> { "woodwall" });

        Assert.Equal("Track 2 m", data.Entries["piece-cost"]["rm_track"]["n"]!.GetValue<string>());
        Assert.Equal("""[["Stone",4,1]]""", data.Entries["piece-cost"]["rm_track"]["r"]!.ToJsonString());
        Assert.Equal("Stonecutter", data.Entries["piece-cost"]["rm_track"]["st"]!.GetValue<string>());
        Assert.Null(data.Entries["piece-cost"]["rm_post"]["st"]);
        Assert.Equal("Author-RailMod", data.Mods["rm_track"]);
        Assert.False(data.Mods.ContainsKey("rm_hidden"));
        Assert.False(data.Mods.ContainsKey("woodwall"));
        // The bundles' reading is kept (none here), and read back for the same mods.
        Assert.True(File.Exists(ModScan.FilePath));
        var again = ModScan.Scan(settings, Path.Combine(_dir, "no game"), new HashSet<string>());
        Assert.True(again.Mods.ContainsKey("rm_track"));
        // A kept file that cannot be read is read from the mods again.
        File.WriteAllText(ModScan.FilePath, "{");
        Assert.True(ModScan.Scan(settings, Path.Combine(_dir, "no game"), new HashSet<string>()).Mods.ContainsKey("rm_post"));
    }
}
