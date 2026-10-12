using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mask's rules one by one on a ground made up for them: biomes (and points past the biome list), the height and
/// slope ranges at both ends, every paint rule, a frozen mask, the warnings and Alt + Shift + click's height range.
/// </summary>
public class MaskBranchesTests
{
    /// <summary>The ground's width and height in grid points.</summary>
    private const int N = 65;

    /// <summary>A grid point's index.</summary>
    private static int P(int x, int z) => z * N + x;

    /// <summary>
    /// One zone at 30 m with a 10 m cliff up to 40 m from x = 40 east, and five painted test points on row 10: dirt,
    /// cultivated, paved, painted black, and never painted.
    /// </summary>
    private static Ground Ground()
    {
        var g = new Ground(N, N, 0, 0, 1);
        for (int p = 0; p < N * N; p++)
        {
            g.Base[p] = p % N >= 40 ? 40 : 30;
        }
        float[][] paints = [[1, 0, 0, 1], [0, 1, 0, 1], [0, 0, 1, 1], [0, 0, 0, 1]];
        for (int i = 0; i < paints.Length; i++)
        {
            g.PMod[P(i, 10)] = 1;
            Array.Copy(paints[i], 0, g.Paint, P(i, 10) * 4, 4);
        }
        return g;
    }

    /// <summary>Whether the five test points on row 10 pass under a paint rule.</summary>
    private static bool[] Pass(Mask.PaintRule rule)
    {
        Func<int, float> f = new Mask { On = true, Paint = rule }.For(Ground(), new int[N * N])!;
        return [.. Enumerable.Range(0, 5).Select(x => f(P(x, 10)) > 0)];
    }

    [Fact]
    public void EachPaintRuleLetsItsGroundThrough()
    {
        Assert.Equal(new[] { true, true, true, true, true }, Pass(Mask.PaintRule.Any));
        Assert.Equal(new[] { false, false, false, true, true }, Pass(Mask.PaintRule.Unpainted));
        Assert.Equal(new[] { true, true, true, false, false }, Pass(Mask.PaintRule.Painted));
        Assert.Equal(new[] { true, false, false, false, false }, Pass(Mask.PaintRule.Dirt));
        Assert.Equal(new[] { false, true, false, false, false }, Pass(Mask.PaintRule.Cultivated));
        Assert.Equal(new[] { false, false, true, false, false }, Pass(Mask.PaintRule.Paved));
    }

    [Fact]
    public void TheHeightRangeHasTwoEnds()
    {
        Ground g = Ground();
        int[] biomes = new int[N * N];
        Func<int, float> f = new Mask { On = true, HeightMin = 29, HeightMax = 35 }.For(g, biomes)!;
        Assert.Equal(1, f(P(10, 30)));
        Assert.Equal(0, f(P(50, 30)));
        f = new Mask { On = true, HeightMin = 31 }.For(g, biomes)!;
        Assert.Equal(0, f(P(10, 30)));
        Assert.Equal(1, f(P(50, 30)));
        f = new Mask { On = true, HeightMax = 35 }.For(g, biomes)!;
        Assert.Equal(1, f(P(10, 30)));
    }

    [Fact]
    public void TheSlopeRangeHasTwoEnds()
    {
        Ground g = Ground();
        int[] biomes = new int[N * N];
        // At the cliff: atan(10 m over 2 m) is about 79°; elsewhere flat.
        Func<int, float> f = new Mask { On = true, SlopeMin = 10, SlopeMax = 80 }.For(g, biomes)!;
        Assert.Equal(1, f(P(39, 30)));
        Assert.Equal(0, f(P(10, 30)));
        f = new Mask { On = true, SlopeMax = 45 }.For(g, biomes)!;
        Assert.Equal(0, f(P(39, 30)));
        Assert.Equal(1, f(P(10, 30)));
        // The block's corners use the points there are.
        Assert.Equal(1, f(0));
        Assert.Equal(1, f(N * N - 1));
    }

    [Fact]
    public void BiomesPassOnlyWhereKnown()
    {
        var m = new Mask { On = true };
        m.Biomes.Add(8);
        // A biome list shorter than the ground: points past it do not pass.
        int[] biomes = new int[10];
        Array.Fill(biomes, 8);
        biomes[3] = 1;
        Func<int, float> f = m.For(Ground(), biomes)!;
        Assert.Equal(1, f(5));
        Assert.Equal(0, f(3));
        Assert.Equal(0, f(20));
    }

    [Fact]
    public void AFrozenMaskRemembersItsAnswers()
    {
        Func<int, float> f = new Mask { On = true, HeightMax = 35 }.For(Ground(), new int[N * N], frozen: true)!;
        Assert.Equal(1, f(P(10, 30)));
        Assert.Equal(1, f(P(10, 30)));
        Assert.Equal(0, f(P(50, 30)));
        Assert.Equal(0, f(P(50, 30)));
    }

    [Fact]
    public void TheWarningsSayWhatLetsNothingThrough()
    {
        Assert.Equal("", new Mask().Warning());
        Assert.Equal("", new Mask { SlopeMin = 10, SlopeMax = 20, HeightMin = 10, HeightMax = 20 }.Warning());
        Assert.StartsWith("Slope max 0° only lets perfectly flat ground through", new Mask { SlopeMax = 0 }.Warning());
        Assert.Equal("Height min is above max: nothing matches.", new Mask { HeightMin = 50, HeightMax = 20 }.Warning());
        string both = new Mask { SlopeMin = 5, SlopeMax = -1, HeightMin = 9, HeightMax = 1 }.Warning();
        Assert.Contains("Slope min is above max", both);
        Assert.Contains("Height min is above max", both);
    }

    [Fact]
    public void PickingAHeightTurnsTheMaskOnAndTellsListeners()
    {
        var m = new Mask();
        // Nobody listening yet: nothing happens.
        m.Notify();
        int told = 0;
        m.Changed += () => told++;
        m.PickHeight(41.26f);
        Assert.True(m.On);
        Assert.Equal(39.3f, m.HeightMin!.Value, 3);
        Assert.Equal(43.3f, m.HeightMax!.Value, 3);
        Assert.Equal(1, told);
    }
}
