using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The dressing of a cell block: the fronts run to their end walls, and no elite stands inside a cell.
/// </summary>
public class DungeonDresserCellBlockTests
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
