using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A PNG text chunk's entry: a keyword, a zero byte, then the text, both read as Latin-1; an entry without a keyword
/// is skipped and a repeated keyword keeps its last text.
/// </summary>
public class PngTextChunkTests
{
    [Fact]
    public void AKeywordAndItsTextAreAddedAsAnEntry()
    {
        var text = new Dictionary<string, string>();

        Png.AddText(text, "Title\0Hello"u8);

        Assert.Equal("Hello", text["Title"]);
    }

    [Fact]
    public void AnEntryWithAnEmptyTextKeepsAnEmptyText()
    {
        var text = new Dictionary<string, string>();

        Png.AddText(text, "Note\0"u8);

        Assert.Equal("", text["Note"]);
    }

    [Fact]
    public void AnEntryStartingWithItsZeroByteHasNoKeywordAndIsSkipped()
    {
        var text = new Dictionary<string, string>();

        Png.AddText(text, "\0Hello"u8);

        Assert.Empty(text);
    }

    [Fact]
    public void AnEntryWithoutAZeroByteIsSkipped()
    {
        var text = new Dictionary<string, string>();

        Png.AddText(text, "NoZero"u8);

        Assert.Empty(text);
    }

    [Fact]
    public void TheTextIsReadAsLatin1()
    {
        var text = new Dictionary<string, string>();

        Png.AddText(text, [(byte)'N', 0, 0xE9]);

        Assert.Equal("é", text["N"]);
    }

    [Fact]
    public void ARepeatedKeywordKeepsItsLastText()
    {
        var text = new Dictionary<string, string>();

        Png.AddText(text, "A\0one"u8);
        Png.AddText(text, "A\0two"u8);

        Assert.Equal("two", text["A"]);
    }
}
