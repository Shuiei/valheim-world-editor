using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' prefabs a game reports (WorldEditorBridge's GET /catalogue): only those that are not the game's own are
/// kept, in the data folder, the newest report of each winning; entries not in the editor's format are left out; they
/// are laid over the game's data without changing the game's own entries, and GameData uses them at once. Runs alone
/// (the catalogues are shared), in a data folder of its own; the tests' copies are in use again afterwards.
/// </summary>
[Collection("DataDir")]
public sealed class ModCatalogueTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-mods-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly HashSet<string> Vanilla = ["wood_fence"];

    public ModCatalogueTests()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
    }

    /// <summary>
    /// Forgets the mods, puts the data folder and the tests' copies back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// A report as the plugin writes it: a vanilla fence (as the game has it, changed by a mod), a modded wall with its
    /// piece, cost and mod, and a modded effect (not saved: no piece).
    /// </summary>
    private static string Report(string wallCost = "[[\"BFP_Plank\",4,1]]") => $$"""
        {
          "version": 1,
          "prefabs": {
            "wood_fence": {"p":1,"d":0,"t":0},
            "bfp_finewall": {"p":1,"d":0,"t":2},
            "vfx_bfp_hit": {"p":0,"d":0,"t":0}
          },
          "pieces": {
            "wood_fence": {"cat":3,"comfort":0},
            "bfp_finewall": {"cat":2,"comfort":0,"box":[-1,1,-0.1,0.1,0,2],"snap":[[-1,0,0],[1,2,0]],"tool":"BFP_FineHammer"}
          },
          "cost": {
            "pieces": {
              "wood_fence": {"n":"Fence","r":[]},
              "bfp_finewall": {"n":"Fine wood wall","r":{{wallCost}},"st":"Workbench"}
            },
            "items": {"BFP_Plank": "Fine Plank", "Wood": "Wood"}
          },
          "mods": {"bfp_finewall": "FineWoodPieces", "wood_fence": "SomeMod"}
        }
        """;

    [Fact]
    public void OnlyThePrefabsThatAreNotTheGamesOwnAreKept()
    {
        var (mods, changed) = ModCatalogue.Take(Report(), Vanilla);

        Assert.Equal(2, mods);
        Assert.True(changed);
        Assert.Equal(["bfp_finewall", "vfx_bfp_hit"], ModCatalogue.Names.Order(StringComparer.Ordinal));
        Assert.Equal("FineWoodPieces", ModCatalogue.ModOf("bfp_finewall"));
        Assert.Null(ModCatalogue.ModOf("wood_fence"));
        Assert.Null(ModCatalogue.ModOf("vfx_bfp_hit"));
        Assert.False(ModCatalogue.IsModded("wood_fence"));
    }

    [Fact]
    public void TheCatalogueIsKeptInTheDataFolder()
    {
        ModCatalogue.Take(Report(), Vanilla);
        ModCatalogue.Forget();

        ModCatalogue.Load();

        Assert.True(File.Exists(ModCatalogue.FilePath));
        Assert.Equal(2, ModCatalogue.Count);
        Assert.Equal("FineWoodPieces", ModCatalogue.ModOf("bfp_finewall"));
        // Only the items the mods' costs take are kept.
        var kept = JsonNode.Parse(File.ReadAllText(ModCatalogue.FilePath))!;
        Assert.Equal(["BFP_Plank"], kept["cost"]!["items"]!.AsObject().Select(kv => kv.Key));
    }

    [Fact]
    public void AReportThatCannotBeKeptIsNotUsed()
    {
        // A folder where the file goes: it cannot be written.
        Directory.CreateDirectory(ModCatalogue.FilePath);

        Assert.ThrowsAny<Exception>(() => ModCatalogue.Take(Report(), Vanilla));

        Assert.Equal(0, ModCatalogue.Count);
        Directory.Delete(ModCatalogue.FilePath);
        Assert.True(ModCatalogue.Take(Report(), Vanilla).Changed);
    }

    [Fact]
    public void TheSameReportAgainChangesNothing()
    {
        ModCatalogue.Take(Report(), Vanilla);
        DateTime written = File.GetLastWriteTimeUtc(ModCatalogue.FilePath);

        var (mods, changed) = ModCatalogue.Take(Report(), Vanilla);

        Assert.Equal(2, mods);
        Assert.False(changed);
        Assert.Equal(written, File.GetLastWriteTimeUtc(ModCatalogue.FilePath));
    }

    [Fact]
    public void ANewerReportReplacesItsEntriesAndKeepsTheOthers()
    {
        ModCatalogue.Take(Report(), Vanilla);
        const string Other = """{"prefabs":{"bfp_finewall":{"p":1,"d":0,"t":2}},"pieces":{},"cost":{"pieces":{"bfp_finewall":{"n":"Fine wood wall","r":[["Wood",9,1]]}},"items":{"Wood":"Wood"}}}""";

        var (mods, changed) = ModCatalogue.Take(Other, Vanilla);

        Assert.Equal(1, mods);
        Assert.True(changed);
        Assert.Equal(2, ModCatalogue.Count);
        var cost = JsonNode.Parse(ModCatalogue.Merge("piece-cost", """{"pieces":{},"items":{}}"""))!;
        Assert.Equal(9, cost["pieces"]!["bfp_finewall"]!["r"]![0]![1]!.GetValue<int>());
        Assert.Equal("Wood", cost["items"]!["Wood"]!.GetValue<string>());
        // The piece it no longer reports is gone; the mod it did not say is kept.
        Assert.Null(JsonNode.Parse(ModCatalogue.Merge("pieces", "{}"))!["bfp_finewall"]);
        Assert.Equal("FineWoodPieces", ModCatalogue.ModOf("bfp_finewall"));
    }

    [Theory]
    [InlineData("""{"p":1,"d":0}""")]
    [InlineData("""{"p":"1","d":0,"t":2}""")]
    [InlineData("""{"p":1,"d":0,"t":2,"cs":[3]}""")]
    [InlineData("""{"p":1,"d":0,"t":2,"ward":"far"}""")]
    [InlineData("""[1,0,2]""")]
    public void PrefabsNotInTheEditorsFormatAreLeftOut(string entry)
    {
        ModCatalogue.Take("""{"prefabs":{"bad":""" + entry + ""","good":{"p":1,"d":0,"t":2}}}""", Vanilla);

        Assert.Equal(["good"], ModCatalogue.Names);
    }

    [Theory]
    [InlineData("""{"comfort":0}""")]
    [InlineData("""{"cat":1,"box":[0,1,0,1]}""")]
    [InlineData("""{"cat":1,"snap":[[0,1]]}""")]
    [InlineData("""{"cat":1,"tool":3}""")]
    public void PiecesNotInTheEditorsFormatAreLeftOut(string entry)
    {
        ModCatalogue.Take("""{"prefabs":{"x":{"p":1,"d":0,"t":2}},"pieces":{"x":""" + entry + "}}", Vanilla);

        Assert.Equal(1, ModCatalogue.Count);
        Assert.Null(JsonNode.Parse(ModCatalogue.Merge("pieces", "{}"))!["x"]);
    }

    [Theory]
    [InlineData("""{"r":[]}""")]
    [InlineData("""{"n":"X","r":[["Wood"]]}""")]
    [InlineData("""{"n":"X","r":[[3,1]]}""")]
    [InlineData("""{"n":"X","r":[],"st":4}""")]
    public void CostsNotInTheEditorsFormatAreLeftOut(string entry)
    {
        ModCatalogue.Take("""{"prefabs":{"x":{"p":1,"d":0,"t":2}},"cost":{"pieces":{"x":""" + entry + """},"items":{}}}""", Vanilla);

        Assert.Null(JsonNode.Parse(ModCatalogue.Merge("piece-cost", """{"pieces":{},"items":{}}"""))!["pieces"]!["x"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void AReportThatIsNotAJsonObjectThrows(string report)
    {
        Assert.ThrowsAny<JsonException>(() => ModCatalogue.Take(report, Vanilla));
    }

    [Fact]
    public void ABrokenKeptFileIsStartedWithout()
    {
        ModCatalogue.Take(Report(), Vanilla);
        File.WriteAllText(ModCatalogue.FilePath, "{ broken");

        ModCatalogue.Load();

        Assert.Equal(0, ModCatalogue.Count);
    }

    [Fact]
    public void MergeAddsTheModsEntriesAndKeepsTheGamesOwn()
    {
        ModCatalogue.Take(Report(), Vanilla);
        // As if the game itself had no bfp wall but had a vfx_bfp_hit of its own: the game's wins.
        string game = """{"wood_fence":{"p":1,"d":0,"t":2},"vfx_bfp_hit":{"p":1,"d":1,"t":1}}""";

        var merged = JsonNode.Parse(ModCatalogue.Merge("prefabs", game))!;

        Assert.Equal(2, merged["wood_fence"]!["t"]!.GetValue<int>());
        Assert.Equal(1, merged["vfx_bfp_hit"]!["d"]!.GetValue<int>());
        Assert.Equal(2, merged["bfp_finewall"]!["t"]!.GetValue<int>());
    }

    [Fact]
    public void WithNoModsMergeReturnsTheGamesData()
    {
        const string Game = """{"wood_fence":{"p":1,"d":0,"t":2}}""";

        Assert.Same(Game, ModCatalogue.Merge("prefabs", Game));
    }

    [Fact]
    public void AKindTheModsDoNotAddToIsReturnedAsItIs()
    {
        ModCatalogue.Take(Report(), Vanilla);
        const string Json = """{"x":1}""";

        Assert.Same(Json, ModCatalogue.Merge("zdo-keys", Json));
    }

    [Fact]
    public void AddModsUsesTheModsPrefabsAtOnce()
    {
        GameData.UseCopies(Fixtures.Root);
        int fenceCategory = PieceCatalog.Get(StableHash.Of("wood_fence"))!.Category;

        int mods = GameData.AddMods(Report());

        Assert.Equal(2, mods);
        int wall = StableHash.Of("bfp_finewall");
        Assert.Equal("bfp_finewall", PrefabCatalog.Get(wall)?.Name);
        Assert.Equal("bfp_finewall", PrefabCatalog.DisplayName(wall));
        var piece = PieceCatalog.Get(wall)!;
        Assert.Equal(2, piece.Category);
        Assert.Equal("BFP_FineHammer", piece.Tool);
        Assert.Equal(2, piece.Snaps.Length);
        Assert.Equal("Fine wood wall", PieceCost.PieceName("bfp_finewall"));
        Assert.Equal("Fine Plank", PieceCost.ItemName("BFP_Plank"));
        // Not saved: known by name, not offered for placing.
        Assert.Null(PrefabCatalog.Get(StableHash.Of("vfx_bfp_hit")));
        Assert.Equal("vfx_bfp_hit", PrefabCatalog.NameOf(StableHash.Of("vfx_bfp_hit")));
        // The game's own fence is as the game has it.
        Assert.Equal(fenceCategory, PieceCatalog.Get(StableHash.Of("wood_fence"))!.Category);
        Assert.Equal("Roundpole Fence", PieceCost.PieceName("wood_fence"));
    }

    [Fact]
    public void UseCopiesKeepsTheModsPrefabsKnown()
    {
        GameData.UseCopies(Fixtures.Root);
        GameData.AddMods(Report());

        GameData.UseCopies(Fixtures.Root);

        Assert.NotNull(PieceCatalog.Get(StableHash.Of("bfp_finewall")));
    }
}
