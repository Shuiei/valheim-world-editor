using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map's line frame: a world point maps to clip space with the frame's centre at zero, a half width of the view at
/// one, scaled by the metres a device pixel covers.
/// </summary>
public sealed class MapViewLineFrameTests
{
    /// <summary>The frame's centre is the middle of the clip space.</summary>
    [Fact]
    public void TheCentreIsTheMiddleOfClipSpace()
    {
        var frame = new MapView.LineFrame(new Vector2(100f, 50f), 800, 600, 1f);

        Assert.Equal(new Vector2(0f, 0f), frame.C(100f, 50f));
    }

    /// <summary>A point half the view's width away, at one metre a pixel, is at the right edge.</summary>
    [Fact]
    public void APointHalfTheViewAwayIsAtTheEdge()
    {
        var frame = new MapView.LineFrame(new Vector2(0f, 0f), 800, 600, 1f);

        Assert.Equal(new Vector2(1f, 0f), frame.C(400f, 0f));
        Assert.Equal(new Vector2(0f, -1f), frame.C(0f, -300f));
    }

    /// <summary>The metres a device pixel covers scale the distance: at two metres a pixel the same point is half as far.</summary>
    [Fact]
    public void TheMetresAPixelCoversScaleTheDistance()
    {
        var frame = new MapView.LineFrame(new Vector2(0f, 0f), 800, 600, 2f);

        Assert.Equal(new Vector2(0.5f, 0f), frame.C(400f, 0f));
    }

    /// <summary>A thick box is that many boxes, each a pixel inside the last; one pixel thick is the box itself.</summary>
    [Fact]
    public void AThickBoxIsBoxesAPixelApart()
    {
        var frame = new MapView.LineFrame(new Vector2(0f, 0f), 100, 100, 2f);
        List<float> thick = [], one = [], box = [];
        frame.ThickBox(thick, -20, -20, 20, 20, 2);
        frame.ThickBox(one, -20, -20, 20, 20, 1);
        frame.Box(box, -20, -20, 20, 20);
        Assert.Equal(box, one);
        Assert.Equal(48, thick.Count);
        Assert.Equal(box, thick[..24]);
        // The second box starts a pixel (2 m) inside the first's corner.
        Assert.Equal(frame.C(-18, -18), new Vector2(thick[24], thick[25]));
    }
}
