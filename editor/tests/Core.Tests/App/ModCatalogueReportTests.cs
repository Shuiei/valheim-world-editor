using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mod prefab reports the game sends (WorldEditorBridge's GET /catalogue): only those that are not the game's own are kept,
/// in the data folder, the newest report of each winning; entries not in the editor's format are left out, and a kept
/// file that cannot be read is started without.
/// </summary>
[Collection("DataDir")]
public sealed class ModCatalogueReportTests : ModCatalogueTestBase
{
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

        ModCatalogue.Load(Vanilla);

        Assert.True(File.Exists(ModCatalogue.FilePath));
        Assert.Equal(2, ModCatalogue.Count);
        Assert.Equal("FineWoodPieces", ModCatalogue.ModOf("bfp_finewall"));
        // Only the items the mods' costs take are kept.
        var kept = JsonNode.Parse(File.ReadAllText(ModCatalogue.FilePath))!;
        Assert.Equal(["BFP_Plank"], kept["cost"]!["items"]!.AsObject().Select(kv => kv.Key));
    }

    [Fact]
    public void PrefabsThatBecameTheGamesOwnAreNoLongerModded()
    {
        ModCatalogue.Take(Report(), Vanilla);
        HashSet<string> updated = ["wood_fence", "bfp_finewall"];

        // Read back after a game update: left out at once, though the kept file still has it.
        ModCatalogue.Forget();
        ModCatalogue.Load(updated);
        Assert.False(ModCatalogue.IsModded("bfp_finewall"));
        Assert.Null(ModCatalogue.ModOf("bfp_finewall"));

        // The next report leaves it out of the kept file too.
        ModCatalogue.Load(Vanilla);
        Assert.True(ModCatalogue.Take("""{"prefabs":{}}""", updated).Changed);
        Assert.Equal(["vfx_bfp_hit"], ModCatalogue.Names);
        ModCatalogue.Load(Vanilla);
        Assert.False(ModCatalogue.IsModded("bfp_finewall"));
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

        ModCatalogue.Load(Vanilla);

        Assert.Equal(0, ModCatalogue.Count);
    }
}
