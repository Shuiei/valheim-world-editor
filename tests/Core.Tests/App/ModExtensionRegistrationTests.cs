using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What an extension registers with the editor's registry: shape providers and ground cutters by prefab, and copy
/// rewriters by key, each showing up in use for the prefabs or keys it named and for no others. Runs alone (the
/// extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionRegistrationTests : IDisposable
{
    /// <summary>Forgets the extensions in use, so the next test starts without any.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An extension registering one shape provider for two prefabs, one cutter for a third and a rewriter for the first.</summary>
    private sealed class Registering : IWorldEditorExtension
    {
        /// <summary>Registers the shapes, cutters and rewriter.</summary>
        public void Register(IExtensionRegistry registry)
        {
            registry.AddShapes(new NoShapes(), "Chest", "Table");
            registry.AddGroundCuts(new NoCuts(), "Pit");
            registry.AddCopyRewriter(new NoRewrite(), "Chest");
        }
    }

    /// <summary>A shape provider that gives no shapes.</summary>
    private sealed class NoShapes : IShapeProvider
    {
        /// <summary>Gives no shapes.</summary>
        public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world) => [];
    }

    /// <summary>A ground cutter that cuts nothing.</summary>
    private sealed class NoCuts : IGroundCutter
    {
        /// <summary>Gives no cuts.</summary>
        public IEnumerable<GroundCut> CutsOf(IObjectView obj, IWorldView world) => [];
    }

    /// <summary>A copy rewriter that changes nothing.</summary>
    private sealed class NoRewrite : ICopyRewriter
    {
        /// <summary>Changes nothing.</summary>
        public void Rewrite(ICopy copy, IIdMap ids)
        {
        }
    }

    /// <summary>A shape provider registered for a prefab makes that prefab have shapes.</summary>
    [Fact]
    public void AShapeProviderMakesItsPrefabsHaveShapes()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.True(ModExtensions.HasShapes(StableHash.Of("Chest")));
        Assert.True(ModExtensions.HasShapes(StableHash.Of("Table")));
        Assert.True(ModExtensions.AnyShapes);
    }

    /// <summary>A prefab no provider was registered for has no shapes from it.</summary>
    [Fact]
    public void APrefabNotRegisteredForShapesHasNone()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.False(ModExtensions.HasShapes(StableHash.Of("Pit")));
    }

    /// <summary>A ground cutter registered for a prefab makes that prefab cut the ground.</summary>
    [Fact]
    public void AGroundCutterMakesItsPrefabCutTheGround()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.True(ModExtensions.HasCuts(StableHash.Of("Pit")));
        Assert.False(ModExtensions.HasCuts(StableHash.Of("Chest")));
        Assert.True(ModExtensions.AnyCuts);
    }

    /// <summary>A copy rewriter registered with keys has those keys in use, and no others.</summary>
    [Fact]
    public void ACopyRewriterKeysItsKeysAsRewritten()
    {
        ModExtensions.UseForTests(("Registering", new Registering()));

        Assert.Contains(StableHash.Of("Chest"), ModExtensions.RewrittenKeys);
        Assert.DoesNotContain(StableHash.Of("Table"), ModExtensions.RewrittenKeys);
    }
}
