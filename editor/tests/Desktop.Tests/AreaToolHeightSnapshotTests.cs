using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The heights taken before an Area action (Smooth and Naturalize): the box round the selection and the blur's reach, and
/// the blur, the average of the points within its reach that the ground has.
/// </summary>
public class AreaToolHeightSnapshotTests
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
    /// A selection of one grid point (x, z).
    /// </summary>
    private static AreaTool.Weights Point(int x, int z) => new([], x, z, x, z, []);

    [Fact]
    public void TakeReadsTheBoxOfTheSelectionWithTheBlurReach()
    {
        var g = Flat();
        var snap = AreaTool.HeightSnapshot.Take(g, Point(10, 10), 2);

        // The 5 × 5 box from 8 to 12.
        Assert.Equal(8, snap.X0);
        Assert.Equal(8, snap.Z0);
        Assert.Equal(5, snap.W);
        Assert.Equal(25, snap.Heights.Length);
        Assert.Equal(30f, snap.Heights[0]);
    }

    [Fact]
    public void TakeWithNoBlurTakesNothing()
    {
        var snap = AreaTool.HeightSnapshot.Take(Flat(), Point(10, 10), -1);
        Assert.Empty(snap.Heights);
    }

    [Fact]
    public void BlurAveragesTheHeightsWithinItsReach()
    {
        var g = Flat();
        int center = 10 * g.W + 10;
        g.SetHeight(center, 33f);
        var snap = AreaTool.HeightSnapshot.Take(g, Point(10, 10), 1);

        // The 3 × 3 round the middle: one point 3 m up and eight at 30 m.
        Assert.Equal((8 * 30f + 33f) / 9f, snap.Blur(center, 1), 3);
        // No reach: the point itself.
        Assert.Equal(33f, snap.Blur(center, 0), 3);
    }

    [Fact]
    public void BlurAtTheCornerAveragesOnlyThePointsInside()
    {
        var g = Flat();
        var snap = AreaTool.HeightSnapshot.Take(g, Point(0, 0), 1);

        // The corner has four points inside the ground, all at 30 m.
        Assert.Equal(30f, snap.Blur(0, 1), 4);
    }
}
