using System.Numerics;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.BundleMaterialFixture;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's materials made ready for the view (BundleModels): judged as the old exporter did (glass and other see-through
/// ones blended, vegetation and cut-out ones cut out), with the main texture found under the names shaders give it.
/// </summary>
public class BundleMaterialsTests
{
    [Fact]
    public void MaterialsAreJudgedAsTheExporterDid()
    {
        var plain = BundleModels.ViewMaterial(Mat("Standard", color: [0.5f, 1f, 0f, 0.8f]));
        Assert.Equal(0, plain.Cutoff);
        Assert.False(plain.DoubleSided);
        Assert.Equal("b:5", plain.Map);
        Assert.Equal(new Vector4(2, 3, 0.5f, 0.25f), plain.UvTransform);
        Assert.Equal(MathF.Pow(0.5f, 2.2f), plain.Color.X, 5);
        Assert.Equal(0.8f, plain.Color.W);
        // Vegetation: cut out at half, both sides, its texture bled ("|cut").
        var leaves = BundleModels.ViewMaterial(Mat("Custom/Vegetation"));
        Assert.Equal(0.5f, leaves.Cutoff);
        Assert.True(leaves.DoubleSided);
        Assert.Equal("b:5|cut", leaves.Map);
        Assert.False(plain.Transparent);
        // No culling: both sides.
        Assert.True(BundleModels.ViewMaterial(Mat("Standard", cull: 0)).DoubleSided);
        Assert.Null(BundleModels.ViewMaterial(Mat("Standard", texture: false)).Map);
        Assert.Equal(new GameBundles.Where("bundle:x", -12), GameBundles.Where.FromKey("bundle:x:-12"));
        Assert.Null(GameBundles.Where.FromKey("nothing"));
    }

    [Theory]
    [InlineData("Standard", 2f)]
    [InlineData("Standard", 3f)]
    [InlineData("Autodesk Interactive", 2f)]
    [InlineData("Legacy Shaders/Transparent/Diffuse", null)]
    [InlineData("FX/Glass/Stained BumpDistort", 2f)]
    [InlineData("Particles/Standard Unlit", null)]
    public void SeeThroughMaterialsAreBlendedNotCutOut(string shader, float? mode)
    {
        var glass = BundleModels.ViewMaterial(Mat(shader, mode));

        Assert.True(glass.Transparent);
        Assert.Equal(0, glass.Cutoff);
        // Its texture bled, for the edges of what it shows.
        Assert.Equal("b:5|cut", glass.Map);
    }

    [Theory]
    [InlineData("Custom/Vegetation", 3f, 0.5f)]
    [InlineData("Legacy Shaders/Transparent/Cutout/Bumped Diffuse", null, 0.5f)]
    [InlineData("Custom/Piece", 2f, 0.5f)]
    [InlineData("Custom/StaticRock", 3f, 0.5f)]
    [InlineData("Custom/Piece", null, 0f)]
    public void CutOutAndOpaqueMaterialsAreNotBlended(string shader, float? mode, float cutoff)
    {
        // Vegetation and cut-out shaders cut out whatever their mode; a mode left on a shader without modes (the game's
        // pieces and rocks keep one) cuts out at half, as before.
        var m = BundleModels.ViewMaterial(Mat(shader, mode));

        Assert.False(m.Transparent);
        Assert.Equal(cutoff, m.Cutoff);
    }

    [Theory]
    [InlineData("Standard", 3f, 0.02f, BundleModels.LeastAlpha)]
    [InlineData("Standard", 2f, 0.6f, 0.6f)]
    [InlineData("FX/Glass/Stained BumpDistort", 2f, 0.9f, BundleModels.LeastAlpha)]
    [InlineData("FX/Glass/Stained BumpDistort", 2f, 0f, BundleModels.LeastAlpha)]
    [InlineData("Custom/Glass", 0f, 0.6f, 0.6f)]
    [InlineData("Standard", 0f, 0.02f, 0.02f)]
    public void SeeThroughMaterialsAlphaIsKeptVisible(string shader, float mode, float given, float drawn)
    {
        // The game's crystal window has 0.38, some mods' glass 0 to 0.02: all but invisible blended, so at least a tint.
        // Refracting glass tints what is behind it; an opaque material's alpha is left alone.
        var m = BundleModels.ViewMaterial(Mat(shader, mode, color: [1f, 1f, 1f, given], texture: false));

        Assert.Equal(drawn, m.Color.W, 5);
        Assert.Null(m.Map);
    }

    [Theory]
    [InlineData("_MainTex")]
    [InlineData("_Albedo")]
    [InlineData("_BaseMap")]
    [InlineData("_BaseColorMap")]
    [InlineData("_MainTexture")]
    [InlineData("_Diffuse")]
    [InlineData("_BaseTexture")]
    public void TheMainTextureIsFoundUnderTheNamesShadersGiveIt(string slot)
    {
        var m = BundleModels.ViewMaterial(Mat("Custom/Thatch", slot: slot));

        Assert.Equal("b:5", m.Map);
        Assert.Equal(new Vector4(2, 3, 0.5f, 0.25f), m.UvTransform);
    }

    [Fact]
    public void TheFirstNameWithATextureIsTaken()
    {
        var two = Mat("Standard");
        two.Textures["_Albedo"] = new GameBundles.TexEnv(new GameBundles.Where("b", 7), 1, 1, 0, 0);
        two.Textures["_BumpMap"] = new GameBundles.TexEnv(new GameBundles.Where("b", 8), 1, 1, 0, 0);
        Assert.Equal("b:5", BundleModels.ViewMaterial(two).Map);
        // A slot named but empty: the next.
        var empty = Mat("Standard", slot: "_Albedo");
        empty.Textures["_MainTex"] = new GameBundles.TexEnv(null, 1, 1, 0, 0);
        Assert.Equal("b:5", BundleModels.ViewMaterial(empty).Map);
        Assert.Null(BundleModels.ViewMaterial(Mat("Standard", slot: "_BumpMap")).Map);
    }
}
