using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The selection's box lines (GlView.SelectionBoxes): a box round each selected object, or one box round them all past
/// GlView.BoxEachUpTo of them.
/// </summary>
public class GlViewSelectionBoxesTests
{
    /// <summary>A box's 12 edges, two points of three floats each.</summary>
    private const int Floats = 12 * 2 * 3;

    [Fact]
    public void EachObjectGetsABoxUpToTheLimit()
    {
        var bounds = Enumerable.Range(0, GlView.BoxEachUpTo).Select(i => (new Vector3(i, 0, 0), new Vector3(i + 1, 2, 1))).ToList();
        float[] segs = GlView.SelectionBoxes(bounds);
        Assert.Equal(Floats * GlView.BoxEachUpTo, segs.Length);
        // The first box's first edge runs along x from its min corner.
        Assert.Equal([0, 0, 0, 1, 0, 0], segs[..6]);
    }

    [Fact]
    public void PastTheLimitOneBoxRoundsThemAll()
    {
        var bounds = Enumerable.Range(0, GlView.BoxEachUpTo + 1).Select(i => (new Vector3(i, -i, 0), new Vector3(i + 1, 2, 1 + i))).ToList();
        float[] segs = GlView.SelectionBoxes(bounds);
        Assert.Equal(Floats, segs.Length);
        float n = GlView.BoxEachUpTo;
        Assert.Equal(-n, segs.Min());
        Assert.Equal([0, -n, 0, n + 1, -n, 0], segs[..6]);
        Assert.Equal(n + 1, segs.Where((_, i) => i % 3 == 0).Max());
        Assert.Equal(n + 1, segs.Where((_, i) => i % 3 == 2).Max());
    }

    [Fact]
    public void NothingSelectedMakesNoLines()
    {
        Assert.Empty(GlView.SelectionBoxes([]));
    }
}
