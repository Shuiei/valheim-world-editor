using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The erosion steps on their own: thermal material sliding down a steep point, what is left alone, the drop's speed
/// and the resting angle.
/// </summary>
public class ErosionStepsTests
{
    /// <summary>
    /// A 5 × 5 window of ground that steps up across it: each cell's height is its column (0 to 4).
    /// </summary>
    private static float[] SteppedGround()
    {
        float[] h = new float[25];
        for (int i = 0; i < h.Length; i++)
        {
            h[i] = i % 5;
        }
        return h;
    }

    [Fact]
    public void ThermalSlidesMaterialFromASteepPointAndKeepsTheTotal()
    {
        float[] h = new float[9];
        h[4] = 10;
        float[] wt = [.. Enumerable.Repeat(1f, 9)];
        Erosion.Thermal(h, wt, 3, 3, talus: 0.5f, amount: 1, passes: 1);
        Assert.True(h[4] < 10);
        Assert.Equal(10.0, h.Sum(x => (double)x), 3);
    }

    [Fact]
    public void ThermalLeavesAFlatWindowAlone()
    {
        float[] h = new float[9];
        Erosion.Thermal(h, [.. Enumerable.Repeat(1f, 9)], 3, 3, 0.5f, 1, 3);
        Assert.All(h, x => Assert.Equal(0f, x));
    }

    [Fact]
    public void ThermalDoesNotMoveAPointWithNoWeight()
    {
        float[] h = new float[9];
        h[4] = 10;
        float[] wt = [.. Enumerable.Repeat(1f, 9)];
        wt[4] = 0;
        Erosion.Thermal(h, wt, 3, 3, 0.5f, 1, 1);
        Assert.Equal(10f, h[4]);
    }

    [Fact]
    public void HydraulicWithNoDropsChangesNothing()
    {
        float[] h = SteppedGround();
        float[] before = (float[])h.Clone();
        Erosion.Hydraulic(h, [.. Enumerable.Repeat(1f, 25)], 5, 5, 0, 1, new Random(1));
        Assert.Equal(before, h);
    }

    [Fact]
    public void HydraulicLeavesAWindowWithNoWeightAlone()
    {
        float[] h = SteppedGround();
        float[] before = (float[])h.Clone();
        Erosion.Hydraulic(h, new float[25], 5, 5, 50, 1, new Random(1));
        Assert.Equal(before, h);
    }

    [Fact]
    public void ADropGoingDownhillSpeedsUpAndGoingUphillStops()
    {
        Assert.Equal(MathF.Sqrt(8), Erosion.DropSpeed(2, -1), 4);
        Assert.Equal(0f, Erosion.DropSpeed(1, 1), 4);
    }

    [Fact]
    public void TalusIsTheSlopeOfTheRestingAngle()
    {
        Assert.Equal(1.0f, Erosion.Talus(45), 4);
        Assert.Equal(0f, Erosion.Talus(0), 4);
    }
}
