using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What an Area action does to one grid point: Flatten, Raise and Lower move it by its weight, Smooth blends it to the
/// average of the heights before, Naturalize aims at the broad shape and the bumps, Paint mixes the colour in, and
/// Restore takes its lift back. Each is the brushes' and the Path tool's action (GroundOps).
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
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Flatten, 34f, 0f, new float[4], HeightsBefore.None(g));

        // Within the game's ±8 m: no limit reached.
        Assert.False(change.Change(Inside, 0.5f));
        Assert.Equal(32f, g.HeightOf(Inside), 3);
    }

    [Fact]
    public void RaiseAndLowerMoveThePointByTheAmountTimesTheWeight()
    {
        var g = Flat();
        var raise = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Raise, 0f, 2f, new float[4], HeightsBefore.None(g));
        raise.Change(Inside, 1f);
        Assert.Equal(32f, g.HeightOf(Inside), 3);

        var lower = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Lower, 0f, 4f, new float[4], HeightsBefore.None(g));
        lower.Change(Inside, 0.5f);
        Assert.Equal(30f, g.HeightOf(Inside), 3);
    }

    [Fact]
    public void SmoothBlendsThePointToTheBlurOfTheHeightsBeforeIt()
    {
        var g = Flat();
        // One point 3 m up; its blur over the 7 × 7 square round it, of which 48 points are 30 m.
        g.SetHeight(Inside, 33f);
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Smooth, 0f, 0f, new float[4], HeightsBefore.Take(g, 64, 64, 64, 64, GroundOps.SmoothReach));

        change.Change(Inside, 1f);
        Assert.Equal((48 * 30f + 33f) / 49f, g.HeightOf(Inside), 2);
    }

    [Fact]
    public void PaintMixesTheColourInByTheWeight()
    {
        var g = Flat();
        float[] paint = [0.2f, 0.4f, 0.6f, 0.8f];
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Paint, 0f, 0f, paint, HeightsBefore.None(g));

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
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Restore, 0f, 0f, new float[4], HeightsBefore.None(g));

        // Halfway: half of the lift stays.
        Assert.False(change.Change(Inside, 0.5f));
        Assert.Equal(1f, g.Lift[Inside], 4);

        // All the way (more than 0.98): none.
        change.Change(Inside, 1f);
        Assert.Equal(0f, g.Lift[Inside]);
    }

    [Fact]
    public void PaintWithFullWeightSetsThePaint()
    {
        var g = Flat();
        var change = new AreaTool.CellChange(g, null!, AreaTool.GroundAction.Paint, 0f, 0f, [1f, 0f, 0f, 0f], HeightsBefore.None(g));

        Assert.False(change.Change(Inside, 1f));
        Assert.Equal(1f, g.Paint[Inside * 4], 4);
        Assert.Equal(0f, g.Paint[Inside * 4 + 3], 4);
    }

    [Fact]
    public void NaturalizeAimsAtTheBroadShapeAndTheBrushsBumps()
    {
        var g = Flat();
        var b = new Brush { NoiseAmp = 2, NoiseSize = 12, Noise = new Noise(7) };
        int reach = GroundOps.NaturalReach(b);
        var before = HeightsBefore.Take(g, 64, 64, 64, 64, reach);
        var change = new AreaTool.CellChange(g, b, AreaTool.GroundAction.Natural, 0f, 0f, new float[4], before);

        change.Change(Inside, 1f);
        // Flat ground: the broad shape is 30 m, the bumps come on top.
        Assert.Equal(GroundOps.NaturalTarget(g, b, Inside, 30f), g.HeightOf(Inside), 3);
    }

    [Theory]
    [InlineData(AreaTool.GroundAction.Smooth, 3)]
    [InlineData(AreaTool.GroundAction.Flatten, -1)]
    [InlineData(AreaTool.GroundAction.Restore, -1)]
    [InlineData(AreaTool.GroundAction.Paint, -1)]
    public void OnlySmoothAndNaturalizeReadTheGroundRoundAPoint(AreaTool.GroundAction act, int reach)
    {
        Assert.Equal(reach, AreaTool.ReachOf(act, new Brush()));
        Assert.Equal(GroundOps.NaturalReach(new Brush()), AreaTool.ReachOf(AreaTool.GroundAction.Natural, new Brush()));
    }
}
