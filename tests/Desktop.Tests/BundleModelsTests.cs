using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's models made ready for the view (BundleModels): Unity's left-handed matrices and meshes mirrored into the
/// view's space.
/// </summary>
public class BundleModelsTests
{
    [Fact]
    public void UnitysSpaceIsMirroredIntoTheViews()
    {
        // A move of (1, 2, 3) in Unity is (1, 2, -3) in the view.
        var m = BundleModels.ViewMatrix(GameBundles.Matrix.Trs(1, 2, 3, 0, 0, 0, 1, 1, 1, 1));
        Assert.Equal(new Vector3(1, 2, -3), Vector3.Transform(Vector3.Zero, m));
        // Turned 90° about y (Unity: x goes to -z), then mirrored: the view's x goes to its +z.
        float s = MathF.Sqrt(0.5f);
        var turn = BundleModels.ViewMatrix(GameBundles.Matrix.Trs(0, 0, 0, 0, s, 0, s, 1, 1, 1));
        var x = Vector3.Transform(Vector3.UnitX, turn);
        Assert.Equal(0, x.X, 4);
        Assert.Equal(1, x.Z, 4);
        // Meshes: z mirrored, an up normal when there is none, uv as they are.
        var mesh = BundleModels.ViewMesh(new GameBundles.Mesh("m", 1, [1f, 2f, 3f], null, [0.25f, 0.75f], [[0, 0, 0]]));
        Assert.Equal(new[] { 1f, 2f, -3f, 0f, 1f, 0f, 0.25f, 0.75f }, mesh.Vertices);
        Assert.Equal(new uint[] { 0, 0, 0 }, mesh.Submeshes[0]);
    }
}
