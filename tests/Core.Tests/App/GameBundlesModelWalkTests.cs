using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// The walk over a prefab's objects that gives its model (GameBundles.ModelWalk), run step by step: the parts left out when
/// it scans and when it is a room, the children placed by their transforms, the inactive ones left out, and each renderer's
/// submeshes placed one part per material.
/// </summary>
public class GameBundlesModelWalkTests
{
    /// <summary>A walk over the prefab's bundle, which holds it.</summary>
    private static GameBundles.ModelWalk Walk(TestGame game) => new(new GameBundles(game.BundlesDir, null), "b_house");

    [Fact]
    public void ScanLeavesOutTheWornAndBrokenParts()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long single = Quad("single").Add(b);
        var ownRef = Fields.Ref(TestAssets.Material(b, "own"));
        var root = b.Prefab("House", r =>
        {
            var fresh = r.Child("new", c => c.Renderer(Fields.Ref(single), [ownRef]));
            var worn = r.Child("worn", c => c.Renderer(Fields.Ref(single), [ownRef]));
            var broken = r.Child("broken", c => c.Renderer(Fields.Ref(single), [ownRef]));
            r.Mono("WearNTear", new Fields { ["m_new"] = Fields.Ref(fresh.Go), ["m_worn"] = Fields.Ref(worn.Go), ["m_broken"] = Fields.Ref(broken.Go) });
        });
        game.Save();

        var walk = Walk(game);
        walk.Scan(root.Go);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void ScanLeavesOutTheRenderersOfLowerDetailLevels()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long single = Quad("single").Add(b);
        var ownRef = Fields.Ref(TestAssets.Material(b, "own"));
        var root = b.Prefab("House", r =>
        {
            long near = 0, far = 0;
            r.Child("near", c => near = c.Renderer(Fields.Ref(single), [ownRef]));
            r.Child("far", c => far = c.Renderer(Fields.Ref(single), [ownRef]));
            r.Component(UnityClass.LodGroup, UnityTypes.LodGroup, new Fields
            {
                ["m_LODs"] = new List<Fields>
                {
                    new() { ["renderers"] = new List<Fields> { new() { ["renderer"] = Fields.Ref(near) } } },
                    new() { ["renderers"] = new List<Fields> { new() { ["renderer"] = Fields.Ref(far) } } },
                },
            });
        });
        game.Save();

        var walk = Walk(game);
        walk.Scan(root.Go);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void ARoomLeavesOutARandomChildThatRarelySpawns()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long single = Quad("single").Add(b);
        var ownRef = Fields.Ref(TestAssets.Material(b, "own"));
        var root = b.Prefab("Room", r =>
        {
            r.Child("rare", c =>
            {
                c.Mono("RandomSpawn", new Fields { ["m_chanceToSpawn"] = 30.0 });
                c.Renderer(Fields.Ref(single), [ownRef]);
            });
            r.Child("common", c =>
            {
                c.Mono("RandomSpawn", new Fields { ["m_chanceToSpawn"] = 80.0 });
                c.Renderer(Fields.Ref(single), [ownRef]);
            });
        });
        game.Save();

        var walk = Walk(game);
        walk.RoomSkips(root.Go, true);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void AChildIsPlacedByItsTransform()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long single = Quad("single").Add(b);
        var ownRef = Fields.Ref(TestAssets.Material(b, "own"));
        var root = b.Prefab("House", r => r.Child("right", c =>
        {
            c.Position = (1, 0, 0);
            c.Renderer(Fields.Ref(single), [ownRef]);
        }));
        game.Save();

        var walk = Walk(game);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        // The translation's x is the fourth number of the row-major matrix.
        Assert.Equal(1f, Assert.Single(walk.Parts).Matrix[3]);
    }

    [Fact]
    public void AnInactiveChildIsLeftOut()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long single = Quad("single").Add(b);
        var ownRef = Fields.Ref(TestAssets.Material(b, "own"));
        var root = b.Prefab("House", r =>
        {
            r.Child("off", c =>
            {
                c.Active = false;
                c.Renderer(Fields.Ref(single), [ownRef]);
            });
            r.Child("on", c => c.Renderer(Fields.Ref(single), [ownRef]));
        });
        game.Save();

        var walk = Walk(game);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void ARendererWithOneMaterialIsOnePartForItsSubmeshes()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long split = Quad("split", split: true).Add(b);
        var ownRef = Fields.Ref(TestAssets.Material(b, "own"));
        var root = b.Prefab("House", r => r.Child("wall", c => c.Renderer(Fields.Ref(split), [ownRef])));
        game.Save();

        var walk = Walk(game);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        Assert.Equal(0, Assert.Single(walk.Parts).Sub);
    }

    [Fact]
    public void ARendererWithTwoMaterialsIsOnePartPerSubmesh()
    {
        using var game = new TestGame();
        var b = game.Bundle("b_house");
        long split = Quad("split", split: true).Add(b);
        var first = Fields.Ref(TestAssets.Material(b, "first"));
        var second = Fields.Ref(TestAssets.Material(b, "second"));
        var root = b.Prefab("House", r => r.Child("wall", c => c.Renderer(Fields.Ref(split), [first, second])));
        game.Save();

        var walk = Walk(game);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        Assert.Equal(new[] { 0, 1 }, walk.Parts.Select(p => p.Sub).ToArray());
    }
}
