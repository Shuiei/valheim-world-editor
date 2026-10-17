using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading a game data cache file: it is used only when its first line is the stamp it was written with, and the text
/// after that line is what comes back. A missing file, a stamp that differs, or a file with no line after its stamp
/// gives nothing.
/// </summary>
public sealed class GameDataCacheFileTests : AppSettingsTestBase
{
    /// <summary>The cache read by the code under test, for a file and a stamp.</summary>
    private static string? Cached(string file, string stamp) => (string?)PrivateCall.Static(typeof(GameData), "Cached", file, stamp);

    [Fact]
    public void AMissingCacheFileGivesNothing()
    {
        Assert.Null(Cached(Path.Combine(Data, "game-zdo.json"), "stamp"));
    }

    [Fact]
    public void ACacheWithTheSameStampGivesTheTextAfterTheStampLine()
    {
        string file = Path.Combine(Data, "game-zdo.json");
        File.WriteAllText(file, "stamp-1\nfirst line\nsecond line");
        Assert.Equal("first line\nsecond line", Cached(file, "stamp-1"));
    }

    [Fact]
    public void ACacheWithAnotherStampIsNotUsed()
    {
        string file = Path.Combine(Data, "game-zdo.json");
        File.WriteAllText(file, "stamp-1\nbody");
        Assert.Null(Cached(file, "stamp-2"));
    }

    [Fact]
    public void ACacheHoldingOnlyItsStampGivesNothing()
    {
        string file = Path.Combine(Data, "game-zdo.json");
        File.WriteAllText(file, "stamp-1");
        Assert.Null(Cached(file, "stamp-1"));
    }

    [Fact]
    public void ACacheWithAnEmptyBodyGivesAnEmptyText()
    {
        string file = Path.Combine(Data, "game-zdo.json");
        File.WriteAllText(file, "stamp-1\n");
        Assert.Equal("", Cached(file, "stamp-1"));
    }
}
