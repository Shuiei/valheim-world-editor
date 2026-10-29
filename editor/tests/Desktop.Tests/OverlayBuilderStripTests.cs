using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A polyline added to an overlay layer: each consecutive pair of points is one segment, two floats per point's three.
/// </summary>
public class OverlayBuilderStripTests
{
    [Fact]
    public void EachPairOfConsecutivePointsIsOneSegment()
    {
        var b = new OverlayBuilder(TestScenes.FlatScene());

        b.Strip(Overlays.Layer.Borders, [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 2)]);

        Assert.Equal(new float[] { 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 2 }, b.Finish()[Overlays.Layer.Borders]);
    }

    [Fact]
    public void AOnePointLineAddsNothing()
    {
        var b = new OverlayBuilder(TestScenes.FlatScene());

        b.Strip(Overlays.Layer.Markers, [new Vector3(5, 5, 5)]);

        Assert.Empty(b.Finish().GetValueOrDefault(Overlays.Layer.Markers) ?? []);
    }
}
