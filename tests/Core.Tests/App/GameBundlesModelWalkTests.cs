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
    private static readonly int[] Expected1 = [0, 1];
    /// <summary>A walk over the prefab's bundle, which holds it.</summary>
    private static GameBundles.ModelWalk Walk(TestGame game) => new(new GameBundles(game.BundlesDir, null), "b_house");

    /// <summary>
    /// The house bundle of a game: one quad mesh, and one material of its own (as a reference a renderer can hold).
    /// </summary>
    private static (TestBundle B, long Single, Fields OwnRef) HouseBundle(TestGame game)
    {
        var b = game.Bundle("b_house");
        long single = Quad("single").Add(b);
        return (b, single, Fields.Ref(TestAssets.Material(b, "own")));
    }

    /// <summary>
    /// Saves the game, then walks the prefab's root at the identity matrix. <paramref name="prepare"/> runs first when given
    /// (a scan, or a room's random skips).
    /// </summary>
    private static GameBundles.ModelWalk WalkRoot(TestGame game, PrefabNode root, Action<GameBundles.ModelWalk>? prepare = null)
    {
        game.Save();
        var walk = Walk(game);
        prepare?.Invoke(walk);
        walk.Walk(root.Go, GameBundles.Matrix.Identity(), true);
        return walk;
    }

    [Fact]
    public void ScanLeavesOutTheWornAndBrokenParts()
    {
        using var game = new TestGame();
        var (b, single, ownRef) = HouseBundle(game);
        var root = b.Prefab("House", r =>
        {
            var fresh = r.Child("new", c => c.Renderer(Fields.Ref(single), [ownRef]));
            var worn = r.Child("worn", c => c.Renderer(Fields.Ref(single), [ownRef]));
            var broken = r.Child("broken", c => c.Renderer(Fields.Ref(single), [ownRef]));
            r.Mono("WearNTear", new Fields { ["m_new"] = Fields.Ref(fresh.Go), ["m_worn"] = Fields.Ref(worn.Go), ["m_broken"] = Fields.Ref(broken.Go) });
        });

        var walk = WalkRoot(game, root, w => w.Scan(root.Go));
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void ScanLeavesOutTheRenderersOfLowerDetailLevels()
    {
        using var game = new TestGame();
        var (b, single, ownRef) = HouseBundle(game);
        var root = b.Prefab("House", r =>
        {
            long near = 0, far = 0;
            r.Child("near", c => near = c.Renderer(Fields.Ref(single), [ownRef]));
            r.Child("far", c => far = c.Renderer(Fields.Ref(single), [ownRef]));
            GameBundlesLodGroup.Add(r, [near], [far]);
        });

        var walk = WalkRoot(game, root, w => w.Scan(root.Go));
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void ARoomLeavesOutARandomChildThatRarelySpawns()
    {
        using var game = new TestGame();
        var (b, single, ownRef) = HouseBundle(game);
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

        var walk = WalkRoot(game, root, w => w.RoomSkips(root.Go, true));
        Assert.Single(walk.Parts);
    }

    [Fact]
    public void AChildIsPlacedByItsTransform()
    {
        using var game = new TestGame();
        var (b, single, ownRef) = HouseBundle(game);
        var root = b.Prefab("House", r => r.Child("right", c =>
        {
            c.Position = (1, 0, 0);
            c.Renderer(Fields.Ref(single), [ownRef]);
        }));

        var walk = WalkRoot(game, root);
        // The translation's x is the fourth number of the row-major matrix.
        Assert.Equal(1f, Assert.Single(walk.Parts).Matrix[3]);
    }

    [Fact]
    public void AnInactiveChildIsLeftOut()
    {
        using var game = new TestGame();
        var (b, single, ownRef) = HouseBundle(game);
        var root = b.Prefab("House", r =>
        {
            r.Child("off", c =>
            {
                c.Active = false;
                c.Renderer(Fields.Ref(single), [ownRef]);
            });
            r.Child("on", c => c.Renderer(Fields.Ref(single), [ownRef]));
        });

        var walk = WalkRoot(game, root);
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

        var walk = WalkRoot(game, root);
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

        var walk = WalkRoot(game, root);
        Assert.Equal(Expected1, walk.Parts.Select(p => p.Sub).ToArray());
    }
}
