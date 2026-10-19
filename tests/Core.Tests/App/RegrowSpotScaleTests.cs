using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>A spot's scale on each axis: the axes' when its rule scales by axis, else the uniform scale on all three.</summary>
public class RegrowSpotScaleTests
{
    [Fact]
    public void ASpotScaledByAxisKeepsEachAxis()
    {
        var s = new Regrow.Spot("Pine", 0, 0, 0, 0, 0, 0, 1.5f, [1.5f, 2f, 0.75f]);
        Assert.Equal(new Vector3(1.5f, 2f, 0.75f), s.Scale3);
    }

    [Fact]
    public void AUniformSpotHasItsScaleOnAllThreeAxes()
    {
        var s = new Regrow.Spot("Pine", 0, 0, 0, 0, 0, 0, 1.25f);
        Assert.Equal(new Vector3(1.25f), s.Scale3);
    }
}
