using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Taking a change's ground and paint back off the points it changed, and the zone reset entries a removal puts back.
/// </summary>
public class EditSessionRevertTests
{
    [Fact]
    public void RevertPointsPutsEachPointsLevelBackToItsValueBefore()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 2, 3, 0, "A");
        var c = s.UndoList[^1];
        int k = Array.IndexOf(c.Points, 32 * s.Ground.W + 32);
        Assert.True(k >= 0);
        s.RevertPoints(c);
        Assert.Equal(c.Before.Level[k], s.Ground.Level[c.Points[k]]);
    }

    [Fact]
    public void RevertPaintLeavesPaintThatALaterStepChanged()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "A");
        var c = s.UndoList[^1];
        int k = 0, p = c.Points[k];
        // A later step repainted the point: its paint differs from what this change left, so it stays.
        s.Ground.PMod[p] = c.After.PMod[k];
        s.Ground.Paint[p * 4] = c.After.Paint[k * 4] + 0.5f;
        float painted = s.Ground.Paint[p * 4];
        s.RevertPaint(c, k, p);
        Assert.Equal(painted, s.Ground.Paint[p * 4]);
    }

    [Fact]
    public void RevertPaintRestoresPaintNothingRepaintedSinceTheChange()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "A");
        var c = s.UndoList[^1];
        int k = 0, p = c.Points[k];
        s.Ground.PMod[p] = c.After.PMod[k];
        for (int ch = 0; ch < 4; ch++)
        {
            s.Ground.Paint[p * 4 + ch] = c.After.Paint[k * 4 + ch];
        }
        s.RevertPaint(c, k, p);
        Assert.Equal(c.Before.PMod[k], s.Ground.PMod[p]);
        for (int ch = 0; ch < 4; ch++)
        {
            Assert.Equal(c.Before.Paint[k * 4 + ch], s.Ground.Paint[p * 4 + ch]);
        }
    }

    [Fact]
    public void ResetsToRevertPutBackTheMarkingTheStepTookOff()
    {
        var s = TestScenes.Flat(1);
        s.MarkResets([(0, 0)], false, false, "Reset");
        var entries = s.ResetsToRevert(s.UndoList[^1]);
        // The zone is still marked as the step left it: the step's marking comes off again (before: none).
        Assert.Single(entries);
        var (_, before, after) = entries[0];
        Assert.True(before);
        Assert.False(after);
    }
}
