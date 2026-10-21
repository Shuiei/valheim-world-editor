using System.Numerics;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What running extensions draw of an area as a whole (ModExtensions.ScenesOf): each provider's layer, with the meshes
/// that cannot be drawn and the empty holes left out; a provider that throws turns its extension off and the others go
/// on. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsScenesTests : IDisposable
{
    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An extension registering one scene provider.</summary>
    private sealed class Scenes(Func<IAreaView, SceneLayer?> build) : IWorldEditorExtension, ISceneProvider
    {
        public void Register(IExtensionRegistry registry) => registry.AddScene(this);

        public SceneLayer? Build(IAreaView area) => build(area);
    }

    /// <summary>An area of flat ground at 10 m, holding nothing.</summary>
    private static readonly PlainArea FlatArea = new(0, 0, 64, 64, 10);

    /// <summary>A mesh of one triangle.</summary>
    private static SceneMesh Triangle() => new([Vector3.Zero, Vector3.UnitZ, Vector3.UnitX], [Vector3.UnitY, Vector3.UnitY, Vector3.UnitY], [0, 1, 2]);

    [Fact]
    public void OnlyWhatCanBeDrawnIsKept()
    {
        var good = Triangle();
        var layer = new SceneLayer();
        layer.Meshes.Add(good);
        layer.Meshes.Add(new SceneMesh([Vector3.Zero], [Vector3.UnitY], [0, 0, 1]));
        layer.Meshes.Add(new SceneMesh([Vector3.Zero, Vector3.One, Vector3.UnitX], [Vector3.UnitY], [0, 1, 2]));
        layer.Meshes.Add(new SceneMesh([new(float.NaN, 0, 0), Vector3.One, Vector3.UnitX], [Vector3.UnitY, Vector3.UnitY, Vector3.UnitY], [0, 1, 2]));
        layer.Meshes.Add(new SceneMesh(good.Positions, good.Normals, good.Triangles) { BiomeColors = [1] });
        layer.Meshes.Add(new SceneMesh(good.Positions, good.Normals, good.Triangles) { Mask = new MaskTexture(2, 1, [1]), MaskUvs = [Vector2.Zero, Vector2.Zero, Vector2.Zero] });
        layer.Meshes.Add(new SceneMesh(good.Positions, good.Normals, good.Triangles) { Mask = new MaskTexture(300, 1, new uint[300]) });
        layer.Meshes.Add(new SceneMesh(good.Positions, good.Normals, good.Triangles) { MaskUvs = [Vector2.Zero] });
        layer.Holes.Add(new GroundHole(0, 0, 2, 3));
        layer.Holes.Add(new GroundHole(4, 4, 4, 8));
        ModExtensions.UseForTests(("Mod", new Scenes(_ => layer)));

        Assert.True(ModExtensions.AnyScenes);
        var kept = Assert.Single(ModExtensions.ScenesOf(FlatArea));
        Assert.Same(good, Assert.Single(kept.Meshes));
        Assert.Equal((0, 0, 2, 3), Assert.Single(kept.Holes) is var h ? (h.X0, h.Z0, h.X1, h.Z1) : default);
    }

    [Fact]
    public void AProviderThatThrowsTurnsItsExtensionOff()
    {
        var layer = new SceneLayer();
        layer.Meshes.Add(Triangle());
        ModExtensions.UseForTests(("Broken", new Scenes(_ => throw new InvalidOperationException("no"))), ("Fine", new Scenes(_ => layer)), ("None", new Scenes(_ => null)));

        Assert.Single(ModExtensions.ScenesOf(FlatArea));
        Assert.Equal(ModExtensions.Status.Failed, ModExtensions.Offered.Single(e => e.Mod == "Broken").State);
        // Turned off: not asked again.
        Assert.Single(ModExtensions.ScenesOf(FlatArea));
    }

    [Fact]
    public void WithoutProvidersThereIsNothing()
    {
        Assert.False(ModExtensions.AnyScenes);
        Assert.Empty(ModExtensions.ScenesOf(FlatArea));
    }
}
