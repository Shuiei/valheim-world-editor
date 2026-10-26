using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Line segments made into strips (GlView.Thicken): each segment is two triangles of 18 floats, a segment of no length is
/// dropped, and a wider request makes a wider strip.
/// </summary>
public class GlViewThickenTests
{
    [AvaloniaFact]
    public void ASegmentIsTwoTrianglesOfEighteenFloats()
    {
        var b = new BareGlView(TestScenes.Flat(2));

        Assert.Equal(18, b.V.Thicken([0, 0, 0, 10, 0, 0], 1f).Length);
    }

    [AvaloniaFact]
    public void ASegmentOfNoLengthIsDropped()
    {
        var b = new BareGlView(TestScenes.Flat(2));

        Assert.Empty(b.V.Thicken([1, 1, 1, 1, 1, 1], 1f));
    }

    [AvaloniaFact]
    public void ADoubleWidthStripIsTwiceAsWide()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        float[] seg = [0, 0, 0, 10, 0, 0];

        float narrow = Span(b.V.Thicken(seg, 1f));
        float wide = Span(b.V.Thicken(seg, 2f));

        Assert.Equal(2 * narrow, wide, 4);
    }

    /// <summary>The distance across a strip at its start: from its first point to its second.</summary>
    private static float Span(float[] strip) =>
        Vector3.Distance(new Vector3(strip[0], strip[1], strip[2]), new Vector3(strip[3], strip[4], strip[5]));
}
