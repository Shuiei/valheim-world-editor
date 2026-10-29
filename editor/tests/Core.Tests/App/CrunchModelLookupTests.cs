using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The lookup table of a crn Huffman code (CrunchModel's table, built by its constructor): each short code fills every table
/// entry that starts with its bits, with its symbol and its length.
/// </summary>
public class CrunchModelLookupTests
{
    /// <summary>
    /// A code over 17 symbols (so the table is 6 bits wide): symbol 0 is a one-bit code, symbols 1 and 2 are the two-bit
    /// codes 10 and 11, the rest are unused.
    /// </summary>
    private static CrunchModel Model()
    {
        byte[] sizes = new byte[17];
        sizes[0] = 1;
        sizes[1] = 2;
        sizes[2] = 2;
        return new CrunchModel(sizes);
    }

    [Fact]
    public void TheTableHasOneEntryForEachPatternOfItsWidth()
    {
        CrunchModel model = Model();
        Assert.Equal(6, model.TableBits);
        Assert.Equal(64, model.Lookup.Length);
    }

    [Fact]
    public void AOneBitCodeFillsHalfTheTable()
    {
        Assert.All(Model().Lookup[..32], entry => Assert.Equal(0x10000u, entry));
    }

    [Fact]
    public void ATwoBitCodeFillsItsQuarterOfTheTable()
    {
        Assert.All(Model().Lookup[32..48], entry => Assert.Equal(0x20001u, entry));
    }

    [Fact]
    public void TheLastQuarterIsTheOtherTwoBitCode()
    {
        Assert.All(Model().Lookup[48..], entry => Assert.Equal(0x20002u, entry));
    }
}
