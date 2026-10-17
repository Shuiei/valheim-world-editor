using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The public cellular sample: the point it is given is scaled by the noise's frequency (a hundredth) before the cell
/// distance is taken there.
/// </summary>
public class FastNoiseCellularSamplingTests
{
    [Fact]
    public void ASamplePointIsScaledByTheFrequencyBeforeTheCellDistanceIsTaken()
    {
        var noise = new FastNoise(1337);
        // A hundredth of (500, -300) is (5, -3): the same distance as sampling there directly.
        Assert.Equal(noise.SingleCellular(5.0, -3.0), noise.GetCellular(500.0, -300.0), 9);
    }
}
