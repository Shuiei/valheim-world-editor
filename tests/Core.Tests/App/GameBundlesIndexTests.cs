using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// The index of the game's bundles (GameBundles): the prefabs and named assets it lists and the bundle each is in, the
/// index kept in its file until the bundles change, bundles of different Unity versions read together, and an unusual
/// bundle skipped while the others are indexed.
/// </summary>
public class GameBundlesIndexTests
{
    [Fact]
    public void TheIndexListsPrefabsAndNamedAssetsAndIsKeptUntilTheBundlesChange()
    {
        using var game = new TestGame();
        var a = game.Bundle("a_models");
        a.Prefab("Rock", r => r.Mono("ZNetView", new Fields { ["m_persistent"] = true }));
        // A model asset of the same name as a networked prefab in another bundle: the networked one is the prefab.
        a.Prefab("Table", r => r.Child("legs"));
        a.Contents("assets/terrain/Heightmap_basematerial.mat", TestAssets.Material(a, "Heightmap_basematerial"));
        a.Contents("assets/other/notes.txt", 7);
        var b = game.Bundle("b_pieces");
        b.Prefab("Table", r => r.Mono("ZNetView", new Fields { ["m_persistent"] = true, ["m_distant"] = true, ["m_type"] = 1 }));
        // Twice, neither networked: the first bundle's.
        a.Prefab("Bush");
        b.Prefab("Bush");
        game.Save();
        // Files that are not bundles are passed over.
        File.WriteAllText(Path.Combine(game.BundlesDir, "readme.txt"), "not a bundle");
        File.WriteAllBytes(Path.Combine(game.BundlesDir, "empty"), []);
        string cache = Path.Combine(game.Root, "index.json");

        var bundles = GameBundles.ForGame(game.Root, cache)!;
        Assert.Equal(game.BundlesDir, bundles.Folder);
        Assert.Equal(4, bundles.BundleCount);
        Assert.True(bundles.HasBundle("a_models"));
        Assert.Equal(Path.Combine(game.BundlesDir, "b_pieces"), bundles.PathOf("b_pieces"));
        Assert.Null(bundles.PathOf("nothing"));
        Assert.False(bundles.Indexed);
        var index = bundles.Index;
        Assert.True(bundles.Indexed);
        Assert.Equal(["Bush", "Rock", "Table"], index.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("b_pieces", index["Table"].Bundle);
        Assert.Equal("a_models", index["Bush"].Bundle);
        Assert.Equal("a_models", bundles.Asset("Heightmap_basematerial.mat")!.Bundle);
        Assert.Null(bundles.Asset("minimap.mat"));
        Assert.Equal("Rock", bundles.NameOf(StableHash.Of("Rock")));
        Assert.Null(bundles.NameOf(StableHash.Of("Nothing")));
        Assert.True(File.Exists(cache));

        // Read again from the kept file: what it says is taken (here a name only it has).
        string kept = File.ReadAllText(cache).Replace("\"Rock\":", "\"FromTheFile\":\"a_models:3\",\"Rock\":", StringComparison.Ordinal);
        File.WriteAllText(cache, kept);
        var again = new GameBundles(game.BundlesDir, cache);
        Assert.Contains("FromTheFile", again.Index.Keys);
        Assert.Equal("a_models", again.Asset("Heightmap_basematerial.mat")!.Bundle);
        // A bundle changed: built again.
        File.SetLastWriteTimeUtc(Path.Combine(game.BundlesDir, "a_models"), DateTime.UtcNow.AddDays(-3));
        Assert.DoesNotContain("FromTheFile", new GameBundles(game.BundlesDir, cache).Index.Keys);
        // A kept file that cannot be read: built again.
        File.WriteAllText(cache, "{ not json");
        Assert.Equal(3, new GameBundles(game.BundlesDir, cache).Index.Count);
        // No folder of bundles: no reader.
        Assert.Null(GameBundles.ForGame(null, null));
        Assert.Null(GameBundles.ForGame(Path.Combine(game.Root, "valheim_Data"), null));
    }

    [Fact]
    public void AnUnusualBundleIsSkippedAndTheOthersIndexed()
    {
        using var game = new TestGame();
        // A bundle that starts with a resource file (no assets file to read), and one whose table of contents is cut
        // short (its name, then nothing): neither stops the index, and the usual bundle after them is read.
        var odd = game.Bundle("b_table");
        odd.WithContents = false;
        odd.Assets.AddRaw(UnityClass.AssetBundle, UnityTypes.AssetBundle, [1, 0, 0, 0, (byte)'b']);
        var usual = game.Bundle("c_models");
        var mats = game.Bundle("d_materials");
        long stone = TestAssets.Material(mats, "stone");
        long quad = Quad("quad").Add(usual);
        usual.Prefab("Rock", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Renderer(Fields.Ref(quad), [usual.RefTo(mats, stone)]);
        });
        game.Save();
        new UnityFsWriter().Resource("CAB-a_odd.resS", [1, 2, 3, 4]).Save(Path.Combine(game.BundlesDir, "a_odd"));
        File.WriteAllText(Path.Combine(game.BundlesDir, "readme.txt"), "not a bundle");
        string cache = Path.Combine(game.Root, "index.json");

        var bundles = new GameBundles(game.BundlesDir, cache);
        Assert.Equal(["Rock"], bundles.Index.Keys);
        Assert.Equal("c_models", bundles.Index["Rock"].Bundle);
        // The index read from its kept file: a reference into another bundle is followed over the same files.
        var again = new GameBundles(game.BundlesDir, cache);
        Assert.Equal(new GameBundles.Where("d_materials", stone), again.LoadModel("Rock", room: false)!.Parts[0].Material);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BundlesOfDifferentUnityVersionsReadTogether(bool otherFirst)
    {
        using var game = new TestGame();
        var plain = game.Bundle("plain_mod");
        long mesh = new TestMesh { Name = "plank", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]] }.Add(plain);
        var older = game.Bundle("older_mod");
        long other = new TestMesh { Name = "plank", AnotherVersion = true, Positions = [0, 0, 0, 2, 0, 0, 0, 2, 0, 2, 2, 0], Submeshes = [[0, 1, 2, 1, 3, 2]] }.Add(older);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null, mixedVersions: true);
        GameBundles.Where a = new("plain_mod", mesh), b = new("older_mod", other);

        // Either read first, each with its own bundle's layout.
        foreach (var at in otherFirst ? [b, a] : (GameBundles.Where[])[a, b])
        {
            Assert.Equal(at == a ? 3 : 4, bundles.VertexCountOf(at));
            Assert.Equal(at == a ? 3 : 4, bundles.LoadMesh(at)!.VertexCount);
        }
        Assert.NotNull(bundles.BoundsOf(b));
    }
}
