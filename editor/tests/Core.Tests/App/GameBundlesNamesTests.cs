using AssetsTools.NET.Extra;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// The names of meshes, materials and textures found by name in the game's bundles, kept beside the index until the bundles
/// change, and the key a found asset is written as.
/// </summary>
public class GameBundlesNamesTests
{
    [Fact]
    public void MeshesMaterialsAndTexturesAreFoundByName()
    {
        using var game = new TestGame();
        var a = game.Bundle("a_mod");
        long plank = Quad("plank").Add(a);
        long wood = TestAssets.Material(a, "wood");
        long bark = TestAssets.Texture(a, "bark", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        // The same name twice in one bundle, and again in another.
        long plankAgain = Quad("plank", split: true).Add(a);
        var b = game.Bundle("b_mod");
        long plankThere = Quad("plank").Add(b);
        // A prefab's GameObject is not one of the kinds found.
        b.Prefab("Bench");
        game.Save();
        var bundles = game.Bundles();

        Assert.Equal([new GameBundles.Where("a_mod", plank), new GameBundles.Where("a_mod", plankAgain), new GameBundles.Where("b_mod", plankThere)], bundles.Named(AssetClassID.Mesh, "plank"));
        Assert.Equal([new GameBundles.Where("a_mod", wood)], bundles.Named(AssetClassID.Material, "wood"));
        Assert.Equal([new GameBundles.Where("a_mod", bark)], bundles.Named(AssetClassID.Texture2D, "bark"));
        // Another kind of that name, a name nothing has, a kind not looked for: none.
        Assert.Empty(bundles.Named(AssetClassID.Material, "plank"));
        Assert.Empty(bundles.Named(AssetClassID.Mesh, "nothing"));
        Assert.Empty(bundles.Named(AssetClassID.GameObject, "Bench"));
    }

    [Fact]
    public void TheNamesAreKeptBesideTheIndexUntilTheBundlesChange()
    {
        using var game = new TestGame();
        var a = game.Bundle("a_mod");
        long plank = Quad("plank").Add(a);
        game.Save();
        string cache = Path.Combine(game.Root, "index.json");
        string names = Path.Combine(game.Root, "index-names.json");

        // Nothing kept without an index file.
        game.Bundles().EnsureNamesRead();
        Assert.False(File.Exists(names));
        game.Bundles(cache).EnsureNamesRead();
        Assert.True(File.Exists(names));

        // Read again from the kept file: what it says is taken (here a name only it has).
        string kept = File.ReadAllText(names).Replace("\"Names\":{", $"\"Names\":{{\"{(int)AssetClassID.Mesh}:FromTheFile\":[\"a_mod:{plank}\"],", StringComparison.Ordinal);
        File.WriteAllText(names, kept);
        var again = game.Bundles(cache);
        Assert.Equal([new GameBundles.Where("a_mod", plank)], again.Named(AssetClassID.Mesh, "FromTheFile"));
        Assert.Single(again.Named(AssetClassID.Mesh, "plank"));
        // A bundle changed: read again.
        File.SetLastWriteTimeUtc(Path.Combine(game.BundlesDir, "a_mod"), DateTime.UtcNow.AddDays(-3));
        Assert.Empty(game.Bundles(cache).Named(AssetClassID.Mesh, "FromTheFile"));
        // A kept file that cannot be read: read again, and kept anew.
        File.WriteAllText(names, "{ not json");
        Assert.Single(game.Bundles(cache).Named(AssetClassID.Mesh, "plank"));
        Assert.StartsWith("{\"Bundles\":", File.ReadAllText(names), StringComparison.Ordinal);
    }

    [Fact]
    public void WhereRoundTripsThroughItsKey()
    {
        var w = new GameBundles.Where("some:bundle", -42);
        Assert.Equal("some:bundle:-42", w.Key);
        Assert.Equal(w, GameBundles.Where.FromKey(w.Key));
        Assert.Null(GameBundles.Where.FromKey("no-number:x"));
        Assert.Null(GameBundles.Where.FromKey(":5"));
    }
}
