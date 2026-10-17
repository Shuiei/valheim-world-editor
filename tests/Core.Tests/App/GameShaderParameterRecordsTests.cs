using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The records of a shader parameter blob: a name (a length, then identifier characters, padded to four bytes) and the
/// numbers after it up to the next name; numbers before the first name, and runs that are not identifiers, are not
/// records.
/// </summary>
public class GameShaderParameterRecordsTests
{
    /// <summary>
    /// The blob's first 24 bytes, which the records do not use.
    /// </summary>
    private static readonly byte[] Header = new byte[24];

    /// <summary>
    /// A number as its four little-endian bytes.
    /// </summary>
    private static byte[] Number(uint n) => BitConverter.GetBytes(n);

    /// <summary>
    /// A name: its length, its characters and zero bytes up to a multiple of four.
    /// </summary>
    private static byte[] Name(string s)
    {
        byte[] text = Encoding.ASCII.GetBytes(s);
        byte[] padding = new byte[(4 - text.Length % 4) % 4];
        return [.. Number((uint)text.Length), .. text, .. padding];
    }

    /// <summary>
    /// The records read from a blob made of these parts after its header, each as its name, a colon and its numbers
    /// separated by commas.
    /// </summary>
    private static string[] RecordsOf(params byte[][] parts)
    {
        byte[] blob = [.. Header, .. parts.SelectMany(p => p)];
        var records = (List<(string Name, List<uint> Nums)>)PrivateCall.Static(typeof(GameShader), "ParameterRecords", blob)!;
        return [.. records.Select(r => $"{r.Name}:{string.Join(",", r.Nums)}")];
    }

    [Fact]
    public void ANameFollowedByNumbersMakesOneRecordWithThoseNumbers()
    {
        string[] records = RecordsOf(Name("_Tint"), Number(7), Number(9));

        Assert.Equal(["_Tint:7,9"], records);
    }

    [Fact]
    public void EachNameStartsANewRecord()
    {
        string[] records = RecordsOf(Name("_A"), Number(1), Name("_Bee"), Number(2));

        Assert.Equal(["_A:1", "_Bee:2"], records);
    }

    [Fact]
    public void NumbersBeforeTheFirstNameMakeNoRecord()
    {
        string[] records = RecordsOf(Number(3), Name("_Late"), Number(4));

        Assert.Equal(["_Late:4"], records);
    }

    [Fact]
    public void ARunThatDoesNotStartWithALetterIsANumberNotAName()
    {
        // The 4 would start a name of four characters, but "1abc" starts with a digit, so the 4 and then the
        // word "1abc" (0x63626131) are both numbers of the record.
        string[] records = RecordsOf(Name("_Only"), Number(4), Encoding.ASCII.GetBytes("1abc"));

        Assert.Equal(["_Only:4,1667391793"], records);
    }

    [Fact]
    public void ABlobWithOnlyNumbersHasNoRecords()
    {
        string[] records = RecordsOf(Number(1), Number(2));

        Assert.Empty(records);
    }
}
