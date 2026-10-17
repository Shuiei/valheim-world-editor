using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The command line's options as the app reads them: a world folder, an option and its missing value, the game data folder
/// that cannot be read, an area that opens straight in the editor, and the window's size.
/// </summary>
[Collection("DataDir")]
public sealed class StartupOptionsTests
{
    [Fact]
    public void AWorldFolderAloneOpensItsMap()
    {
        Options.Parse(["/worlds/Midgard"]);
        Assert.Equal("/worlds/Midgard", Options.Folder);
        Assert.False(Options.Direct);
        Assert.Null(Options.LiveUrl);
        // The first one counts; options and their values are not folders.
        Options.Parse(["--size", "3", "/a", "/b"]);
        Assert.Equal("/a", Options.Folder);
        Assert.Equal(3, Options.Size);
    }

    [Fact]
    public void AnOptionGivenLastWithoutItsValueIsAnError()
    {
        foreach (string option in new[] { "--world", "--data", "--gamedata", "--zone", "--size", "--window", "--live", "--token" })
        {
            var e = Assert.Throws<ArgumentException>(() => Options.Parse(["/worlds/Midgard", option]));
            Assert.Equal($"{option} needs a value after it.", e.Message);
        }
    }

    [Fact]
    public void AGameDataFolderThatCannotBeReadIsAnError()
    {
        string missing = Path.Combine(Path.GetTempPath(), "vwe-no-gamedata-" + Guid.NewGuid().ToString("N")[..8]);
        var e = Assert.Throws<ArgumentException>(() => Options.Parse(["--gamedata", missing]));
        Assert.StartsWith($"--gamedata {missing} cannot be read: ", e.Message);
        Assert.IsAssignableFrom<IOException>(e.InnerException);
    }

    [Fact]
    public void AnAreaOpensStraightInTheEditor()
    {
        Options.Parse(["--world", "Midgard", "--zone", "-3,7", "--size", "12"]);
        Assert.True(Options.Direct);
        Assert.Equal("Midgard", Options.World);
        Assert.Equal((-3, 7), (Options.ZoneX, Options.ZoneZ));
        // At most 9 zones across, at least 1.
        Assert.Equal(9, Options.Size);
        Options.Parse(["--size", "0"]);
        Assert.Equal(1, Options.Size);
        Assert.Equal((0, 0), (Options.WindowWidth, Options.WindowHeight));
    }

    [Fact]
    public void TheWindowsSizeCanBeGivenWithinLimits()
    {
        Options.Parse(["--window", "1440x900"]);
        Assert.Equal((1440, 900), (Options.WindowWidth, Options.WindowHeight));
        Options.Parse(["--window", "10x99999"]);
        Assert.Equal((640, 4320), (Options.WindowWidth, Options.WindowHeight));
    }
}
