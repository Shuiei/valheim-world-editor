using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The lists' order as the cache file writes it: each list's location and vegetation counts as "locations/vegetation",
/// comma-separated.
/// </summary>
public class GameLocationsOrderTextTests
{
    [Fact]
    public void EachListsCountsAreWrittenAsLocationsSlashVegetation()
    {
        (int, int)[] order = [(3, 0), (2, 1)];

        Assert.Equal("3/0,2/1", PrivateMember.Call<string>(typeof(GameLocations), "OrderText", null, order));
    }

    [Fact]
    public void AnEmptyOrderIsAnEmptyText()
    {
        (int, int)[] order = [];

        Assert.Equal("", PrivateMember.Call<string>(typeof(GameLocations), "OrderText", null, order));
    }
}
