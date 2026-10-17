using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The mask's judgement of one ground point: the biome, height, slope and paint rules, and that the mask's settings and
/// the ground are copied when the judge is made.
/// </summary>
public class MaskJudgeTests
{
    /// <summary>The ground's width and height in grid points.</summary>
    private const int N = 5;

    /// <summary>A flat ground of 5 by 5 points at 30 m, with no paint.</summary>
    private static Ground Flat()
    {
        var g = new Ground(N, N, 0, 0, 1);
        for (int p = 0; p < N * N; p++)
        {
            g.Base[p] = 30;
        }
        return g;
    }

    [Fact]
    public void AMaskWithNoLimitsPassesEveryPoint()
    {
        var judge = new MaskJudge(new Mask { On = true }, Flat(), new int[N * N]);
        Assert.Equal(1, judge.Test(0));
        Assert.Equal(1, judge.Test(N * N - 1));
    }

    [Fact]
    public void ABiomeNotChosenFails()
    {
        var mask = new Mask { On = true };
        mask.Biomes.Add(8);
        int[] biomes = new int[N * N];
        Array.Fill(biomes, 8);
        biomes[3] = 1;
        var judge = new MaskJudge(mask, Flat(), biomes);
        Assert.Equal(1, judge.Test(4));
        Assert.Equal(0, judge.Test(3));
    }

    [Fact]
    public void APointPastTheBiomeListFails()
    {
        var mask = new Mask { On = true };
        mask.Biomes.Add(8);
        int[] biomes = new int[10];
        Array.Fill(biomes, 8);
        var judge = new MaskJudge(mask, Flat(), biomes);
        Assert.Equal(1, judge.Test(9));
        Assert.Equal(0, judge.Test(20));
    }

    [Fact]
    public void AHeightOutsideTheRangeFails()
    {
        var low = new MaskJudge(new Mask { On = true, HeightMin = 31 }, Flat(), new int[N * N]);
        Assert.Equal(0, low.Test(0));
        var high = new MaskJudge(new Mask { On = true, HeightMax = 30 }, Flat(), new int[N * N]);
        Assert.Equal(1, high.Test(0));
    }

    [Fact]
    public void ASlopeOnFlatGroundIsNoSlope()
    {
        Assert.Equal(0, new MaskJudge(new Mask { On = true, SlopeMin = 1 }, Flat(), new int[N * N]).Test(12));
        Assert.Equal(1, new MaskJudge(new Mask { On = true, SlopeMax = 45 }, Flat(), new int[N * N]).Test(12));
    }

    [Fact]
    public void PaintFollowsTheRuleAndTheGroundsOwnPaint()
    {
        var g = Flat();
        g.PMod[1] = 1;
        g.Paint[1 * 4] = 1;
        var dirt = new MaskJudge(new Mask { On = true, Paint = Mask.PaintRule.Dirt }, g, new int[N * N]);
        Assert.Equal(1, dirt.Test(1));
        Assert.Equal(0, dirt.Test(2));
    }

    [Fact]
    public void LaterChangesToTheMaskDoNotChangeTheJudgement()
    {
        var mask = new Mask { On = true, HeightMin = 31 };
        var judge = new MaskJudge(mask, Flat(), new int[N * N]);
        mask.HeightMin = null;
        Assert.Equal(0, judge.Test(0));
    }
}
