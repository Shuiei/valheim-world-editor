using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A block written from the palettes (CrunchUnpacker.WriteBlock): a DXT1 block is its colour endpoint then its selectors, a
/// DXT5 block has its alpha part first, and each part is taken by its index.
/// </summary>
public class CrunchUnpackerBlockTests
{
    /// <summary>The colour endpoint 0x11223344 then the selectors 0xAABBCCDD, as the bytes a DXT1 block holds.</summary>
    private static readonly byte[] EndpointThenSelectors = [0x44, 0x33, 0x22, 0x11, 0xDD, 0xCC, 0xBB, 0xAA];

    /// <summary>
    /// A DXT1 block written with this colour endpoint and selector taken by index, and no alpha part.
    /// </summary>
    private static byte[] ColourBlock(int colour, int colourSelector, uint[] colourEndpoints, uint[] colourSelectors)
    {
        byte[] block = new byte[8];
        CrunchUnpacker.WriteBlock(block, false, colour, colourSelector, 0, 0, colourEndpoints, colourSelectors, [], []);
        return block;
    }

    [Fact]
    public void AColourBlockIsItsEndpointThenItsSelectors()
    {
        Assert.Equal(
            EndpointThenSelectors,
            ColourBlock(0, 0, [0x11223344u], [0xAABBCCDDu]));
    }

    [Fact]
    public void TheColourPartIsTakenByItsIndex()
    {
        Assert.Equal(
            EndpointThenSelectors,
            ColourBlock(1, 1, [1u, 0x11223344u], [0u, 0xAABBCCDDu]));
    }

    [Fact]
    public void ADxt5BlockWritesItsAlphaPartFirst()
    {
        byte[] block = new byte[16];
        CrunchUnpacker.WriteBlock(block, true, 0, 0, 0, 0, [0x11223344u], [0xAABBCCDDu], [0x0102], [0x0A0B, 0x0C0D, 0x0E0F]);
        Assert.Equal(
            new byte[] { 0x02, 0x01, 0x0B, 0x0A, 0x0D, 0x0C, 0x0F, 0x0E, 0x44, 0x33, 0x22, 0x11, 0xDD, 0xCC, 0xBB, 0xAA },
            block);
    }

    [Fact]
    public void TheAlphaSelectorsAreTakenByTheirIndexInThreeNumbers()
    {
        byte[] block = new byte[16];
        // Alpha selector 1 takes the numbers at 3, 4 and 5 of the alpha selectors.
        CrunchUnpacker.WriteBlock(block, true, 0, 0, 0, 1, [0u], [0u], [0x0102], [0, 0, 0, 0x0A0B, 0x0C0D, 0x0E0F]);
        Assert.Equal(new byte[] { 0x02, 0x01, 0x0B, 0x0A, 0x0D, 0x0C, 0x0F, 0x0E }, block[..8]);
    }
}
