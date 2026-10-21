using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Png's Paeth predictor (the PNG filter that picks whichever of left, up and up-left is nearest to left + up - up-left).
/// </summary>
public class PngPaethPredictorTests
{
    [Fact]
    public void PaethPicksTheUpNeighbourWhenItIsNearest()
    {
        // The estimate is 10 + 20 - 10 = 20: up (20) is 0 away, left and up-left are 10 away.
        Assert.Equal(20, PrivateMember.Call<int>(typeof(Png), "Paeth", null, 10, 20, 10));
    }

    [Fact]
    public void PaethPicksTheLeftNeighbourWhenItIsNearest()
    {
        // The estimate is 20 + 10 - 10 = 20: left (20) is 0 away, up and up-left are 10 away.
        Assert.Equal(20, PrivateMember.Call<int>(typeof(Png), "Paeth", null, 20, 10, 10));
    }

    [Fact]
    public void PaethPicksUpLeftWhenItIsNearest()
    {
        // The estimate is 0 + 20 - 10 = 10, which is the up-left value itself: up-left is 0 away, left and up 10 away.
        Assert.Equal(10, PrivateMember.Call<int>(typeof(Png), "Paeth", null, 0, 20, 10));
    }
}
