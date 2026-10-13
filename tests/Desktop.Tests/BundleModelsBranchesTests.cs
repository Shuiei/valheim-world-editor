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
}
