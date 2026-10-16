using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The foes a generated dungeon's settings name: one of a biome's (by its spawner or its creature), else taken as it
/// is, a Spawner_ name standing for the creature after the prefix; what a room's dressing keeps free.
/// </summary>
public class DungeonDresserTests
{
    [Fact]
    public void AFoeIsFoundByItsSpawnerOrItsCreature()
    {
        Assert.Equal(new DungeonKit.Foe("Spawner_Draugr_Ranged", "Draugr_Ranged"), DungeonDresser.FoeOf("Spawner_Draugr_Ranged"));
        Assert.Equal(new DungeonKit.Foe("Spawner_Draugr_Ranged", "Draugr_Ranged"), DungeonDresser.FoeOf("Draugr_Ranged"));
        // A boss and an elite are foes too.
        Assert.Equal(new DungeonKit.Foe("Spawner_Troll", "Troll"), DungeonDresser.FoeOf("Troll"));
        Assert.Equal(new DungeonKit.Foe("Spawner_Greydwarf_Elite", "Greydwarf_Elite"), DungeonDresser.FoeOf("Greydwarf_Elite"));
    }

    [Fact]
    public void AFoeOfNoBiomeIsTakenAsItIs()
    {
        Assert.Equal(new DungeonKit.Foe("Spawner_Wolf", "Wolf"), DungeonDresser.FoeOf("Spawner_Wolf"));
        Assert.Equal(new DungeonKit.Foe("Wolf", "Wolf"), DungeonDresser.FoeOf("Wolf"));
    }

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
}
