using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The side rooms of a dungeon plan of building pieces (DungeonPlanner, DungeonPlan): a room that comes only once is not
/// twinned, and a twin is mirrored across its parent's own box.
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

    [Fact]
    public void ATwinIsMirroredAcrossItsParentsOwnBox()
    {
        // A side room's twin was mirrored across the parent's box, which its recesses had grown by a tile on one side:
        // the twin stood 2 m off the mirror, or not at all. Twins (one kind, size and corridor, unshifted, off
        // opposite side walls) of parents with recesses, in plans where they used to be off.
        int twins = 0;
        foreach (var (styleName, seed) in new[] { ("Crypt", 8), ("Crypt", 12), ("Catacombs", 3), ("Temple", 2), ("Fortress", 5), ("Ruins", 6), ("Ruins", 10) })
        {
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: styleName, Size: 3, Levels: 2, Seed: seed), DungeonKit.StyleOf(styleName), new Random(seed));
            foreach (var p in plan.Spaces.Where(s => s.IsRoom && s.Recesses.Count > 0))
            {
                var inRecess = p.Recesses.SelectMany(r => r.Tiles).ToHashSet();
                var own = p.Tiles.Where(t => !inRecess.Contains(t)).ToList();
                float cx = own.Min(t => t.I) + own.Max(t => t.I) + 1f, cz = own.Min(t => t.J) + own.Max(t => t.J) + 1f;
                var sides = plan.Spaces.Where(s => s.IsRoom && s.Parent == p && s.Shift == 0 && s.Dir != p.Dir && s.Dir != (-p.Dir.X, -p.Dir.Z)).ToList();
                foreach (var a in sides)
                {
                    foreach (var b in sides.Where(b => b.Id > a.Id && b.Room == a.Room && b.Dir == (-a.Dir.X, -a.Dir.Z) && b.Length == a.Length && b.Across == a.Across && b.Deep == a.Deep))
                    {
                        twins++;
                        string where = $"{styleName} seed {seed}: the {a.Name} at ({a.CenterX}, {a.CenterZ}) and ({b.CenterX}, {b.CenterZ}) off the {p.Name} at ({cx}, {cz})";
                        Assert.True(a.Dir.X != 0
                            ? a.CenterX + b.CenterX == 2 * cx && a.CenterZ == b.CenterZ
                            : a.CenterZ + b.CenterZ == 2 * cz && a.CenterX == b.CenterX, where);
                    }
                }
            }
        }
        Assert.True(twins > 0, "no twin off a room with recesses");
    }
}
