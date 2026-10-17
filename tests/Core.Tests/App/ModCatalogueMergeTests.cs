using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' entries laid over the game's data without changing the game's own, and GameData using them at once.
/// </summary>
[Collection("DataDir")]
public sealed class ModCatalogueMergeTests : ModCatalogueTestBase
{
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
