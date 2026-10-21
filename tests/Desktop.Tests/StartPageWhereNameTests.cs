using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The name shown for where a saved world is kept: the three known places have their own names, and any other place is
/// shown as it is; the world generation mods the world was made with follow it.
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

    [Fact]
    public void TheModsAWorldWasMadeWithFollowThePlace()
    {
        static Worlds.Info World(bool expandWorld, bool riverheim) => new("W", "/w", DateTime.Now, 2, "recent", true, null, expandWorld, riverheim);
        string recent = Lang.T("start.where.recent");
        Assert.Equal(recent, StartPage.MadeWith(World(false, false)));
        Assert.Equal(Lang.T("start.world.riverheim", recent), StartPage.MadeWith(World(false, true)));
        Assert.Equal(Lang.T("start.world.expandWorld", Lang.T("start.world.riverheim", recent)), StartPage.MadeWith(World(true, true)));
    }
}
