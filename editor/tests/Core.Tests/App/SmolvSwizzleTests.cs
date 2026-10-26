using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A compact shuffle's swizzle (SmolvOperands.Swizzle): one byte holding four 2-bit components, read only as far as the
/// instruction's length reaches.
/// </summary>
public class SmolvSwizzleTests
{
    private static List<uint> Swizzle(byte packed, int length)
    {
        var output = new List<uint>();
        var reader = new SmolvReader([packed], 0);
        PrivateCall.Static(typeof(SmolvOperands), "Swizzle", reader, output, length);
        return output;
    }

    [Fact]
    public void AFullLengthInstructionReadsAllFourComponentsHighFirst()
    {
        // 0b11_10_01_00: components 3, 2, 1, 0 from the top bits down.
        Assert.Equal([3u, 2u, 1u, 0u], Swizzle(0xE4, 9));
    }

    [Fact]
    public void AShortInstructionReadsOnlyTheComponentsItsLengthReaches()
    {
        Assert.Equal([3u], Swizzle(0xE4, 6));
        Assert.Equal([3u, 2u], Swizzle(0xE4, 7));
    }

    [Fact]
    public void EachComponentIsMaskedToTwoBits()
    {
        // 0b00_00_11_01 : components 0, 0, 3, 1.
        Assert.Equal([0u, 0u, 3u, 1u], Swizzle(0x0D, 9));
    }
}
