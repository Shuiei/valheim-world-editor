using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Replacing a whole word in a log text: a name's letters, digits, '_' and '-' count as part of it, a '.' only when a
/// letter or digit follows it, and with a word start, a name before the word counts too.
/// </summary>
public class LogWholeWordsTests
{
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('7')]
    [InlineData('_')]
    [InlineData('-')]
    public void LettersDigitsUnderscoreAndDashArePartOfAName(char c)
    {
        Assert.True(PrivateMember.Call<bool>(typeof(Log), "NameChar", null, c));
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData('/')]
    [InlineData(',')]
    public void SpacesStopsAndSeparatorsAreNotPartOfAName(char c)
    {
        Assert.False(PrivateMember.Call<bool>(typeof(Log), "NameChar", null, c));
    }

    [Fact]
    public void AStandaloneWordIsReplaced()
    {
        Assert.Equal("X is here", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "Stone is here", "Stone", "X", false));
    }

    [Fact]
    public void EveryStandaloneOccurrenceIsReplaced()
    {
        Assert.Equal("X X", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "Stone Stone", "Stone", "X", false));
    }

    [Fact]
    public void AWordThatIsThePrefixOfALongerNameIsLeftAlone()
    {
        Assert.Equal("Stonework and X", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "Stonework and Stone", "Stone", "X", false));
    }

    [Fact]
    public void AWordFollowedByAnUnderscoreOrDashIsLeftAlone()
    {
        Assert.Equal("Stone_1 Stone-x", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "Stone_1 Stone-x", "Stone", "X", false));
    }

    [Fact]
    public void AFullStopEndingASentenceDoesNotKeepAWordFromBeingReplaced()
    {
        Assert.Equal("Use X.", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "Use Stone.", "Stone", "X", false));
    }

    [Fact]
    public void AFullStopFollowedByALetterKeepsAWordFromBeingReplaced()
    {
        // A file name: Stone.dll is not the word Stone.
        Assert.Equal("Stone.dll", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "Stone.dll", "Stone", "X", false));
    }

    [Fact]
    public void WithAWordStartTheMatchIgnoresCase()
    {
        Assert.Equal("a X", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "a stone", "Stone", "X", true));
    }

    [Fact]
    public void WithAWordStartANameBeforeTheWordKeepsItFromBeingReplaced()
    {
        Assert.Equal("a X and xstone", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "a stone and xstone", "Stone", "X", true));
    }

    [Fact]
    public void WithoutAWordStartANameBeforeTheMatchDoesNotStopIt()
    {
        Assert.Equal("xX", PrivateMember.Call<string>(typeof(Log), "ReplaceWhole", null, "xStone", "Stone", "X", false));
    }
}
