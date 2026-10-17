using AssetsTools.NET.Extra;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's bundles read by GameBundles, on bundles made by the tests (Unity's formats, made-up objects): the index
/// of prefabs and named assets and the file it is kept in, meshes, materials and textures found by name (their names
/// kept beside the index), a mesh's vertex count and bounds, what a prefab's root has, models (what is drawn of a
/// prefab, a dungeon room), meshes (compressed ones too), materials, textures (crunched ones too), texture arrays,
/// shaders and scripts' fields. Bundles without type trees are read as having no fields rather than failing.
/// </summary>
public class GameBundlesFilesTests
{
    /// <summary>
    /// The texture formats used: RGBA32 and Alpha8.
    /// </summary>
    private const int Rgba32 = 4, Alpha8 = 1;

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
        var bundles = new GameBundles(game.BundlesDir, null);

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
        new GameBundles(game.BundlesDir, null).EnsureNamesRead();
        Assert.False(File.Exists(names));
        new GameBundles(game.BundlesDir, cache).EnsureNamesRead();
        Assert.True(File.Exists(names));

        // Read again from the kept file: what it says is taken (here a name only it has).
        string kept = File.ReadAllText(names).Replace("\"Names\":{", $"\"Names\":{{\"{(int)AssetClassID.Mesh}:FromTheFile\":[\"a_mod:{plank}\"],", StringComparison.Ordinal);
        File.WriteAllText(names, kept);
        var again = new GameBundles(game.BundlesDir, cache);
        Assert.Equal([new GameBundles.Where("a_mod", plank)], again.Named(AssetClassID.Mesh, "FromTheFile"));
        Assert.Single(again.Named(AssetClassID.Mesh, "plank"));
        // A bundle changed: read again.
        File.SetLastWriteTimeUtc(Path.Combine(game.BundlesDir, "a_mod"), DateTime.UtcNow.AddDays(-3));
        Assert.Empty(new GameBundles(game.BundlesDir, cache).Named(AssetClassID.Mesh, "FromTheFile"));
        // A kept file that cannot be read: read again, and kept anew.
        File.WriteAllText(names, "{ not json");
        Assert.Single(new GameBundles(game.BundlesDir, cache).Named(AssetClassID.Mesh, "plank"));
        Assert.StartsWith("{\"Bundles\":", File.ReadAllText(names), StringComparison.Ordinal);
    }

    [Fact]
    public void AMeshsVertexCountAndBoundsAreReadAndNothingWhenItIsNotThere()
    {
        using var game = new TestGame();
        var a = game.Bundle("a_mod");
        long quad = Quad("quad").Add(a);
        long triangle = new TestMesh { Name = "triangle", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = ((1, 2, 3), (0.5, 1, 1.5)) }.Add(a);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        Assert.Equal(4, bundles.VertexCountOf(new("a_mod", quad)));
        Assert.Equal(3, bundles.VertexCountOf(new("a_mod", triangle)));
        Assert.Equal(-1, bundles.VertexCountOf(new("a_mod", 4242)));
        // A bundle read is kept open until Close; read again after that, it is opened again.
        Assert.Equal(1, bundles.OpenBundles);
        bundles.Close();
        Assert.Equal(0, bundles.OpenBundles);
        Assert.Equal(4, bundles.VertexCountOf(new("a_mod", quad)));
        Assert.Equal(1, bundles.OpenBundles);
        Assert.Equal(new[] { 1f, 2f, 3f, 0.5f, 1f, 1.5f }, bundles.BoundsOf(new("a_mod", triangle)));
        Assert.Null(bundles.BoundsOf(new("a_mod", 4242)));
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

    [Fact]
    public void APrefabsRootTellsWhatItIs()
    {
        using var game = new TestGame();
        var b = game.Bundle("things");
        b.Prefab("Wolf", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("Character", new Fields { ["m_runSpeed"] = 7.5 });
        });
        b.Prefab("Coin", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("ItemDrop");
            // A component listed that the file does not have: passed over.
            r.Missing(999);
        });
        b.Prefab("Stone");
        b.Prefab("Ruin", r => r.Mono("Location", new Fields { ["m_exteriorRadius"] = 12.0 }));
        // A bundle whose objects' fields are not described (no type trees): its table of contents neither, so its
        // prefab is not found; read first (its name comes first), so no other bundle's type tree stands in.
        var bare = game.Bundle("bare", typeTrees: false);
        bare.Prefab("Bare", r => r.Mono("ZNetView", new Fields { ["m_persistent"] = true }));
        game.Save();
        // An index kept from before names a prefab in it: what its root has is not known either.
        string cache = Path.Combine(game.Root, "index.json");
        var bundles = new GameBundles(game.BundlesDir, cache);
        Assert.DoesNotContain("Bare", bundles.Index.Keys);
        File.WriteAllText(cache, File.ReadAllText(cache).Replace("\"Prefabs\":{", "\"Prefabs\":{\"Kept\":\"bare:1\",", StringComparison.Ordinal));
        Assert.Equal(new GameBundles.Kind(false, false, false), new GameBundles(game.BundlesDir, cache).KindOf("Kept"));

        Assert.Equal(new GameBundles.Kind(true, true, false), bundles.KindOf("Wolf"));
        Assert.Equal(new GameBundles.Kind(true, false, true), bundles.KindOf("Coin"));
        Assert.Equal(new GameBundles.Kind(false, false, false), bundles.KindOf("Stone"));
        Assert.Equal(new GameBundles.Kind(false, false, false, Location: true), bundles.KindOf("Ruin"));
        // Asked again: the same answer, kept.
        Assert.Same(bundles.KindOf("Wolf"), bundles.KindOf("Wolf"));
        Assert.Null(bundles.KindOf("Nothing"));
        Assert.Null(bundles.KindOf("Bare"));
        Assert.Empty(bundles.MonoBehaviours("bare"));
        Assert.Equal(5, bundles.MonoBehaviours("things").Count);
    }

    /// <summary>
    /// A two-triangle quad mesh (4 vertices), in one submesh unless split in two.
    /// </summary>
    private static TestMesh Quad(string name, bool split = false) => new()
    {
        Name = name,
        Positions = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0],
        Submeshes = split ? [[0, 1, 2], [0, 2, 3]] : [[0, 1, 2, 0, 2, 3]],
    };

    [Fact]
    public void AModelIsWhatTheGameDrawsOfAPrefab()
    {
        using var game = new TestGame();
        var mats = game.Bundle("a_materials");
        long shared = TestAssets.Material(mats, "shared");
        var b = game.Bundle("b_house");
        long quad = Quad("quad", split: true).Add(b);
        long single = Quad("single").Add(b);
        long own = TestAssets.Material(b, "own");
        var ownRef = Fields.Ref(own);
        var sharedRef = b.RefTo(mats, shared);
        b.Prefab("House", r =>
        {
            r.Scale = (2, 2, 2);
            long lodRenderer = 0, keptRenderer = 0;
            var fresh = r.Child("new", c =>
            {
                c.Position = (1, 0, 0);
                keptRenderer = c.Renderer(Fields.Ref(quad), [ownRef, sharedRef]);
            });
            var worn = r.Child("worn", c => c.Renderer(Fields.Ref(single), [ownRef]));
            var broken = r.Child("broken", c => c.Renderer(Fields.Ref(single), [ownRef]));
            r.Mono("WearNTear", new Fields { ["m_new"] = Fields.Ref(fresh.Go), ["m_worn"] = Fields.Ref(worn.Go), ["m_broken"] = Fields.Ref(broken.Go) });
            r.Child("far", c => lodRenderer = c.Renderer(Fields.Ref(single), [ownRef]));
            r.Component(UnityClass.LodGroup, UnityTypes.LodGroup, new Fields
            {
                ["m_LODs"] = new List<Fields>
                {
                    new() { ["renderers"] = new List<Fields> { new() { ["renderer"] = Fields.Ref(keptRenderer) } } },
                    new() { ["renderers"] = new List<Fields> { new() { ["renderer"] = Fields.Ref(lodRenderer) }, new() { ["renderer"] = Fields.Ref(0) } } },
                },
            });
            r.Child("off", c =>
            {
                c.Active = false;
                c.Renderer(Fields.Ref(single), [ownRef]);
            });
            r.Child("hidden", c => c.Renderer(Fields.Ref(single), [ownRef], enabled: false));
            // Animated: drawn in its bind pose, its renderer holding the mesh.
            r.Child("skinned", c =>
            {
                c.Position = (0, 3, 0);
                c.Component(UnityClass.SkinnedMeshRenderer, UnityTypes.SkinnedMeshRenderer, new Fields { ["m_Enabled"] = true, ["m_Materials"] = new List<Fields> { ownRef }, ["m_Mesh"] = Fields.Ref(single) });
            });
            // A material pointing into a file the bundle does not list: no material.
            r.Child("lost", c => c.Renderer(Fields.Ref(single), [Fields.Ref(5, fileId: 9)]));
        });
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var model = bundles.LoadModel("House", room: false)!;
        Assert.Equal("House", model.Name);
        Assert.Equal(new[] { 2f, 2f, 2f }, model.RootScale);
        Assert.Equal(4, model.Parts.Count);
        var (first, second) = (model.Parts[0], model.Parts[1]);
        Assert.Equal(new GameBundles.Where("b_house", quad), first.Mesh);
        Assert.Equal((0, 1), (first.Sub, second.Sub));
        Assert.Equal(new GameBundles.Where("b_house", own), first.Material);
        Assert.Equal(new GameBundles.Where("a_materials", shared), second.Material);
        // Placed relative to the root, the root's own scale left out.
        Assert.Equal(1f, first.Matrix[3]);
        Assert.Equal(1f, first.Matrix[0]);
        var skinned = model.Parts[2];
        Assert.Equal(new GameBundles.Where("b_house", single), skinned.Mesh);
        Assert.Equal(3f, skinned.Matrix[7]);
        Assert.Null(model.Parts[3].Material);
        Assert.Null(bundles.LoadModel("Nothing", room: false));
    }

    [Fact]
    public void ARoomLeavesOutItsNetworkedObjectsAndRandomParts()
    {
        using var game = new TestGame();
        var b = game.Bundle("rooms");
        long single = Quad("single").Add(b);
        long mat = TestAssets.Material(b, "stone");
        void Draw(PrefabNode c) => c.Renderer(Fields.Ref(single), [Fields.Ref(mat)]);
        b.Prefab("Room_hall", r =>
        {
            // The room's own ZNetView is not one of its contents.
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            Draw(r);
            r.Child("chest", c =>
            {
                c.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                Draw(c);
            });
            // Less than half the time: left out.
            r.Child("rare", c =>
            {
                c.Mono("RandomSpawn", new Fields { ["m_chanceToSpawn"] = 30.0 });
                Draw(c);
            });
            // Most of the time: drawn, and what shows when it is not there left out.
            var instead = r.Child("rubble", Draw);
            r.Child("common", c =>
            {
                c.Mono("RandomSpawn", new Fields { ["m_chanceToSpawn"] = 80.0, ["m_OffObject"] = Fields.Ref(instead.Go) });
                Draw(c);
            });
            r.Child("often", c => c.Mono("RandomSpawn", new Fields { ["m_chanceToSpawn"] = 90.0 }));
            // One of several: the first is drawn.
            var one = r.Child("one", Draw);
            var other = r.Child("other", Draw);
            r.Child("pick", c => c.Mono("RandomObject", new Fields
            {
                ["m_objects"] = new List<Fields> { new() { ["m_object"] = Fields.Ref(one.Go) }, new() { ["m_object"] = Fields.Ref(other.Go) }, new() { ["m_object"] = Fields.Ref(0) } },
            }));
        });
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        // The root, "common" and "one": three parts; as a plain prefab all seven that draw.
        Assert.Equal(3, bundles.LoadModel("Room_hall", room: true)!.Parts.Count);
        Assert.Equal(7, bundles.LoadModel("Room_hall", room: false)!.Parts.Count);
    }

    [Fact]
    public void MeshesAreReadInEveryLayoutTheGameUses()
    {
        using var game = new TestGame { BlockSize = 256 };
        var b = game.Bundle("meshes");
        float[] pos = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1];
        float[] normals = [0, 0, -1, 0, 1, 0, 1, 0, 0, -1, 0, 0];
        float[] uv = [0, 0, 1, 0, 1, 1, 0.5f, 0.25f];
        long plain = new TestMesh { Name = "plain", Positions = pos, Normals = normals, Uv = uv, Submeshes = [[0, 1, 2, 0, 2, 3]] }.Add(b);
        // Half floats, normals in signed bytes, UV in a second stream as 16-bit fractions; 32-bit indices from a base
        // vertex (a count not a multiple of 3 cut); a line list draws nothing.
        long packed = new TestMesh
        {
            Name = "packed",
            Positions = pos,
            Normals = normals,
            Uv = uv,
            PositionFormat = VertexFormat.Ieee16,
            NormalFormat = VertexFormat.SNorm8,
            UvFormat = VertexFormat.UNorm16,
            UvStream = true,
            Wide = true,
            Submeshes = [[0, 1, 2, 3], [0, 1], [0, 1, 2]],
            BaseVertex = [1, 0, 0],
            Topology = [0, 3, 0],
        }.Add(b);
        long bytes = new TestMesh { Name = "bytes", Positions = pos, Normals = normals, Uv = uv, NormalFormat = VertexFormat.SNorm16, UvFormat = VertexFormat.UNorm8, Submeshes = [[0, 1, 2]] }.Add(b);
        // Kept in the bundle's resource file.
        long streamed = new TestMesh { Name = "streamed", Positions = pos, Submeshes = [[2, 1, 0]], Streamed = true }.Add(b);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var m = bundles.LoadMesh(new("meshes", plain))!;
        Assert.Equal("plain", m.Name);
        Assert.Equal(4, m.VertexCount);
        Assert.Equal(pos, m.Positions);
        Assert.Equal(normals, m.Normals);
        Assert.Equal(uv, m.Uv);
        Assert.Equal([0, 1, 2, 0, 2, 3], m.Submeshes[0]);

        var p = bundles.LoadMesh(new("meshes", packed))!;
        Assert.Equal(pos, p.Positions);
        Assert.Equal(normals, p.Normals);
        for (int i = 0; i < uv.Length; i++)
        {
            Assert.Equal(uv[i], p.Uv![i], 4);
        }
        Assert.Equal([1, 2, 3], p.Submeshes[0]);
        Assert.Empty(p.Submeshes[1]);
        Assert.Equal([0, 1, 2], p.Submeshes[2]);

        var s = bundles.LoadMesh(new("meshes", bytes))!;
        for (int i = 0; i < normals.Length; i++)
        {
            Assert.Equal(normals[i], s.Normals![i], 4);
        }
        Assert.Equal(1f, s.Uv![2], 2);

        var st = bundles.LoadMesh(new("meshes", streamed))!;
        Assert.Equal(pos, st.Positions);
        Assert.Null(st.Normals);
        Assert.Null(st.Uv);
        Assert.Equal([2, 1, 0], st.Submeshes[0]);

        Assert.Null(bundles.LoadMesh(new("meshes", 12345)));
    }

    [Fact]
    public void CompressedMeshesAreUnpacked()
    {
        using var game = new TestGame();
        var b = game.Bundle("meshes");
        float[] pos = [-1, 0, 2, 3, 0.3f, 2, 3, 4, -2, -1, 4, 0.7f];
        float[] normals = [0, 0, -1, 0.6f, 0, 0.8f, 0, 0.6f, -0.8f, 0, 1, 0];
        float[] uv = [0, 0, 1, 0, 1, 1, 0.3f, 0.6f];
        // A count not a multiple of 3 (cut), from a base vertex; 32-bit indices count four bytes each.
        long narrow = new TestMesh { Name = "narrow", Positions = pos, Normals = normals, Uv = uv, Submeshes = [[0, 1, 2, 0, 2, 3], [0, 1, 2, 0]], BaseVertex = [0, 1], Compressed = true }.Add(b);
        long wide = new TestMesh { Name = "wide", Positions = pos, Submeshes = [[3, 2, 1], [0, 1, 2]], Wide = true, Compressed = true }.Add(b);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var m = bundles.LoadMesh(new("meshes", narrow))!;
        Assert.Equal(("narrow", 4), (m.Name, m.VertexCount));
        // Each value a step of its range (positions: -2 to 4, in 10 bits): at most half a step off.
        float step = 6f / 1023;
        for (int i = 0; i < pos.Length; i++)
        {
            Assert.Equal(pos[i], m.Positions[i], step / 2 + 1e-5f);
        }
        // Normals: x and y kept, z worked out with its sign.
        for (int i = 0; i < normals.Length; i++)
        {
            Assert.Equal(normals[i], m.Normals![i], 0.01f);
        }
        Assert.True(m.Normals![2] < 0);
        Assert.True(m.Normals[8] < 0);
        Assert.True(m.Normals[5] > 0);
        for (int i = 0; i < uv.Length; i++)
        {
            Assert.Equal(uv[i], m.Uv![i], 1f / 1023);
        }
        Assert.Equal([0, 1, 2, 0, 2, 3], m.Submeshes[0]);
        Assert.Equal([1, 2, 3], m.Submeshes[1]);

        var w = bundles.LoadMesh(new("meshes", wide))!;
        Assert.Null(w.Normals);
        Assert.Null(w.Uv);
        Assert.Equal([3, 2, 1], w.Submeshes[0]);
        Assert.Equal([0, 1, 2], w.Submeshes[1]);
    }

    [Fact]
    public void MaterialsGiveTheirShaderAndProperties()
    {
        using var game = new TestGame();
        var shaders = game.Bundle("a_shaders");
        long sh = TestAssets.Shader(shaders, "Custom/Piece", new Dictionary<int, byte[]>());
        var b = game.Bundle("b_mats");
        long tex = TestAssets.Texture(b, "wood", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        long mat = TestAssets.Material(b, "wood_mat", b.RefTo(shaders, sh),
            textures: [("_MainTex", Fields.Ref(tex), (2, 3), (0.5, 0.25)), ("_BumpMap", Fields.Ref(0), (1, 1), (0, 0))],
            floats: [("_Mode", 1), ("_Cutoff", 0.4)],
            colors: [("_Color", 1, 0.5, 0.25, 1)]);
        // A shader that is not one (no parsed form here: a texture): no name.
        long odd = TestAssets.Material(b, "odd", Fields.Ref(tex));
        long none = TestAssets.Material(b, "none");
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var m = bundles.LoadMaterial(new("b_mats", mat))!;
        Assert.Equal("wood_mat", m.Name);
        Assert.Equal("Custom/Piece", m.Shader);
        Assert.Equal(new GameBundles.Where("a_shaders", sh), m.ShaderAt);
        var main = m.Textures["_MainTex"];
        Assert.Equal(new GameBundles.Where("b_mats", tex), main.Texture);
        Assert.Equal((2f, 3f, 0.5f, 0.25f), (main.ScaleX, main.ScaleY, main.OffsetX, main.OffsetY));
        Assert.Null(m.Textures["_BumpMap"].Texture);
        Assert.Equal(0.4f, m.Floats["_Cutoff"]);
        Assert.Equal(new[] { 1f, 0.5f, 0.25f, 1f }, m.Colors["_Color"]);
        Assert.Equal("", bundles.LoadMaterial(new("b_mats", odd))!.Shader);
        var n = bundles.LoadMaterial(new("b_mats", none))!;
        Assert.Null(n.ShaderAt);
        Assert.Equal("", n.Shader);
        Assert.Null(bundles.LoadMaterial(new("b_mats", 777)));
    }

    [Fact]
    public void TexturesGiveTheLargestLevelThatFits()
    {
        using var game = new TestGame();
        var b = game.Bundle("textures");
        // 4 x 2 then 2 x 1 then 1 x 1, each level's pixels one byte: its level number.
        byte[] levels = [.. Enumerable.Repeat((byte)0, 8), .. Enumerable.Repeat((byte)1, 2), 2];
        long kept = TestAssets.Texture(b, "levels", 4, 2, Alpha8, 3, levels);
        long streamed = TestAssets.Texture(b, "streamed", 4, 2, Alpha8, 3, levels, streamed: true);
        long unknown = TestAssets.Texture(b, "unknown", 4, 4, 99, 1, new byte[16]);
        long empty = TestAssets.Texture(b, "empty", 0, 4, Alpha8, 1, []);
        long tooShort = TestAssets.Texture(b, "short", 4, 4, Rgba32, 1, new byte[10]);
        long mat = TestAssets.Material(b, "not a texture");
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var top = bundles.LoadTexture(new("textures", kept), 1024)!;
        Assert.Equal(("levels", Alpha8, 4, 2), (top.Name, top.Format, top.Width, top.Height));
        Assert.All(top.Data, x => Assert.Equal(0, x));
        var mid = bundles.LoadTexture(new("textures", kept), 2)!;
        Assert.Equal((2, 1), (mid.Width, mid.Height));
        Assert.All(mid.Data, x => Assert.Equal(1, x));
        // None small enough: the smallest there is.
        var least = bundles.LoadTexture(new("textures", streamed), 0)!;
        Assert.Equal((1, 1), (least.Width, least.Height));
        Assert.Equal([2], least.Data);
        Assert.Equal(1, bundles.LoadTexture(new("textures", streamed), 2)!.Data[0]);
        Assert.Null(bundles.LoadTexture(new("textures", unknown), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", empty), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", tooShort), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", mat), 1024));
        Assert.Null(bundles.LoadTexture(new("textures", 4242), 1024));
    }

    [Fact]
    public void CrunchedTexturesGiveTheirLevelAsDxtBlocks()
    {
        using var game = new TestGame();
        var b = game.Bundle("crunched");
        // The crn files of CrunchTests: DXT1 64 x 64 in 7 levels, DXT5 48 x 20 in 6; the Texture2D's own size and mip
        // count are not used (the crn file has its own).
        byte[] dxt1 = File.ReadAllBytes(Path.Combine(Fixtures.Root, "crunch-dxt1.crn"));
        byte[] dxt5 = File.ReadAllBytes(Path.Combine(Fixtures.Root, "crunch-dxt5.crn"));
        long kept = TestAssets.Texture(b, "kept", 48, 20, 29, 1, dxt5);
        long streamed = TestAssets.Texture(b, "streamed", 64, 64, 28, 1, dxt1, streamed: true);
        long broken = TestAssets.Texture(b, "broken", 64, 64, 28, 7, dxt1[..500]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var top = bundles.LoadTexture(new("crunched", kept), 1024)!;
        Assert.Equal(("kept", 12, 48, 20), (top.Name, top.Format, top.Width, top.Height));
        Assert.Equal(Crunch.Unpack(dxt5, 0), top.Data);
        var mid = bundles.LoadTexture(new("crunched", kept), 24)!;
        Assert.Equal((24, 10), (mid.Width, mid.Height));
        Assert.Equal(Crunch.Unpack(dxt5, 1), mid.Data);
        var least = bundles.LoadTexture(new("crunched", streamed), 0)!;
        Assert.Equal((10, 1, 1), (least.Format, least.Width, least.Height));
        Assert.Equal(Crunch.Unpack(dxt1, 6), least.Data);
        Assert.Equal(GameBundles.LevelSize(10, 32, 32), bundles.LoadTexture(new("crunched", streamed), 32)!.Data.Length);
        Assert.Null(bundles.LoadTexture(new("crunched", broken), 1024));
    }

    [Fact]
    public void CrunchedTexturesAreReadFromUnity2017Point3On()
    {
        Assert.False(GameBundles.UnityCrunchFormat("5.6.7f1"));
        Assert.False(GameBundles.UnityCrunchFormat("2017.2.5f1"));
        Assert.True(GameBundles.UnityCrunchFormat("2017.3.0f3"));
        Assert.True(GameBundles.UnityCrunchFormat("2022.3.62f1"));
        Assert.True(GameBundles.UnityCrunchFormat("6000.0.74f1"));
        // Not given (stripped bundles): taken as new.
        Assert.True(GameBundles.UnityCrunchFormat("0.0.0"));
        Assert.True(GameBundles.UnityCrunchFormat(""));
    }

    [Fact]
    public void TextureArraysGiveEachSlice()
    {
        using var game = new TestGame();
        var b = game.Bundle("arrays");
        // Two 1 x 1 RGBA slices (graphics format 8: RGBA8 sRGB, read as RGBA32).
        long kept = TestAssets.TextureArray(b, "pair", 1, 1, 8, 2, [1, 2, 3, 4, 5, 6, 7, 8]);
        long streamed = TestAssets.TextureArray(b, "pair_streamed", 1, 1, 4, 2, [1, 2, 3, 4, 5, 6, 7, 8], streamed: true);
        // DXT1 (graphics format 96): one 4 x 4 block of 8 bytes per slice.
        long dxt = TestAssets.TextureArray(b, "dxt", 4, 4, 96, 1, new byte[8]);
        long unknown = TestAssets.TextureArray(b, "unknown", 1, 1, 200, 1, [1, 2, 3, 4]);
        long missing = TestAssets.TextureArray(b, "missing", 2, 2, 8, 2, [1, 2, 3, 4]);
        long tex = TestAssets.Texture(b, "plain", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var slices = bundles.LoadTextureArray(new("arrays", kept))!;
        Assert.Equal(["pair[0]", "pair[1]"], slices.Select(s => s.Name));
        Assert.Equal([5, 6, 7, 8], slices[1].Data);
        Assert.Equal(Rgba32, slices[0].Format);
        Assert.Equal([1, 2, 3, 4], bundles.LoadTextureArray(new("arrays", streamed))![0].Data);
        Assert.Equal(10, bundles.LoadTextureArray(new("arrays", dxt))![0].Format);
        Assert.Null(bundles.LoadTextureArray(new("arrays", unknown)));
        Assert.Null(bundles.LoadTextureArray(new("arrays", missing)));
        Assert.Null(bundles.LoadTextureArray(new("arrays", tex)));
    }

    [Fact]
    public void ShadersGiveEachPlatformsProgramAndTheDeferredPass()
    {
        using var game = new TestGame();
        var b = game.Bundle("shaders");
        byte[] gl = [.. Enumerable.Range(0, 300).Select(i => (byte)i)];
        byte[] vk = "vulkan program"u8.ToArray();
        long sh = TestAssets.Shader(b, "Custom/Heightmap", new Dictionary<int, byte[]> { [15] = gl, [18] = vk },
            [new(0, 0, 25, 1), new(2, 1, 25, 3)], part: 128);
        long other = TestAssets.Shader(b, "Custom/Other", new Dictionary<int, byte[]> { [15] = gl }, [new(0, 0, 25, 1)], pass: "FORWARD_DELTA");
        long tex = TestAssets.Texture(b, "plain", 1, 1, Rgba32, 1, [1, 2, 3, 4]);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        var p = bundles.LoadShader(new("shaders", sh))!;
        Assert.Equal([15, 18], p.Platforms);
        Assert.Equal(gl, p.Blobs[15]);
        Assert.Equal(vk, p.Blobs[18]);
        Assert.Equal([new GameShader.SubProgram(25, 0, 0, 1), new GameShader.SubProgram(25, 1, 2, 3)], p.Deferred);
        Assert.Empty(bundles.LoadShader(new("shaders", other))!.Deferred);
        Assert.Null(bundles.LoadShader(new("shaders", tex)));
    }

    [Fact]
    public void AShaderBlobThatDoesNotUnpackIsSaid()
    {
        using var game = new TestGame();
        var b = game.Bundle("shaders");
        byte[] packed = UnityFsWriter.Literals(new byte[64]);
        // A part said to unpack to more bytes than it does, and one that is not LZ4 at all.
        long shorter = BrokenShader(b, packed, 100);
        long garbage = BrokenShader(b, [0xFF, 0xFF, 1, 2], 100);
        long whole = BrokenShader(b, packed, 64);
        game.Save();
        var bundles = new GameBundles(game.BundlesDir, null);

        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", shorter)));
        Assert.Throws<InvalidDataException>(() => bundles.LoadShader(new("shaders", garbage)));
        Assert.Equal(new byte[64], bundles.LoadShader(new("shaders", whole))!.Blobs[15]);
    }

    /// <summary>
    /// A shader with one OpenGL program of one part: these packed bytes, said to unpack to <paramref name="size"/>.
    /// </summary>
    private static long BrokenShader(TestBundle b, byte[] packed, uint size) =>
        b.Assets.Add(UnityClass.Shader, UnityTypes.Shader, new Fields
        {
            ["platforms"] = new List<object> { 15u },
            ["offsets"] = new List<object> { new List<object> { 0u } },
            ["compressedLengths"] = new List<object> { new List<object> { (uint)packed.Length } },
            ["decompressedLengths"] = new List<object> { new List<object> { size } },
            ["compressedBlob"] = packed,
        });
}
