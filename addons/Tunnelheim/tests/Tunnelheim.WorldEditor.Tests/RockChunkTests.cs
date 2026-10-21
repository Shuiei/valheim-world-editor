using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// One chunk of rock: seed ground until a sample changes (a write equal to the seed keeps no array), densities kept to
/// 1/256 m within ±4 m (halves to even), paint kept once a sample is painted, and a reset back to new seeds.
/// </summary>
public class RockChunkTests
{
    private static float[] Flat(float h) => [.. Enumerable.Repeat(h, ColumnRecord.SeedCount)];

    [Fact]
    public void AChunkReadsAsItsSeedGroundUntilChanged()
    {
        var c = new RockChunk(Flat(17.3f), 1);
        Assert.False(c.IsStored);
        // Sample (0, 2, 0): 16 m + 1 m up, 0.3 m under the ground.
        int i = RockGrid.Samples * 2;
        Assert.Equal(RockChunk.Quantize(0.3f), c[i]);
        c[i] = c[i];
        Assert.False(c.IsStored);
        c[i] = -1f;
        Assert.True(c.IsStored);
        Assert.Equal(-1f, c[i]);
        Assert.Equal(RockChunk.Encode(17.3f - 16f), c.Raw(0));
    }

    [Theory]
    [InlineData(10f, 1024)]
    [InlineData(-10f, -1024)]
    [InlineData(0.5f / 256f, 0)]
    [InlineData(1.5f / 256f, 2)]
    public void DensitiesAreHeldToTheBandInSteps(float v, short stored) => Assert.Equal(stored, RockChunk.Encode(v));

    [Fact]
    public void TheOldestCheckpointsGroundIsHeldWider() => Assert.Equal(10f, RockChunk.QuantizeWide(10f));

    [Fact]
    public void PaintIsKeptOnceASampleIsPaintedAndResetClearsAll()
    {
        var c = new RockChunk(Flat(0f), 0);
        c.Paint(3, RockPaint.None);
        Assert.False(c.IsPainted);
        c.Paint(3, RockPaint.Cultivated);
        c[3] = 2f;
        Assert.Equal(RockPaint.Cultivated, c.PaintAt(3));
        c.Reset(Flat(5f));
        Assert.False(c.IsPainted || c.IsStored);
        Assert.Equal(4f, c[0]);
    }
}
