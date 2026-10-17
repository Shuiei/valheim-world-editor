using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The localisation tables' CSV reader, which follows Python's csv rules: quoted fields, doubled quotes, line breaks
/// inside quotes, lone carriage returns and unclosed quotes.
/// </summary>
public class LocalizationCsvTests
{
    /// <summary>
    /// The rows of a text, each as its list of fields.
    /// </summary>
    private static List<List<string>> Rows(string text) => [.. GameCatalogues.Localization.Csv(text)];

    [Fact]
    public void PlainFieldsSplitAtCommasAndLineBreaks()
    {
        Assert.Equal([["a", "b"], ["c"]], Rows("a,b\nc\n"));
    }

    [Fact]
    public void QuotedFieldsKeepCommasAndDoubledQuotes()
    {
        Assert.Equal([["a", "b,c", "x\"y"]], Rows("a,\"b,c\",\"x\"\"y\"\n"));
    }

    [Fact]
    public void QuotedFieldsMayHoldLineBreaks()
    {
        Assert.Equal([["line1\nline2", "z"]], Rows("\"line1\nline2\",z\n"));
    }

    [Fact]
    public void CarriageReturnLineBreaksEndRows()
    {
        Assert.Equal([["a"], ["b"]], Rows("a\r\nb\n"));
    }

    [Fact]
    public void LoneCarriageReturnFollowedByTextStartsANewRow()
    {
        Assert.Equal([["a"], ["b"]], Rows("a\rb\n"));
    }

    [Fact]
    public void EmptyLineIsARowOfNoFields()
    {
        Assert.Equal([[]], Rows("\n"));
    }

    [Fact]
    public void TrailingCommaAddsAnEmptyField()
    {
        Assert.Equal([["a", ""]], Rows("a,\n"));
    }

    [Fact]
    public void UnclosedQuoteAtTheEndKeepsTheRowAsItIs()
    {
        Assert.Equal([["abc"]], Rows("\"abc"));
    }

    [Fact]
    public void QuotedFieldClosedAfterDoubledQuoteEndsWithTheLine()
    {
        Assert.Equal([["a\""]], Rows("\"a\"\"\"\n"));
    }
}
