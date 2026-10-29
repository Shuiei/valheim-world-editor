using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A PNG row's filter types, undone one byte at a time with one byte per pixel (Png.Unfilter).
/// </summary>
public class PngUnfilterTests
{
    /// <summary>Unfilters one row of raw bytes (filter byte first) into a new row, with the previous row above it.</summary>
    private static byte[] Unfilter(byte[] raw, byte[] previous)
    {
        byte[] row = new byte[raw.Length - 1];
        PrivateMember.Call(typeof(Png), "Unfilter", null, raw, 0, row, previous, 1);
        return row;
    }

    [Fact]
    public void FilterZeroKeepsTheBytes()
    {
        Assert.Equal(new byte[] { 5, 6, 7 }, Unfilter([0, 5, 6, 7], [0, 0, 0]));
    }

    [Fact]
    public void SubFilterAddsTheByteBefore()
    {
        Assert.Equal(new byte[] { 5, 6, 7 }, Unfilter([1, 5, 1, 1], [0, 0, 0]));
    }

    [Fact]
    public void UpFilterAddsTheByteAbove()
    {
        Assert.Equal(new byte[] { 11, 21, 31 }, Unfilter([2, 1, 1, 1], [10, 20, 30]));
    }

    [Fact]
    public void AverageFilterAddsTheRoundedMeanOfTheByteBeforeAndAbove()
    {
        Assert.Equal(new byte[] { 5, 12, 21 }, Unfilter([3, 0, 0, 0], [10, 20, 30]));
    }

    [Fact]
    public void PaethFilterPicksTheNearestOfTheThreeNeighbours()
    {
        Assert.Equal(new byte[] { 10, 20, 30 }, Unfilter([4, 0, 0, 0], [10, 20, 30]));
    }

    [Fact]
    public void AnUnknownFilterTypeIsAFormatError()
    {
        Assert.Throws<InvalidDataException>(() => Unfilter([5, 1, 2, 3], [0, 0, 0]));
    }
}
