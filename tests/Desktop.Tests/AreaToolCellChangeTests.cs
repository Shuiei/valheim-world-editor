using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What an Area action does to one grid point: Flatten, Raise and Lower move it by its weight, Smooth blends it to the
/// blur of the heights before, Paint mixes the colour in, and Restore takes its lift back.
/// </summary>
public class AreaToolCellChangeTests
{
    /// <summary>
    /// A flat 129 × 129 ground at 30 m, as the Area tests use.
    /// </summary>
    private static Ground Flat()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        Array.Fill(g.Base, 30f);
        return g;
    }

    /// <summary>
    /// A grid point in the middle of the ground (64, 64), with its 7 × 7 square inside it.
    /// </summary>
    private const int Inside = 64 * 129 + 64;

    [Fact]
    public void FlattenMovesThePointTowardsItsHeightByItsWeight()
    {
        var g = Flat();
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Flatten, 34f, 0f, new float[4], HeightSnapshot(g, -1), 0);

        // Within the game's ±8 m: no limit reached.
        Assert.False(change.Change(Inside, 0.5f));
        Assert.Equal(32f, g.HeightOf(Inside), 3);
    }

    [Fact]
    public void RaiseAndLowerMoveThePointByTheAmountTimesTheWeight()
    {
        var g = Flat();
        var raise = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Raise, 0f, 2f, new float[4], HeightSnapshot(g, -1), 0);
        raise.Change(Inside, 1f);
        Assert.Equal(32f, g.HeightOf(Inside), 3);

        var lower = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Lower, 0f, 4f, new float[4], HeightSnapshot(g, -1), 0);
        lower.Change(Inside, 0.5f);
        Assert.Equal(30f, g.HeightOf(Inside), 3);
    }

    [Fact]
    public void SmoothBlendsThePointToTheBlurOfTheHeightsBeforeIt()
    {
        var g = Flat();
        // One point 3 m up; its blur over the 7 × 7 square round it, of which 48 points are 30 m.
        g.SetHeight(Inside, 33f);
        var box = new AreaTool.Weights([], 64, 64, 64, 64, []);
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Smooth, 0f, 0f, new float[4], AreaTool.HeightSnapshot.Take(g, box, 3), 0);

        change.Change(Inside, 1f);
        Assert.Equal((48 * 30f + 33f) / 49f, g.HeightOf(Inside), 2);
    }

    [Fact]
    public void PaintMixesTheColourInByTheWeight()
    {
        var g = Flat();
        float[] paint = [0.2f, 0.4f, 0.6f, 0.8f];
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Paint, 0f, 0f, paint, HeightSnapshot(g, -1), 0);

        change.Change(Inside, 1f);
        for (int c = 0; c < 4; c++)
        {
            Assert.Equal(paint[c], g.Paint[Inside * 4 + c], 4);
        }
    }

    [Fact]
    public void RestoreTakesTheLiftBackByTheWeight()
    {
        var g = Flat();
        g.Lift[Inside] = 2f;
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Restore, 0f, 0f, new float[4], HeightSnapshot(g, -1), 0);

        // Halfway: half of the lift stays.
        change.RestorePoint(Inside, 0.5f);
        Assert.Equal(1f, g.Lift[Inside], 4);

        // All the way (more than 0.98): none.
        change.RestorePoint(Inside, 1f);
        Assert.Equal(0f, g.Lift[Inside]);
    }

    [Fact]
    public void PaintPointWithFullWeightSetsThePaint()
    {
        var g = Flat();
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Paint, 0f, 0f, [1f, 0f, 0f, 0f], HeightSnapshot(g, -1), 0);

        change.PaintPoint(Inside, 1f);
        Assert.Equal(1f, g.Paint[Inside * 4], 4);
        Assert.Equal(0f, g.Paint[Inside * 4 + 3], 4);
    }

    /// <summary>
    /// The heights before an action with no blur (none taken).
    /// </summary>
    private static AreaTool.HeightSnapshot HeightSnapshot(Ground g, int reach) => AreaTool.HeightSnapshot.Take(g, null!, reach);
}
