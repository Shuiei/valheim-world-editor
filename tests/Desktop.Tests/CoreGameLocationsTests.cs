using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// The order of the game's location lists, as its own log (Player.log) shows them added at each start
// (GameLocations.OrderFromLog): the last run's, lists that add no location left out.
public class CoreGameLocationsTests
{
    [Fact]
    public void TheListsOrderComesFromTheLastRunInTheGamesLog()
    {
        static string Added(int locations, int vegetation) =>
            $"10/10/2026 06:59:10: Added {locations} locations, {vegetation} vegetations, 0 environments, 0 biome env-setups, 0 clutter  from main";
        var log = new List<string>
        {
            "10/10/2026 05:11:04: Zonesystem Start 3488",
            Added(3, 0), Added(27, 25), Added(25, 33),
            "[Message:HD Valheim Textures] something else",
            "10/10/2026 06:59:10: Zonesystem Start 3488",
            Added(3, 0), Added(2, 0), Added(0, 12), Added(27, 25), Added(4, 0), Added(25, 33), Added(25, 35),
            "10/10/2026 06:59:11: Added 9 locations, 1 vegetations from start",
        };
        Assert.Equal(new[] { (3, 0), (2, 0), (27, 25), (4, 0), (25, 33), (25, 35) }, GameLocations.OrderFromLog(log));
        // Two runs one after the other (no line between): the second.
        Assert.Equal(new[] { (3, 0), (5, 1), (6, 2) }, GameLocations.OrderFromLog([Added(3, 0), Added(4, 0), Added(3, 0), Added(5, 1), Added(6, 2)]));
        // No such lines: none.
        Assert.Null(GameLocations.OrderFromLog(["Zonesystem Start 1", "nothing"]));
    }
}
