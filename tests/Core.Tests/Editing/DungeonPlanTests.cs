using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The plan of a dungeon of building pieces (DungeonPlanner, DungeonPlan): its levels' rooms and corridors.
/// </summary>
public class DungeonPlanTests
{
    [Fact]
    public void NothingStandsOverATallRoomNotEvenAHiddenCache()
    {
        // A tall room rises through the level above it, which is laid out before it; the hidden caches are attached last,
        // to any level. Plans where a cache (once with its corridor) used to land over a great hall or chapel of the
        // level below, its floor cutting the room's upper half.
        int tallUnder = 0;
        foreach (var (styleName, seed) in new[] { ("Crypt", 8), ("Crypt", 22), ("Temple", 27), ("Temple", 37), ("Dvergr hold", 15) })
        {
            var style = DungeonKit.StyleOf(styleName);
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: styleName, Size: 3, Levels: 3, Secrets: true, Seed: seed), style, new Random(seed));
            Assert.Contains(plan.Spaces, s => s.Room == DungeonKit.Room.Secret);
            for (int k = 1; k < plan.Levels.Count; k++)
            {
                var above = plan.Levels[k - 1];
                foreach (var tall in plan.Levels[k].Spaces.Where(s => s.Tall))
                {
                    tallUnder++;
                    var cells = tall.Tiles.Select(DungeonPlan.CellOf).ToHashSet();
                    var over = above.Spaces.Where(s => s.Tiles.Any(t => cells.Contains(DungeonPlan.CellOf(t)))).Select(s => s.Name).ToList();
                    Assert.True(over.Count == 0, $"{styleName} seed {seed}: {string.Join(", ", over)} over the {tall.Name} of level {k}");
                }
            }
        }
        Assert.True(tallUnder > 0, "no tall room under another level");
    }
}
