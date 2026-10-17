using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The frame of a room is its own box, not its recesses' floor added to it, and a plain corridor or a kind without dressing gets nothing.
/// </summary>
public class DungeonDresserFrameTests
{
    [Fact]
    public void ARoomsFrameIsItsOwnBoxItsRecessesExtraFloor()
    {
        // A recess grew the room's box by a tile on one side, and the dressing took that box for the room: its middle
        // 1 m toward the recess and its half width 1 m more, so every symmetric placement (a hall's colonnades, its
        // braziers) was off on one side. The frame is the room's own box; the recesses are floor beyond it.
        var mat = DungeonKit.MaterialOf(DungeonKit.BiomeOf("Black Forest").Material);
        int rooms = 0, corridors = 0;
        var ways = new HashSet<(int, int)>();
        for (int seed = 1; seed <= 3; seed++)
        {
            var style = DungeonKit.StyleOf("Crypt");
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style.Name, Size: 3, Levels: 2, Seed: seed), style, new Random(seed));
            foreach (var s in plan.Spaces)
            {
                var f = new DungeonDresser.Frame(s, mat, plan.Levels[s.Level]);
                string where = $"seed {seed}: the {s.Name}";
                if (s.Recesses.Count == 0)
                {
                    // Without recesses the own box is the box.
                    Assert.Equal((s.CenterX, s.CenterZ, s.Across, s.Deep), (f.Cx, f.Cz, f.Across, f.Deep));
                    continue;
                }
                rooms += s.IsRoom ? 1 : 0;
                corridors += s.Corridor ? 1 : 0;
                ways.Add(s.Dir);
                var inRecess = s.Recesses.SelectMany(r => r.Tiles).ToHashSet();
                var own = s.Tiles.Where(t => !inRecess.Contains(t)).ToList();
                int i0 = own.Min(t => t.I), i1 = own.Max(t => t.I), j0 = own.Min(t => t.J), j1 = own.Max(t => t.J);
                int across = s.Dir.X != 0 ? j1 - j0 + 1 : i1 - i0 + 1, deep = s.Dir.X != 0 ? i1 - i0 + 1 : j1 - j0 + 1;
                Assert.Equal(i0 + i1 + 1f, f.Cx);
                Assert.Equal(j0 + j1 + 1f, f.Cz);
                Assert.Equal(across - mat.Thick / 2, f.HalfA, 0.001f);
                Assert.Equal(deep - mat.Thick / 2, f.HalfD, 0.001f);
                // Each recess tile is floor of the frame.
                foreach (var (i, j) in inRecess)
                {
                    var (u, v) = f.Local(2 * i + 1, 2 * j + 1);
                    Assert.True(f.Inside(u, v), $"{where}: its recess at ({i}, {j}) is not floor");
                    // And within the reach the walls, corners and nooks are walked over.
                    int a = (int)MathF.Floor((u + f.Across) / 2), b = (int)MathF.Floor((v + f.Deep) / 2);
                    Assert.True(a >= f.A0 && a <= f.A1 && b >= f.B0 && b <= f.B1, $"{where}: its recess at ({i}, {j}) is beyond its reach");
                }
            }
        }
        Assert.True(rooms > 0, "no room with recesses");
        // Niches off corridors too, and spaces facing all four ways (each turns the frame's tiles differently).
        Assert.True(corridors > 0, "no corridor with recesses");
        Assert.Equal(4, ways.Count);
    }

    [Fact]
    public void APlainCorridorOrAKindWithoutDressingGetsNothing()
    {
        // The kinds a corridor is dressed as: a plain one, or one the dresser has no dressing for, adds nothing (the
        // switch's default); a gate adds its wall and door.
        var biome = DungeonKit.BiomeOf("Black Forest");
        var mat = DungeonKit.MaterialOf(biome.Material);
        var style = DungeonKit.StyleOf("Crypt");
        var s = new DungeonGen.Settings(Biome: "Black Forest", Style: style.Name, Size: 3, Levels: 1, Seed: 1);
        var plan = DungeonPlanner.Make(s, style, new Random(1));
        var f = plan.Spaces.Where(sp => sp.Corridor && sp.Shape == DungeonPlan.Shape.Rect)
            .Select(sp => new DungeonDresser.Frame(sp, mat, plan.Levels[sp.Level])).First(fr => fr.HalfD >= 4);
        int Dressed(string kind)
        {
            var o = new DungeonOut();
            DungeonDresser.DressCorridor(f, kind, mat, biome, style, s, new Random(1), o);
            return o.Items.Count;
        }

        Assert.Equal(0, Dressed("plain"));
        Assert.Equal(0, Dressed("no such kind"));
        Assert.True(Dressed("gate") > 0);
    }
}
