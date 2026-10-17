using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// What a prefab's root tells the editor it is, and the models read from prefabs: what the game draws of a prefab, and what a
/// room draws of its contents.
/// </summary>
public class GameBundlesPrefabTests
{
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
            GameBundlesLodGroup.Add(r, [keptRenderer], [lodRenderer, 0]);
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
        var bundles = SavedGameBundles.Of(game);

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
        var bundles = SavedGameBundles.Of(game);

        // The root, "common" and "one": three parts; as a plain prefab all seven that draw.
        Assert.Equal(3, bundles.LoadModel("Room_hall", room: true)!.Parts.Count);
        Assert.Equal(7, bundles.LoadModel("Room_hall", room: false)!.Parts.Count);
    }
}
