using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The text typed into the start page's connect fields: blank text is no text (null), and other text is given trimmed.
/// </summary>
public sealed class StartPageConnectTests
{
    /// <summary>Calls the connect field's trimming check.</summary>
    private static string? NullIfEmpty(string? text) =>
        (string?)PrivateMethods.Call(typeof(StartPage), "NullIfEmpty", null, text);

    /// <summary>Missing text is no text.</summary>
    [Fact]
    public void MissingTextIsNoText() =>
        Assert.Null(NullIfEmpty(null));

    /// <summary>Text of spaces only is no text.</summary>
    [Fact]
    public void SpacesOnlyIsNoText() =>
        Assert.Null(NullIfEmpty("   "));

    /// <summary>Text with spaces round it is given without them.</summary>
    [Fact]
    public void TextIsGivenTrimmed() =>
        Assert.Equal("192.168.1.5", NullIfEmpty("  192.168.1.5 "));

}
