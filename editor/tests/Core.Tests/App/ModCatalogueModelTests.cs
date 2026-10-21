using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The models the game reports of the mods' prefabs (POST /models): taken for known mod prefabs only, read back by name, kept
/// with the catalogue, and each change counted.
/// </summary>
[Collection("DataDir")]
public sealed class ModCatalogueModelTests : ModCatalogueTestBase
{
    private static readonly float[] Expected2 = [2f, 1.5f, 1f];
    private static readonly float[] Expected1 = [0f, 1.5f, 0f, 1f, 1.5f, 0.25f];
    [Fact]
    public void OnlyTheModelsOfKnownModPrefabsAreTaken()
    {
        ModCatalogue.Take(Report(), Vanilla);
        string model = ModelJson("1,1,1", PartJson("wall", 24, 0, "fine_wood"));

        int taken = ModCatalogue.TakeModels($$"""{"bfp_finewall":{{model}},"wood_fence":{{model}},"nothing_known":{{model}}}""");

        Assert.Equal(1, taken);
        Assert.NotNull(ModCatalogue.ModelOf("bfp_finewall"));
        Assert.Null(ModCatalogue.ModelOf("wood_fence"));
        Assert.Null(ModCatalogue.ModelOf("nothing_known"));
    }

    [Fact]
    public void AModelGivesItsPartsAsTheGameDrawsThem()
    {
        ModCatalogue.Take(Report(), Vanilla);
        ModCatalogue.TakeModels($$"""{"bfp_finewall":{{ModelJson("2,1.5,1", PartJson("wall Instance", 24, 1, "fine_wood (Instance)"), PartJson("trim", 8, 0, null))}}}""");

        var model = ModCatalogue.ModelOf("bfp_finewall")!;

        Assert.Equal(Expected2, model.RootScale);
        Assert.Equal(2, model.Parts.Count);
        var wall = model.Parts[0];
        Assert.Equal(("wall", 24, 1, "fine_wood"), (wall.Mesh, wall.Vertices, wall.Sub, wall.Material));
        Assert.Equal(new[] { 1f, 0, 0, 1, 0, 1, 0, 2, 0, 0, 1, 3, 0, 0, 0, 1 }, wall.Matrix);
        Assert.Null(model.Parts[1].Material);
        Assert.Null(ModCatalogue.ModelOf("vfx_bfp_hit"));
        // Read into records once, when taken: never from the shared JSON by the threads that ask.
        Assert.Same(model, ModCatalogue.ModelOf("bfp_finewall"));
    }

    [Fact]
    public void TheGenerationCountsEachChangeOfWhatIsKnown()
    {
        int before = ModCatalogue.Generation;

        ModCatalogue.Take(Report(), Vanilla);
        Assert.Equal(before + 1, ModCatalogue.Generation);
        // Nothing new: the same.
        ModCatalogue.Take(Report(), Vanilla);
        Assert.Equal(before + 1, ModCatalogue.Generation);
        ModCatalogue.TakeModels($$"""{"bfp_finewall":{{ModelJson("1,1,1")}}}""");
        Assert.Equal(before + 2, ModCatalogue.Generation);
        ModCatalogue.Load(Vanilla);
        ModCatalogue.Forget();
        Assert.Equal(before + 4, ModCatalogue.Generation);
    }

    [Fact]
    public void ReportsAndModelsTakenAtOnceLoseNoChange()
    {
        const int Prefabs = 40;
        string names = string.Join(",", Enumerable.Range(0, Prefabs).Select(i => $"\"mod_piece{i}\":{{\"p\":1,\"d\":0,\"t\":2}}"));
        string Costed(int wood) => "{\"prefabs\":{" + names + "},\"cost\":{\"pieces\":{\"mod_piece0\":{\"n\":\"Piece\",\"r\":[[\"Wood\"," + wood + ",1]]}}}}";
        ModCatalogue.Take(Costed(0), Vanilla);
        int before = ModCatalogue.Generation;

        // Each report changes a cost and each model is new: every call is a change, and none may undo another.
        Parallel.For(0, 2 * Prefabs, i =>
        {
            if (i % 2 == 0)
            {
                ModCatalogue.Take(Costed(i + 1), Vanilla);
            }
            else
            {
                ModCatalogue.TakeModels($$"""{"mod_piece{{i / 2}}":{{ModelJson("1,1,1")}}}""");
            }
        });

        Assert.All(Enumerable.Range(0, Prefabs), i => Assert.NotNull(ModCatalogue.ModelOf($"mod_piece{i}")));
        Assert.Equal(before + (2 * Prefabs), ModCatalogue.Generation);
    }

