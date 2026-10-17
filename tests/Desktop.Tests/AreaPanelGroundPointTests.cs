using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Restoring a backup's ground point by point: a point the same as in the backup is left alone, one inside the soft edge
/// takes the backup's edits outright, and one outside it moves part of the way, with the paint blended by the weight.
/// </summary>
public class AreaPanelGroundPointTests
{
    /// <summary>
    /// A flat 129 × 129 ground at 30 m.
    /// </summary>
    private static Ground Flat()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        Array.Fill(g.Base, 30f);
        return g;
    }

    /// <summary>
    /// A grid point in the middle of the ground.
    /// </summary>
    private const int P = 64 * 129 + 64;

    [Fact]
    public void APointIsTheSameWhenItsEditsMatchAndNotWhenTheyDiffer()
    {
        var g = Flat();
        var bk = Flat();
        Assert.True(AreaPanel.SameGroundPoint(g, bk, P));

        g.Mod[P] = 1;
        Assert.False(AreaPanel.SameGroundPoint(g, bk, P));
    }

    [Fact]
    public void CopyTakesTheBackupsEditsAndItsLift()
    {
        var g = Flat();
        var bk = Flat();
        bk.Mod[P] = 2;
        bk.Level[P] = 0.5f;
        bk.Smooth[P] = 0.25f;
        bk.PMod[P] = 1;
        bk.Paint[P * 4] = 0.1f;
        bk.Paint[P * 4 + 1] = 0.2f;
        bk.Paint[P * 4 + 2] = 0.3f;
        bk.Paint[P * 4 + 3] = 0.4f;
        bk.Lift[P] = 1f;

        AreaPanel.CopyGroundPoint(g, bk, P);

        Assert.Equal(2, g.Mod[P]);
        Assert.Equal(0.5f, g.Level[P]);
        Assert.Equal(0.25f, g.Smooth[P]);
        Assert.Equal(1, g.PMod[P]);
        Assert.Equal(0.4f, g.Paint[P * 4 + 3], 4);
        // The backup's original ground (30 + 1) as a lift on this ground's 30.
        Assert.Equal(1f, g.Lift[P], 4);
    }

    [Fact]
    public void BlendMovesThePointPartWayToTheBackupHeight()
    {
        var g = Flat();
        var bk = Flat();
        Array.Fill(bk.Base, 40f);

        AreaPanel.BlendGroundPoint(g, bk, P, 0.5f);

        Assert.Equal(35f, g.HeightOf(P), 2);
        Assert.Equal(0, g.PMod[P]);
    }

    [Fact]
    public void BlendTakesThePaintFromABackupThatHasPaint()
    {
        var g = Flat();
        var bk = Flat();
        bk.PMod[P] = 1;
        bk.Paint[P * 4] = 1f;
        bk.Paint[P * 4 + 1] = 0f;
        bk.Paint[P * 4 + 2] = 0f;
        bk.Paint[P * 4 + 3] = 0f;

        AreaPanel.BlendGroundPoint(g, bk, P, 0.5f);

        // Half the way from the plain colour (0, 0, 0, 1) to the backup's paint.
        Assert.Equal(1, g.PMod[P]);
        Assert.Equal(0.5f, g.Paint[P * 4], 4);
        Assert.Equal(0.5f, g.Paint[P * 4 + 3], 4);
    }
}
