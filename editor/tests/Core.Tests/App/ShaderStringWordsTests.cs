using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// GameShader's SPIR-V literal strings: UTF-8, nul-terminated and zero-padded to whole 32-bit words.
/// </summary>
public class ShaderStringWordsTests
{
    private static List<uint> StringWords(string s) => PrivateMember.Call<List<uint>>(typeof(GameShader), "StringWords", null, s);

    [Fact]
    public void AShortStringIsOneWordWithItsNulAndPadding()
    {
        // "ab" and its nul are three bytes, padded to four: little-endian 'a', 'b', 0, 0.
        Assert.Equal([0x00006261u], StringWords("ab"));
    }

    [Fact]
    public void AStringWhoseNulFillsAWordStillGetsItsOwnWord()
    {
        // Four bytes plus the nul are five, so the nul starts a second word.
        Assert.Equal([0x64636261u, 0x00000000u], StringWords("abcd"));
    }

    [Fact]
    public void AnEmptyStringIsOneZeroWord()
    {
        Assert.Equal([0u], StringWords(""));
    }
}
