using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>A blank object's scale: kept on each axis when it is not 1, left out when it is.</summary>
public class ZdoBuilderBlankTests
{
    [Fact]
    public void AScaleByAxisIsStoredOnEachAxis()
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(77, 0x0100, Vector3.Zero, Vector3.Zero, new Vector3(1.5f, 2f, 0.75f)));
        Assert.Equal([(ZdoData.ScaleKey, new Vector3(1.5f, 2f, 0.75f))], z.Vec3List);
    }

    [Fact]
    public void AUniformScaleIsStoredOnAllThreeAxes()
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(77, 0x0100, Vector3.Zero, Vector3.Zero, 1.25f));
        Assert.Equal([(ZdoData.ScaleKey, new Vector3(1.25f))], z.Vec3List);
    }

    [Theory]
    [InlineData(1f, 1f, 1f)]
    [InlineData(0f, 2f, 2f)]
    public void AScaleOfOneOrNoneIsLeftOut(float x, float y, float z)
    {
        var data = ZdoData.Parse(ZdoBuilder.Blank(77, 0x0100, Vector3.Zero, Vector3.Zero, new Vector3(x, y, z)));
        Assert.Empty(data.Vec3List);
    }
}
