using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Erosion: slopes settle to their rest angle, rain moves ground downhill.
/// </summary>
public class ErosionTests
{
    /// <summary>
    /// A 20 × 20 window with a 10 m cliff in the middle (x &gt;= 10 is high).
    /// </summary>
    private static (float[] H, float[] Wt) Cliff()
    {
        float[] h = new float[400];
        float[] wt = [.. Enumerable.Repeat(1f, 400)];
        for (int z = 0; z < 20; z++)
        {
            for (int x = 10; x < 20; x++)
            {
                h[z * 20 + x] = 10;
            }
        }
        return (h, wt);
    }

    [Fact]
    public void ThermalSettlesACliffAndKeepsTheGround()
    {
        var (h, wt) = Cliff();
        float sum = h.Sum();
        Erosion.Thermal(h, wt, 20, 20, Erosion.Talus(33), 1, 200);
        // No ground is made or lost, and the step is much less steep.
        Assert.Equal(sum, h.Sum(), 1);
        float step = h[10 * 20 + 10] - h[10 * 20 + 9];
        Assert.True(step < 3, $"{step}");
    }

    [Fact]
    public void PointsWithNoWeightStayPut()
    {
        var (h, wt) = Cliff();
        Array.Fill(wt, 0f);
        Erosion.Thermal(h, wt, 20, 20, Erosion.Talus(33), 1, 50);
        Erosion.Hydraulic(h, wt, 20, 20, 500, 1, new Random(1));
        Assert.Equal(10, h[10 * 20 + 10]);
        Assert.Equal(0, h[10 * 20 + 9]);
    }

    [Fact]
    public void RainCarriesGroundDownhill()
    {
        // A slope rising to the east.
        float[] h = new float[900];
        for (int i = 0; i < 900; i++)
        {
            h[i] = i % 30 * 0.5f;
        }
        float[] wt = [.. Enumerable.Repeat(1f, 900)];
        float[] before = (float[])h.Clone();
        Erosion.Hydraulic(h, wt, 30, 30, 3000, 1, new Random(3));
        float upper = Enumerable.Range(0, 900).Where(i => i % 30 is > 18 and < 27).Sum(i => h[i] - before[i]);
        Assert.True(upper < 0, $"the upper slope should lose ground: {upper}");
    }

    /// <summary>
    /// A 30 × 30 valley: a floor at x = 3, a steep wall to the west, a 2 m-per-point slope to the east. The points up to
    /// x = 5 may change fully; east of that they may change by 0.01, or (soft) by a share falling over 8 points to 0.01,
    /// with a locked block on the slope.
    /// </summary>
    private static (float[] H, float[] Wt) Valley(bool soft)
    {
        float[] h = new float[900];
        float[] wt = new float[900];
        for (int i = 0; i < 900; i++)
        {
            int x = i % 30, z = i / 30;
            h[i] = x >= 3 ? (x - 3) * 2f : (3 - x) * 3f;
            wt[i] = x <= 5 ? 1 : soft ? MathF.Max(0.01f, 1 - (x - 5) / 8f) : 0.01f;
            if (soft && z is > 12 and < 17 && x is > 6 and < 11)
            {
                wt[i] = 0;
            }
        }
        return (h, wt);
    }

    [Fact]
    public void RainNeitherMakesNorLosesGroundAtSoftEdgesAndLockedPoints()
    {
        var (h, wt) = Valley(soft: true);
        float[] before = (float[])h.Clone();
        Erosion.Hydraulic(h, wt, 30, 30, 3000, 1, new Random(3));
        Assert.Contains(Enumerable.Range(0, 900), i => MathF.Abs(h[i] - before[i]) > 0.1f);
        Assert.Equal(before.Sum(v => (double)v), h.Sum(v => (double)v), 0.01);
        Assert.All(Enumerable.Range(0, 900).Where(i => wt[i] == 0), i => Assert.Equal(before[i], h[i]));
    }

    [Fact]
    public void HeavyRainNextToNearlyLockedGroundDigsNoRunawayHoles()
    {
        var (h, wt) = Valley(soft: false);
        float[] before = (float[])h.Clone();
        Erosion.Hydraulic(h, wt, 30, 30, 10000, 1, new Random(0));
        float most = Enumerable.Range(0, 900).Max(i => MathF.Abs(h[i] - before[i]));
        Assert.True(most < 20, $"{most}");
    }
}
