using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The op swaps SMOL-V makes (SmolvOpcodes.BuildSwap): each pair maps both ways, and no op is swapped with itself.
/// </summary>
public class SmolvOpcodesSwapTests
{
    /// <summary>The swaps the table builds.</summary>
    private static Dictionary<int, int> Swaps() => (Dictionary<int, int>)PrivateCall.Static(typeof(SmolvOpcodes), "BuildSwap")!;

    [Fact]
    public void EachSwapMapsBothWays()
    {
        var swaps = Swaps();

        Assert.Equal(0, swaps[71]);
        Assert.Equal(71, swaps[0]);
        Assert.Equal(15, swaps[127]);
        Assert.Equal(127, swaps[15]);
    }

    [Fact]
    public void NoOpIsSwappedWithItself()
    {
        Assert.All(Swaps(), s => Assert.NotEqual(s.Key, s.Value));
    }

    [Fact]
    public void EveryOpSwappedHasItsPartner()
    {
        var swaps = Swaps();

        Assert.All(swaps, s => Assert.Equal(s.Key, swaps[s.Value]));
    }
}
