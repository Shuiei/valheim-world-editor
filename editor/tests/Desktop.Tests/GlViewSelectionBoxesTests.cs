using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The selection's box lines (GlView.SelectionBoxes): one object's own box, or one box round several.
/// </summary>
public class GlViewSelectionBoxesTests
{
    /// <summary>A box's 12 edges, two points of three floats each.</summary>
    private const int Floats = 12 * 2 * 3;

    [Fact]
    public void OneObjectGetsItsOwnBox()
    {
        float[] segs = GlView.SelectionBoxes([(new Vector3(0, 0, 0), new Vector3(1, 2, 1))]);
        Assert.Equal(Floats, segs.Length);
        // The box's first edge runs along x from its min corner.
        Assert.Equal([0, 0, 0, 1, 0, 0], segs[..6]);
    }

    [Fact]
    public void SeveralObjectsGetOneBoxRoundThemAll()
    {
        var bounds = Enumerable.Range(0, 3).Select(i => (new Vector3(i, -i, 0), new Vector3(i + 1, 2, 1 + i))).ToList();
        float[] segs = GlView.SelectionBoxes(bounds);
        Assert.Equal(Floats, segs.Length);
        Assert.Equal(-2, segs.Min());
        Assert.Equal([0, -2, 0, 3, -2, 0], segs[..6]);
        // Each point's x and z: the box reaches 3 east and 3 north.
        Assert.Equal(3, Enumerable.Range(0, segs.Length / 3).Max(k => segs[k * 3]));
        Assert.Equal(3, Enumerable.Range(0, segs.Length / 3).Max(k => segs[k * 3 + 2]));
    }

    [Fact]
    public void NothingSelectedMakesNoLines()
    {
        Assert.Empty(GlView.SelectionBoxes([]));
    }
}