    [Fact]
    public void APartGivesItsMeshsBoundsWhenTheGameToldThem()
    {
        ModCatalogue.Take(Report(), Vanilla);
        const string Identity = "1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1";
        ModCatalogue.TakeModels($$$"""
            {"bfp_finewall":{"s":[1,1,1],"p":[
              {"mesh":"wall","v":24,"sub":0,"b":[0,1.5,0,1,1.5,0.25],"m":[{{{Identity}}}]},
              {"mesh":"trim","v":8,"sub":0,"m":[{{{Identity}}}]}
            ]}}
            """);

        var parts = ModCatalogue.ModelOf("bfp_finewall")!.Parts;

        Assert.Equal(Expected1, parts[0].Bounds);
        Assert.Null(parts[1].Bounds);
    }

    [Theory]
    [InlineData("wall", "wall")]
    [InlineData("wall Instance", "wall")]
    [InlineData("fine_wood (Instance)", "fine_wood")]
    [InlineData("fine_wood (Instance) (Instance)", "fine_wood")]
    [InlineData("wall Instance Instance", "wall")]
    [InlineData("Instance", "Instance")]
    public void APlainNameIsWithoutWhatUnityAddsToCopies(string name, string plain)
    {
        Assert.Equal(plain, ModCatalogue.Plain(name));
    }

    [Theory]
    [InlineData("""[1,2]""")]
    [InlineData("""{"s":[1,1],"p":[]}""")]
    [InlineData("""{"s":[1,1,"1"],"p":[]}""")]
    [InlineData("""{"s":[1,1,1]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"v":3,"sub":0,"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3.5,"sub":0,"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3,"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3,"sub":0,"mat":4,"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3,"sub":0,"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[3]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3,"sub":0,"b":[0,0,0,1,1],"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3,"sub":0,"b":[0,0,0,1,1,"1"],"m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    [InlineData("""{"s":[1,1,1],"p":[{"mesh":"x","v":3,"sub":0,"b":"box","m":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}]}""")]
    public void ModelsNotInTheExpectedFormAreLeftOut(string model)
    {
        ModCatalogue.Take(Report(), Vanilla);

        int taken = ModCatalogue.TakeModels($$"""{"bfp_finewall":{{model}},"vfx_bfp_hit":{{ModelJson("1,1,1")}}}""");

        Assert.Equal(1, taken);
        Assert.Null(ModCatalogue.ModelOf("bfp_finewall"));
        Assert.Empty(ModCatalogue.ModelOf("vfx_bfp_hit")!.Parts);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void ModelsThatAreNotAJsonObjectThrow(string json)
    {
        Assert.ThrowsAny<JsonException>(() => ModCatalogue.TakeModels(json));
    }

    [Fact]
    public void TheModelsAreKeptWithTheCatalogue()
    {
        ModCatalogue.Take(Report(), Vanilla);
        string models = $$"""{"bfp_finewall":{{ModelJson("1,1,1", PartJson("wall", 24, 0, "fine_wood"))}}}""";
        ModCatalogue.TakeModels(models);
        DateTime written = File.GetLastWriteTimeUtc(ModCatalogue.FilePath);

        // The same again: taken, nothing written.
        Assert.Equal(1, ModCatalogue.TakeModels(models));
        Assert.Equal(written, File.GetLastWriteTimeUtc(ModCatalogue.FilePath));
        ModCatalogue.Forget();
        ModCatalogue.Load(Vanilla);

        Assert.Equal("wall", ModCatalogue.ModelOf("bfp_finewall")!.Parts[0].Mesh);
    }

    [Fact]
    public void ANewerReportKeepsTheModelsThereWere()
    {
        ModCatalogue.Take(Report(), Vanilla);
        ModCatalogue.TakeModels($$"""{"bfp_finewall":{{ModelJson("1,1,1", PartJson("wall", 24, 0, "fine_wood"))}}}""");

        ModCatalogue.Take(Report("[[\"Wood\",2,1]]"), Vanilla);

        Assert.Equal("wall", ModCatalogue.ModelOf("bfp_finewall")!.Parts[0].Mesh);
    }
}
