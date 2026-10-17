using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The JSON syntax CatalogueJson writes (its containers and strings): empty and nested lists and dictionaries, the item and
/// key separators, the indent, the key order, and string escapes with or without ASCII-only output.
/// </summary>
public class CatalogueJsonSyntaxTests
{
    /// <summary>The compact style with a comma between items and a colon after keys.</summary>
    private static readonly CatalogueJson.Style Compact = new(",", ":");

    [Fact]
    public void AnEmptyListIsTwoBrackets() => Assert.Equal("[]", CatalogueJson.Write(new List<object?>(), Compact));

    [Fact]
    public void AnEmptyDictionaryIsTwoBraces() => Assert.Equal("{}", CatalogueJson.Write(new CatalogueJson.Dict(), Compact));

    [Fact]
    public void ItemsAreSeparatedByTheItemSeparator() =>
        Assert.Equal("[1,2,3]", CatalogueJson.Write(new List<object?> { 1L, 2L, 3L }, Compact));

    [Fact]
    public void AnIndentedListHasEachItemOnItsOwnLine() =>
        Assert.Equal("[\n  1,\n  2\n]", CatalogueJson.Write(new List<object?> { 1L, 2L }, new CatalogueJson.Style(",", ":", Indent: 2)));

    [Fact]
    public void ANestedListIndentsByItsLevel()
    {
        var inner = new List<object?> { 1L };
        var outer = new List<object?> { inner };
        Assert.Equal("[\n  [\n    1\n  ]\n]", CatalogueJson.Write(outer, new CatalogueJson.Style(",", ":", Indent: 2)));
    }

    [Fact]
    public void ADictionaryKeepsTheOrderItsKeysWereSet()
    {
        var dict = new CatalogueJson.Dict { ["b"] = 2L, ["a"] = 1L };
        Assert.Equal("{\"b\":2,\"a\":1}", CatalogueJson.Write(dict, Compact));
    }

    [Fact]
    public void ADictionaryIsSortedWhenAsked()
    {
        var dict = new CatalogueJson.Dict { ["b"] = 2L, ["a"] = 1L };
        Assert.Equal("{\"a\":1,\"b\":2}", CatalogueJson.Write(dict, new CatalogueJson.Style(",", ":", SortKeys: true)));
    }

    [Fact]
    public void QuotesAndBackslashesAndLineBreaksAreEscaped() =>
        Assert.Equal("\"a\\\"b\\\\c\\n\\t\"", CatalogueJson.Write("a\"b\\c\n\t", Compact));

    [Fact]
    public void ControlCharactersAreWrittenAsUnicodeEscapes() =>
        Assert.Equal("\"\\u0001\"", CatalogueJson.Write("\u0001", Compact));

    [Fact]
    public void NonAsciiIsEscapedUnlessAsciiIsOff()
    {
        Assert.Equal("\"\\u00e9\"", CatalogueJson.Write("\u00e9", Compact));
        Assert.Equal("\"\u00e9\"", CatalogueJson.Write("\u00e9", new CatalogueJson.Style(",", ":", Ascii: false)));
    }
}
