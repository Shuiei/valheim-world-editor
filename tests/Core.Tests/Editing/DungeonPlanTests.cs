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

    [Fact]
    public void AnAntechamberIsKeptOnlyWithTheArenaBeyondIt()
    {
        // The antechamber was placed before the arena was tried, and stayed when the arena did not fit; the next try
        // added another. Plans that used to hold two or three antechambers, or one with no arena at all: in the first
        // three a later try finds room for both, in the last none does.
        foreach (var (styleName, size, seed, arena) in new[] { ("Goblin warren", 1, 20, true), ("Goblin warren", 1, 57, true), ("Ruins", 3, 20, true), ("Ruins", 4, 60, false) })
        {
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: styleName, Size: size, Levels: 2, Secrets: true, Seed: seed), DungeonKit.StyleOf(styleName), new Random(seed));
            var antes = plan.Spaces.Where(s => s.Room == DungeonKit.Room.Antechamber).ToList();
            string where = $"{styleName} size {size} seed {seed}";
            Assert.True(antes.Count <= 1, $"{where}: {antes.Count} antechambers");
            Assert.True(arena == (plan.Boss != null), $"{where}: arena {plan.Boss != null}");
            Assert.True(antes.Count == (plan.Boss == null ? 0 : 1), $"{where}: {antes.Count} antechambers, arena {plan.Boss != null}");
            Assert.True(plan.Boss == null || plan.Boss.Parent == antes[0], $"{where}: the arena is not off the antechamber");
            // What was taken back left nothing behind: ids in order, doorways and tiles only of spaces still there.
            Assert.Equal(Enumerable.Range(0, plan.Spaces.Count), plan.Spaces.Select(s => s.Id));
            var kept = plan.Spaces.ToHashSet();
            Assert.All(plan.Doors, d => Assert.True(kept.Contains(d.A) && kept.Contains(d.B), $"{where}: a doorway to a dropped space"));
            Assert.All(plan.Spaces, s => Assert.All(s.Doors, d => Assert.Contains(d, plan.Doors)));
            Assert.All(plan.Levels, l => Assert.All(l.Owner.Values, o => Assert.Contains(o, l.Spaces)));
            // And no tall cell of a dropped tall room.
            Assert.All(plan.Levels, l =>
            {
                HashSet<(int, int)> tall = [.. l.Spaces.Where(s => s.Tall).SelectMany(s => s.Tiles).Select(DungeonPlan.CellOf)];
                Assert.Equal(tall, l.TallCells);
            });
        }
    }

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
