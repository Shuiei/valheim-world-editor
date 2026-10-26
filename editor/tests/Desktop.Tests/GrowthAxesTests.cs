using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>A regrown object scaled by axis (Expand World Data) carries the scale on each axis as its data.</summary>
public class GrowthAxesTests
{
    [Fact]
    public void ASpotScaledByAxisGetsItsScaleAsData()
    {
        var data = Growth.AxesData(new Regrow.Spot("Pine", 0, 0, 0, 0, 0, 0, 1.5f, [1.5f, 2f, 0.75f]));
        var field = Assert.Single(data!);
        Assert.Equal(("vec3", ZdoData.ScaleKey, "1.5,2,0.75"), (field.Section, field.Key, field.Value));
    }

    [Fact]
    public void AUniformSpotGetsNoData() =>
        Assert.Null(Growth.AxesData(new Regrow.Spot("Pine", 0, 0, 0, 0, 0, 0, 1.25f)));

    [Fact]
    public void ASpotsDataComesAfterItsScale()
    {
        int mark = StableHash.Of("mark");
        var data = Growth.DataOf(new Regrow.Spot("Pine", 0, 0, 0, 0, 0, 0, 1.5f, [1.5f, 2f, 0.75f], [new ValheimGen.ContentField("ints", mark, "1")]));
        Assert.Equal([("vec3", ZdoData.ScaleKey), ("ints", mark)], data!.Select(f => (f.Section, f.Key)));
        Assert.Null(Growth.DataOf(new Regrow.Spot("Pine", 0, 0, 0, 0, 0, 0, 1.25f)));
    }
}
