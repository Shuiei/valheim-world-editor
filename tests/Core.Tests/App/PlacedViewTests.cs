using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The placement every object view shares (PlacedView): the size is the one given, or the prefab's own (1) when none
/// is given (0 or less), and the turn is the Euler angles' quaternion.
/// </summary>
public class PlacedViewTests
{
    /// <summary>A placed view with the given size, for the shared placement.</summary>
    private sealed class Placed(float scale) : PlacedView("Cart", Vector3.Zero, Vector3.Zero, scale);

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-2f, 1f)]
    [InlineData(2.5f, 2.5f)]
    public void ScaleIsTheGivenSizeOrThePrefabsOwn(float given, float expected)
    {
        Assert.Equal(expected, new Placed(given).Scale);
    }

    [Fact]
    public void OrientationIsTheQuaternionOfTheTurn()
    {
        var view = new Placed(1);
        Assert.Equal(BlueprintFormats.FromEuler(Vector3.Zero), view.Orientation);
    }
}
