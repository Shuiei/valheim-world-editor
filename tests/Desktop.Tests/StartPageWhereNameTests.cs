using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The name shown for where a saved world is kept: the three known places have their own names, and any other place is
/// shown as it is.
/// </summary>
public class StartPageWhereNameTests
{
    [Fact]
    public void TheKnownPlacesHaveTheirOwnNames()
    {
        Assert.Equal(Lang.T("start.where.recent"), StartPage.WhereName("recent"));
        Assert.Equal(Lang.T("start.where.yours"), StartPage.WhereName("yours"));
        Assert.Equal(Lang.T("start.where.local"), StartPage.WhereName("local"));
    }

    [Fact]
    public void AnyOtherPlaceIsShownAsItIs()
    {
        Assert.Equal("elsewhere", StartPage.WhereName("elsewhere"));
    }
}
