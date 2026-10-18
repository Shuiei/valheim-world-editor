using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests.Unity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's models made ready for the view (BundleModels) from a made-up game's bundles: which saved objects are
/// drawn with a model of their own, a model's parts, its meshes, materials and textures by id, and nothing (not an
/// error) for what is not there.
/// </summary>
public class BundleModelsGameTests
{
    /// <summary>
    /// RGBA32.
    /// </summary>
    private const int Rgba32 = 4;

    /// <summary>
    /// A game with networked objects of every kind, a location, a model with a mesh, a material and a texture.
    /// </summary>
    private static TestGame Game(out long mesh, out long material, out long texture)
    {
        var game = new TestGame();
        var b = game.Bundle("models");
        long m = new TestMesh { Name = "box", Positions = [0, 0, 1, 1, 0, 1, 0, 1, 1], Submeshes = [[0, 1, 2]] }.Add(b);
        // 2 x 1: an opaque red pixel and a transparent one (bled red when cut out).
        long t = TestAssets.Texture(b, "leaf", 2, 1, Rgba32, 1, [255, 0, 0, 255, 0, 0, 0, 0]);
        long mat = TestAssets.Material(b, "leaf_mat", textures: [("_MainTex", Fields.Ref(t), (1, 1), (0, 0))], floats: [("_Mode", 1)]);
        static void Net(PrefabNode r) => r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
        b.Prefab("vwe_test_barrel", r =>
        {
            Net(r);
            r.Child("body", c =>
            {
                c.Position = (0, 1, 0);
                c.Renderer(Fields.Ref(m), [Fields.Ref(mat)]);
            });
        });
        b.Prefab("vwe_test_troll", r =>
        {
            Net(r);
            r.Mono("Character", new Fields { ["m_runSpeed"] = 3.0 });
        });
        b.Prefab("Boar", r =>
        {
            Net(r);
            r.Mono("Character", new Fields { ["m_runSpeed"] = 5.0 });
        });
        b.Prefab("vwe_test_coin", r =>
        {
            Net(r);
            r.Mono("ItemDrop");
        });
        b.Prefab("vwe_test_ragdoll", Net);
        b.Prefab("Runestone_vwe_test");
        b.Prefab("vwe_test_rock");
        // A location: its own rocks, and a chest saved as an object of its own.
        b.Prefab("vwe_test_camp", r =>
        {
            r.Mono("Location", new Fields { ["m_exteriorRadius"] = 10.0 });
            r.Child("rocks", c => c.Renderer(Fields.Ref(m), [Fields.Ref(mat)]));
            r.Child("chest", c =>
            {
                Net(c);
                c.Renderer(Fields.Ref(m), [Fields.Ref(mat)]);
            });
        });
        game.Save();
        (mesh, material, texture) = (m, mat, t);
        return game;
    }

    [Fact]
    public void SavedObjectsWithAModelOfTheirOwnAreNamed()
    {
        using var game = Game(out _, out _, out _);
        var models = BundleModels.For(game.Bundles())!;
        string? Name(string prefab) => models.ObjectName(StableHash.Of(prefab));
        Assert.Equal("vwe_test_barrel", Name("vwe_test_barrel"));
        // Creatures, items and ragdolls are not, but tameable creatures and runestones are.
        Assert.Null(Name("vwe_test_troll"));
        Assert.Equal("Boar", Name("Boar"));
        Assert.Null(Name("vwe_test_coin"));
        Assert.Null(Name("vwe_test_ragdoll"));
        Assert.Equal("Runestone_vwe_test", Name("Runestone_vwe_test"));
        // Locations are, for their parts the game makes again from a location proxy.
        Assert.Equal("vwe_test_camp", Name("vwe_test_camp"));
        // Not networked, or not in the game.
        Assert.Null(Name("vwe_test_rock"));
        Assert.Null(Name("vwe_test_nothing"));
        // Asked again: the same.
        Assert.Equal("vwe_test_barrel", Name("vwe_test_barrel"));
    }

    [Fact]
    public void AModelsPartsMeshesMaterialsAndTexturesAreReadyForTheView()
    {
        using var game = Game(out long mesh, out long material, out long texture);
        var models = BundleModels.For(game.Bundles())!;

        var model = models.Model("vwe_test_barrel")!;
        var part = Assert.Single(model.Parts);
        Assert.Equal($"models:{mesh}", part.Mesh);
        Assert.Equal($"models:{material}", part.Material);
        Assert.Equal(new Vector3(0, 1, 0), Vector3.Transform(Vector3.Zero, part.Matrix));
        Assert.Equal(Vector3.One, model.RootScale);
        Assert.Null(models.Model("vwe_test_nothing"));
        // A location is read as a room: without its saved objects (the chest), drawn as objects of their own.
        Assert.Single(models.Model("vwe_test_camp")!.Parts);

        // z mirrored, an up normal (the mesh has none).
        var m = models.Mesh($"models:{mesh}")!;
        Assert.Equal(new[] { 0f, 0f, -1f, 0f, 1f, 0f, 0f, 0f }, m.Vertices[..8]);
        Assert.Equal(new uint[] { 0, 1, 2 }, m.Submeshes[0]);
        Assert.Null(models.Mesh("not an id"));
        Assert.Null(models.Mesh("models:4242"));

        // Cut out (_Mode 1): its texture bled.
        var mat = models.Material($"models:{material}");
        Assert.Equal($"models:{texture}|cut", mat.Map);
        Assert.Equal(0.5f, mat.Cutoff);
        // Not there: plain white.
        Assert.Equal(Vector4.One, models.Material("models:4242").Color);
        Assert.Null(models.Material("nothing").Map);

        var plain = models.Texture($"models:{texture}")!;
        Assert.Equal((2, 1), (plain.Width, plain.Height));
        Assert.Equal([255, 0, 0, 255, 0, 0, 0, 0], plain.Rgba);
        var bled = models.Texture($"models:{texture}|cut")!;
        Assert.Equal([255, 0, 0, 255, 255, 0, 0, 0], bled.Rgba);
        Assert.Null(models.Texture("models:4242"));
        Assert.Null(models.Texture("nothing|cut"));
    }

    [Fact]
    public void NoBundlesGiveNoModels()
    {
        Assert.Null(BundleModels.For(null));
        using var empty = new TestGame();
        empty.Save();
        Assert.Null(BundleModels.For(empty.Bundles()));
    }
}
