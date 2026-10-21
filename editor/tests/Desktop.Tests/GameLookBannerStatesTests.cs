using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game-look card's state views, one state at a time: the reading's progress, the missing game's folder field, the
/// failed copy's Try again and log, and the ready card's heading.
/// </summary>
[Collection("DataDir")]
public class GameLookBannerStatesTests
{
    [AvaloniaFact]
    public void ReadingShowsItsProgressAndLastLine()
    {
        var b = new GameLookBanner(new AppSettings());
        b.ShowReading(new GameLook.Snapshot("running", "Reading", null, "bundle.x", 0.25));
        Assert.Equal(0.25, b.Bar.Value, 3);
        Assert.Equal("bundle.x", b.LastLine.Text);
        Assert.True(b.Bar.IsVisible && b.LastLine.IsVisible);
        Assert.Equal("Reading", b.Message.Text);
    }

    [AvaloniaFact]
    public void ReadingWithoutProgressStartsAtZero()
    {
        var b = new GameLookBanner(new AppSettings());
        b.ShowReading(new GameLook.Snapshot("running", null, null, null, null));
        Assert.Equal(0, b.Bar.Value);
    }

    [AvaloniaFact]
    public void MissingShowsTheFolderRowAndFillsTheBoxWhenTheStateChanged()
    {
        var b = new GameLookBanner(new AppSettings());
        b.ShowMissing(new GameLook.Snapshot("missing", null, "/games/valheim", null, null), true);
        Assert.True(b.PathRow.IsVisible && b.Steps.IsVisible);
        Assert.Equal("/games/valheim", b.PathBox.Text);
    }

    [AvaloniaFact]
    public void MissingKeepsTheBoxWhenTheStateDidNotChange()
    {
        var b = new GameLookBanner(new AppSettings());
        b.PathBox.Text = "typed";
        b.ShowMissing(new GameLook.Snapshot("missing", null, "/games/valheim", null, null), false);
        Assert.Equal("typed", b.PathBox.Text);
    }

    [AvaloniaFact]
    public void FailedShowsTryAgainAndTheLog()
    {
        var b = new GameLookBanner(new AppSettings());
        b.ShowFailed(new GameLook.Snapshot("failed", "It broke", null, null, null));
        Assert.True(b.Retry.IsVisible && b.ShowLog.IsVisible);
        Assert.Equal("It broke", b.Message.Text);
    }

    [AvaloniaFact]
    public void ReadyHasNoHint()
    {
        var b = new GameLookBanner(new AppSettings());
        b.ShowReady();
        Assert.Equal("", b.Hint.Text);
        Assert.False(string.IsNullOrEmpty(b.Title.Text));
    }
}
