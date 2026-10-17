using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Picking's axis choice: an axis index picks the vector's X, Y or Z, which the box test uses one axis at a time.
/// </summary>
public class PickingComponentTests
{
    [Fact]
    public void AxisZeroIsX()
    {
        Assert.Equal(1f, Picking.Component(new Vector3(1, 2, 3), 0));
    }

    [Fact]
    public void AxisOneIsY()
    {
        Assert.Equal(2f, Picking.Component(new Vector3(1, 2, 3), 1));
    }

    [Fact]
    public void AnyOtherAxisIsZ()
    {
        Assert.Equal(3f, Picking.Component(new Vector3(1, 2, 3), 2));
    }
}
