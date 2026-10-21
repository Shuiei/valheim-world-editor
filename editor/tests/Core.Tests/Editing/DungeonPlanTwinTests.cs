using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The side rooms of a dungeon plan of building pieces (DungeonPlanner, DungeonPlan): a room that comes only once is not twinned.
/// </summary>
public class DungeonPlanTwinTests
{
    [Fact]
    public void ARoomThatComesOnceIsNotTwinned()
    {
        // A side room is sometimes mirrored across its parent; kinds that come only once were mirrored too. Plans that
        // used to hold two kitchens, two forges or two feast halls.
        int twins = 0;
        foreach (var (styleName, size, seed) in new[] { ("Fortress", 2, 19), ("Dvergr hold", 4, 60), ("Ruins", 3, 17) })
        {
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: styleName, Size: size, Levels: 2, Secrets: true, Seed: seed), DungeonKit.StyleOf(styleName), new Random(seed));
            var twice = plan.Spaces.Where(s => DungeonKit.SpecOf(s.Room).Unique).GroupBy(s => s.Room).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(twice.Count == 0, $"{styleName} size {size} seed {seed}: {string.Join(", ", twice)} twice");
            // Other side rooms still get their twin: the same kind off the same parent, facing the other way.
            twins += plan.Spaces.Count(a => a.IsRoom && a.Parent != null && plan.Spaces.Any(b => b.Id > a.Id && b.Room == a.Room && b.Parent == a.Parent && b.Dir == (-a.Dir.X, -a.Dir.Z)));
        }
        Assert.True(twins > 0, "no side room twinned");
    }
}
