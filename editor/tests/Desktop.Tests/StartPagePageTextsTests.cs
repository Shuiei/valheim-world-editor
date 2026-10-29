using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The title and text each start page mode shows, and the refusal of a mode with no page (StartPage.PageTexts).
/// </summary>
public class StartPagePageTextsTests
{
    [Fact]
    public void TheGamePageShowsItsOwnTitleAndText()
    {
        Assert.Equal((Lang.T("start.page.game"), Lang.T("start.page.gameText")), StartPage.PageTexts("game"));
    }

    [Fact]
    public void TheOfflinePageTextIsTheWordsOfItsModeTip()
    {
        Assert.Equal((Lang.T("start.page.offline"), Lang.T("tip.start.offline")), StartPage.PageTexts("offline"));
    }

    [Fact]
    public void AModeWithNoPageIsAnArgumentError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StartPage.PageTexts("no such mode"));
    }
}
