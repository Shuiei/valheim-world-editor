using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Png's tEXt keyword rule: 1 to 79 printable Latin-1 characters, with no space at either end and no two spaces in a row.
/// </summary>
public class PngKeywordTests
{
    private static bool IsKeyword(string k) => PrivateMember.Call<bool>(typeof(Png), "IsKeyword", null, k);

    [Fact]
    public void APlainWordIsAKeyword()
    {
        Assert.True(IsKeyword("Title"));
    }

    [Fact]
    public void AnEmptyKeywordIsRefused()
    {
        Assert.False(IsKeyword(""));
    }

    [Fact]
    public void A79CharacterKeywordIsAcceptedAnd80Refused()
    {
        Assert.True(IsKeyword(new string('a', 79)));
        Assert.False(IsKeyword(new string('a', 80)));
    }

    [Fact]
    public void ASpaceAtAnEndIsRefusedButASingleInsideIsFine()
    {
        Assert.False(IsKeyword(" lead"));
        Assert.False(IsKeyword("trail "));
        Assert.True(IsKeyword("a b"));
    }

    [Fact]
    public void TwoSpacesInARowAreRefused()
    {
        Assert.False(IsKeyword("a  b"));
    }

    [Fact]
    public void ControlCharactersAreRefused()
    {
        Assert.False(IsKeyword("tab\t"));
        Assert.False(IsKeyword("del\u007f"));
    }

    [Fact]
    public void LatinOneLettersAboveTheAsciiRangeAreAccepted()
    {
        // 161 (U+00A1) is the first accepted; 160 (no-break space) is not, and 256 is outside Latin-1.
        Assert.True(IsKeyword("¡"));
        Assert.False(IsKeyword(" "));
        Assert.False(IsKeyword("Ā"));
    }
}
