using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The localisation text's strip, as Python's str.strip: whitespace and the separator characters go off both ends,
/// and the text between them is kept as it is.
/// </summary>
public class LocalizationStripTests
{
    [Fact]
    public void SpacesAndTabsAtBothEndsAreRemoved()
    {
        Assert.Equal("hello", GameCatalogues.Localization.Strip(" \thello\t "));
    }

    [Fact]
    public void SpacesBetweenTheWordsAreKept()
    {
        Assert.Equal("a  b", GameCatalogues.Localization.Strip("  a  b  "));
    }

    [Fact]
    public void TheSeparatorCharactersOfPythonAreRemovedAtTheEnds()
    {
        Assert.Equal("word", GameCatalogues.Localization.Strip("\x1c\x1dword\x1e\x1f"));
    }

    [Fact]
    public void ATextOfOnlySpacesStripsToNothing()
    {
        Assert.Equal("", GameCatalogues.Localization.Strip(" \t\r\n "));
    }
}
