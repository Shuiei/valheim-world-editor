using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The side rooms of a dungeon plan of building pieces (DungeonPlanner, DungeonPlan): a twin is mirrored across its parent's own box.
/// </summary>
public class DungeonPlanTwinMirrorTests
{
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
