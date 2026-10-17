using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The header lines of a PlanBuild blueprint: a header field's value, and the quotes PlanBuild puts around a description
/// (BlueprintFormats.HeaderValue and BlueprintFormats.Unquote).
/// </summary>
public class BlueprintFormatsHeaderLinesTests
{
    [Fact]
    public void AHeaderValueIsTheTextAfterItsFieldNameWithoutTheSpaces()
    {
        Assert.Equal("My house", BlueprintFormats.HeaderValue("Name:   My house  ", "name:"));
    }

    [Fact]
    public void AHeaderLineWithAnotherFieldNameGivesNoValue()
    {
        Assert.Null(BlueprintFormats.HeaderValue("Category: Misc", "name:"));
    }

    [Fact]
    public void QuotesAroundADescriptionAreRemoved()
    {
        Assert.Equal("a house", BlueprintFormats.Unquote("\"a house\""));
    }

    [Fact]
    public void AnEscapedQuoteAndANewlineInADescriptionAreRestored()
    {
        Assert.Equal("say \"hi\"\nthere", BlueprintFormats.Unquote("\"say \\\"hi\\\"\\nthere\""));
    }

    [Fact]
    public void TextWithoutBothQuotesIsKeptAsItIs()
    {
        Assert.Equal("\"open", BlueprintFormats.Unquote("\"open"));
    }

    [Fact]
    public void ASingleQuoteCharacterIsKeptAsItIs()
    {
        Assert.Equal("\"", BlueprintFormats.Unquote("\""));
    }
}
