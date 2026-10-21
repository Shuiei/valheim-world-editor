using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The bits a crn table needs for a count (CrunchModel's ceiling of log2): the smallest number of bits whose
/// power of two is at least the count.
/// </summary>
public class CrunchModelCeilLog2Tests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    public void TheBitsAreTheSmallestPowerOfTwoCoveringTheCount(int count, int bits)
    {
        Assert.Equal(bits, PrivateMember.Call<int>(typeof(TerrainEditor.App.CrunchModel), "CeilLog2", null, count));
    }
}
