using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The dressing of a room, a cell block and a pit arena: the frame of a room is its own box, the cells' fronts run to their
/// end walls, the pit's corner columns stand, and no elite stands inside a cell.
/// </summary>
public class DungeonDresserFrameTests
{
    [Fact]
    public void NoEliteStandsInsideACell()
    {
        // A cell block keeps only its aisle free for what comes after its cells; the reservation had a radius of 0, so
        // its elite guards (and their spawners) were placed anywhere, locked inside cells behind the grates. Plans where
        // they used to be.
        var biome = DungeonKit.BiomeOf("Black Forest");
        var foes = biome.Foes.SelectMany(e => new[] { e.Spawner, e.Creature }).ToHashSet();
        var elites = biome.Elites.SelectMany(e => new[] { e.Spawner, e.Creature }).Where(n => !foes.Contains(n)).ToHashSet();
        int seen = 0;
        foreach (int seed in new[] { 70, 73, 78, 80 })
        {
            var style = DungeonKit.StyleOf("Goblin warren");
            var s = new DungeonGen.Settings(Biome: "Black Forest", Style: style.Name, Size: 3, Levels: 2, BossKey: false, Seed: seed);
            var rnd = new Random(seed);
            var plan = DungeonPlanner.Make(s, style, rnd);
            var o = new DungeonOut();
            DungeonDresser.Dress(plan, DungeonKit.MaterialOf(biome.Material), biome, style, s, rnd, o, "Test");
            foreach (var cells in plan.Spaces.Where(sp => sp.Room == DungeonKit.Room.Cells))
            {
                var (dx, dz) = cells.Dir;
                // Across (u, to the right of the way in) and deep (v) from the block's middle.
                (float U, float V) Local(Vector3 p) => ((p.X - cells.CenterX) * dz - (p.Z - cells.CenterZ) * dx, (p.X - cells.CenterX) * dx + (p.Z - cells.CenterZ) * dz);
                foreach (var it in o.Items.Where(i => elites.Contains(i.Prefab) && MathF.Abs(i.Position.Y - DungeonPlan.FloorY(cells.Level)) < 2))
                {
                    var (u, v) = Local(it.Position);
                    if (MathF.Abs(u) < cells.Across && MathF.Abs(v) < cells.Deep - 0.5f)
                    {
                        seen++;
                        Assert.True(MathF.Abs(u) <= 2, $"seed {seed}: {it.Prefab} {u:0.0} m across, inside a cell");
                    }
                }
                // The aisle's torches, by the cells' front walls every 8 m, still fit beside the cells kept.
                int torches = o.Items.Count(i => i.Prefab == biome.Torch && MathF.Abs(i.Position.Y - DungeonPlan.FloorY(cells.Level)) < 2
                    && Local(i.Position) is var (u, v) && MathF.Abs(MathF.Abs(u) - 1.6f) < 0.05f && MathF.Abs(v) < cells.Deep);
                Assert.True(torches >= cells.Deep / 4, $"seed {seed}: {torches} torches in a cell block {cells.Deep} m deep");
            }
        }
        Assert.True(seen > 0, "no elite in a cell block's aisle");
    }

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
    public void APitArenasCornerColumnsStand()
    {
        // The pit's ledges kept 1.3 m round each edge tile, so the columns 2 m in from them never found room: all four
        // were left out of every pit. Some pits have every corner on the floor and away from the doorways.
        var columns = DungeonKit.MaterialOf(DungeonKit.BiomeOf("Black Forest").Material).Column.Select(c => c.Prefab).ToHashSet();
        int pits = 0, whole = 0;
        foreach (string style in new[] { "Fortress", "Temple" })
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var r = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 2, Levels: 1, Seed: seed));
                var a = r.Map!.Single(m => m.Name == "Arena");
                bool In(Vector3 p) => p.X > a.X0 && p.X < a.X1 && p.Z > a.Z0 && p.Z < a.Z1;
                // The ledges round the sunken floor.
                if (r.Items.Count(i => i.Prefab == "stone_floor_2x2" && In(i.Position) && i.Position.Y > a.Y + 0.3f && i.Position.Y < a.Y + 0.9f) < 30)
                {
                    continue;
                }
                pits++;
                (float X, float Z)[] corners = [(a.X0 + 3, a.Z0 + 3), (a.X1 - 3, a.Z0 + 3), (a.X0 + 3, a.Z1 - 3), (a.X1 - 3, a.Z1 - 3)];
                if (corners.All(c => r.Items.Any(i => columns.Contains(i.Prefab) && MathF.Abs(i.Position.X - c.X) < 0.3f && MathF.Abs(i.Position.Z - c.Z) < 0.3f)))
                {
                    whole++;
                }
            }
        }
        Assert.True(pits > 0, "no pit arena");
        Assert.True(whole > 0, $"none of {pits} pit arenas has its four corner columns");
    }

    [Fact]
    public void ACellBlocksFrontsRunToItsEndWalls()
    {
        // The cells take 4 m each along the block; the strip left at each end (1.5 m in a stone block 15 m deep) had no
        // front wall, so the first and last cells were open to the aisle. The fronts' lowest row must cover each end up
        // to the first grate.
        var biome = DungeonKit.BiomeOf("Black Forest");
        var mat = DungeonKit.MaterialOf(biome.Material);
        var widths = mat.Walls.ToDictionary(w => w.Prefab, w => w.W);
        int fronts = 0;
        for (int seed = 1; seed <= 3; seed++)
        {
            var style = DungeonKit.StyleOf("Prison");
            var s = new DungeonGen.Settings(Biome: biome.Name, Style: style.Name, Size: 3, Levels: 1, Seed: seed);
            var rnd = new Random(seed);
            var plan = DungeonPlanner.Make(s, style, rnd);
            var o = new DungeonOut();
            DungeonDresser.Dress(plan, mat, biome, style, s, rnd, o, "Test");
            foreach (var cells in plan.Spaces.Where(sp => sp.Room == DungeonKit.Room.Cells && sp.Recesses.Count == 0))
            {
                var f = new DungeonDresser.Frame(cells, mat, plan.Levels[cells.Level]);
                int n = (int)MathF.Floor(2 * f.HalfD / 4);
                foreach (int side in new[] { -1, 1 })
                {
                    fronts++;
                    var spans = new List<(float From, float To)>();
                    foreach (var it in o.Items.Where(i => widths.ContainsKey(i.Prefab) && MathF.Abs(i.Position.Y - f.F - 0.5f) < 1))
                    {
                        var (u, v) = f.Local(it.Position.X, it.Position.Z);
                        if (MathF.Abs(u - side * 2) < 0.05f)
                        {
                            spans.Add((v - widths[it.Prefab] / 2, v + widths[it.Prefab] / 2));
                        }
                    }
                    bool Covered(float from, float to)
                    {
                        float at = from;
                        foreach (var (a, b) in spans.OrderBy(x => x.From))
                        {
                            if (a <= at + 0.01f && b > at)
                            {
                                at = b;
                            }
                        }
                        return at >= to - 0.01f;
                    }
                    Assert.True(Covered(-f.HalfD, -2 * n + 1) && Covered(2 * n - 1, f.HalfD), $"seed {seed}: the cell block's front at u = {side * 2} is open at an end");
                }
            }
        }
        Assert.True(fronts > 0, "no cell block");
    }
}
