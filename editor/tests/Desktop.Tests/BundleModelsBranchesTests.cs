using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's models made ready for the view, remaining branches: a mesh with its own normals, and a cut-out
/// material's cutoff given or left at zero.
/// </summary>
public class BundleModelsBranchesTests
{
    [Fact]
    public void AMeshsOwnNormalsAreMirroredToo()
    {
        var mesh = BundleModels.ViewMesh(new GameBundles.Mesh("m", 1, [1f, 2f, 3f], [0f, 0.6f, 0.8f], null, [[0, 0, 0]]));
        Assert.Equal(new[] { 1f, 2f, -3f, 0f, 0.6f, -0.8f, 0f, 0f }, mesh.Vertices);
    }

    [Theory]
    [InlineData(0.3f, 0.3f)]
    [InlineData(0f, 0.5f)]
    public void ACutOutMaterialKeepsItsCutoffUnlessZero(float given, float want)
    {
        var floats = new Dictionary<string, float> { ["_Mode"] = 1, ["_Cutoff"] = given };
        var m = new GameBundles.Material("m", "Standard", null, [], floats, []);
        Assert.Equal(want, BundleModels.ViewMaterial(m).Cutoff, 4);
    }

    [Theory]
    [InlineData("Standard", 1f, false, true, false, false)]
    [InlineData("Custom/Vegetation", 0f, true, true, false, false)]
    [InlineData("Standard", 2f, false, false, true, false)]
    [InlineData("Custom/Thatch", 2f, false, false, false, true)]
    [InlineData("Legacy/Glass", 0f, false, false, true, false)]
    [InlineData("Custom/Opaque", 0f, false, false, false, false)]
    public void AMaterialsModeIsCutOutSeeThroughOrHalfCutByItsShaderAndMode(string shader, float mode, bool vegetation, bool cut, bool seeThrough, bool halfCut)
    {
        Assert.Equal((cut, seeThrough, halfCut), BundleModels.Modes(shader, mode, vegetation));
    }

    [Fact]
    public void ACutOutMaterialsCutoffIsItsThresholdRoundedToThousandths()
    {
        var m = new GameBundles.Material("m", "Standard", null, [], new Dictionary<string, float> { ["_Cutoff"] = 0.12345f }, []);
        Assert.Equal(0.123f, BundleModels.Cutoff(m, true, false), 4);
    }

    [Fact]
    public void AHalfCutMaterialsCutoffIsHalfWhateverItsThreshold()
    {
        var m = new GameBundles.Material("m", "Custom/Thatch", null, [], new Dictionary<string, float> { ["_Cutoff"] = 0.2f }, []);
        Assert.Equal(0.5f, BundleModels.Cutoff(m, false, true));
    }

    [Fact]
    public void AnOpaqueMaterialHasNoCutoff()
    {
        var m = new GameBundles.Material("m", "Custom/Opaque", null, [], [], []);
        Assert.Equal(0f, BundleModels.Cutoff(m, false, false));
    }

    [Fact]
    public void AnOpaqueMaterialKeepsItsAlpha()
    {
        Assert.Equal(0.1f, BundleModels.Alpha(0.1f, "Standard", false));
    }

    [Fact]
    public void ASeeThroughMaterialIsAtLeastLeastAlpha()
    {
        Assert.Equal(BundleModels.LeastAlpha, BundleModels.Alpha(0.1f, "Legacy", true));
    }

    [Fact]
    public void ASeeThroughMaterialKeepsAnAlphaAboveLeastAlpha()
    {
        Assert.Equal(0.8f, BundleModels.Alpha(0.8f, "Legacy", true));
    }

    [Fact]
    public void ARefractingGlassIsLeastAlphaWhateverItsAlpha()
    {
        Assert.Equal(BundleModels.LeastAlpha, BundleModels.Alpha(0.9f, "Glass/Distort", true));
    }

    [Fact]
    public void ACutOutMainTextureIsItsIdWithCutAndItsUvTransform()
    {
        var where = new GameBundles.Where("b", 5);
        var textures = new Dictionary<string, GameBundles.TexEnv> { ["_MainTex"] = new(where, 2f, 3f, 0.5f, 0.25f) };
        var m = new GameBundles.Material("m", "Standard", null, textures, [], []);
        var (map, uv) = BundleModels.MainTexture(m, true);
        Assert.Equal(where.Key + "|cut", map);
        Assert.Equal((2f, 3f, 0.5f, 0.25f), (uv.X, uv.Y, uv.Z, uv.W));
    }

    [Fact]
    public void AMaterialWithoutATextureHasNoMainTextureAndAnIdentityTransform()
    {
        var m = new GameBundles.Material("m", "Standard", null, [], [], []);
        var (map, uv) = BundleModels.MainTexture(m, false);
        Assert.Null(map);
        Assert.Equal((1f, 1f, 0f, 0f), (uv.X, uv.Y, uv.Z, uv.W));
    }
}
