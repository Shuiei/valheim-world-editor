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

    [Fact]
    public void StepsBeforeAPlatformRiseTowardIt()
    {
        // A stone stair climbs toward its back. The steps before the plinth of a burial chamber, chapel, shrine and tomb
        // were turned the other way from those of a great hall, arena or feast hall, so they rose away from the plinth.
        // Every stair with a plinth slab (half a metre above its foot) 2 m before or behind it must have it behind.
        int checkedStairs = 0;
        var rooms = new HashSet<string>();
        foreach (string style in new[] { "Crypt", "Temple" })
        {
            foreach (string biome in new[] { "Black Forest", "Deep North" })
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    var r = DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: style, Size: 3, Levels: 2, BossKey: false, Seed: seed));
                    var slabs = r.Items.Where(i => i.Prefab == "stone_floor_2x2").Select(i => i.Position).ToList();
                    foreach (var stair in r.Items.Where(i => i.Prefab == "stone_stair"))
                    {
                        var ahead = Vector3.Transform(Vector3.UnitZ, stair.Rotation);
                        bool SlabAt(Vector3 p) => slabs.Any(s => MathF.Abs(s.Y - stair.Position.Y - 0.5f) < 0.1f && Vector2.Distance(new(s.X, s.Z), new(p.X, p.Z)) < 1.2f);
                        bool behind = SlabAt(stair.Position - 2 * ahead), before = SlabAt(stair.Position + 2 * ahead);
                        if (!behind && !before)
                        {
                            continue;
                        }
                        checkedStairs++;
                        string room = r.Map!.FirstOrDefault(m => !m.Corridor && MathF.Abs(m.Y - stair.Position.Y) < 1
                            && stair.Position.X >= m.X0 && stair.Position.X <= m.X1 && stair.Position.Z >= m.Z0 && stair.Position.Z <= m.Z1)?.Name ?? "?";
                        rooms.Add(room);
                        Assert.True(behind && !before, $"{style}, {biome}, seed {seed}: the stair at {stair.Position} in the {room} rises away from its plinth");
                    }
                }
            }
        }
        Assert.True(checkedStairs > 0, "no steps before a platform");
        Assert.Superset(new HashSet<string> { "Burial chamber", "Chapel", "Shrine", "Tomb" }, rooms);
    }

    [Fact]
    public void TheAntechambersWarningHangsOnTheWallsInnerFace()
    {
        // The warning beside the boss's locked gate was put 4 cm from the doorway's middle, which is on the wall's centre
        // line: 0.54 m deep in a stone wall. It belongs on the antechamber's side, on the wall's inner face.
        int signs = 0;
        foreach (string biome in new[] { "Black Forest", "Mistlands" })
        {
            float thick = DungeonKit.MaterialOf(DungeonKit.BiomeOf(biome).Material).Thick;
            for (int seed = 1; seed <= 3; seed++)
            {
                var r = DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: "Crypt", Size: 2, Levels: 1, Seed: seed));
                foreach (var sign in r.Items.Where(i => i.Prefab == "sign" && i.Data != null && i.Data.Any(d => d.Value.StartsWith("Sealed", StringComparison.Ordinal))))
                {
                    signs++;
                    var ante = r.Map!.Single(m => m.Name == "Antechamber");
                    var gate = r.Doors!.Where(d => d.Kind == "Key").MinBy(d => Vector2.Distance(new(d.X, d.Z), new(sign.Position.X, sign.Position.Z)))!;
                    float dx = MathF.Abs(sign.Position.X - gate.X), dz = MathF.Abs(sign.Position.Z - gate.Z);
                    Assert.Equal(2.6f, MathF.Max(dx, dz), 0.01f);
                    Assert.Equal(thick / 2 + 0.04f, MathF.Min(dx, dz), 0.01f);
                    Assert.True(sign.Position.X > ante.X0 && sign.Position.X < ante.X1 && sign.Position.Z > ante.Z0 && sign.Position.Z < ante.Z1,
                        $"{biome}, seed {seed}: the warning at {sign.Position} is not on the antechamber's side");
                }
            }
        }
        Assert.True(signs > 0, "no warning by a locked gate");
    }

    [Fact]
    public void ARoomsFrameIsItsOwnBoxItsRecessesExtraFloor()
    {
        // A recess grew the room's box by a tile on one side, and the dressing took that box for the room: its middle
        // 1 m toward the recess and its half width 1 m more, so every symmetric placement (a hall's colonnades, its
        // braziers) was off on one side. The frame is the room's own box; the recesses are floor beyond it.
        var mat = DungeonKit.MaterialOf(DungeonKit.BiomeOf("Black Forest").Material);
        int rooms = 0;
        for (int seed = 1; seed <= 3; seed++)
        {
            var style = DungeonKit.StyleOf("Crypt");
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style.Name, Size: 3, Levels: 2, Seed: seed), style, new Random(seed));
            foreach (var s in plan.Spaces.Where(s => s.IsRoom && s.Recesses.Count > 0))
            {
                rooms++;
                var inRecess = s.Recesses.SelectMany(r => r.Tiles).ToHashSet();
                var own = s.Tiles.Where(t => !inRecess.Contains(t)).ToList();
                int i0 = own.Min(t => t.I), i1 = own.Max(t => t.I), j0 = own.Min(t => t.J), j1 = own.Max(t => t.J);
                int across = s.Dir.X != 0 ? j1 - j0 + 1 : i1 - i0 + 1, deep = s.Dir.X != 0 ? i1 - i0 + 1 : j1 - j0 + 1;
                var f = new DungeonDresser.Frame(s, mat, plan.Levels[s.Level]);
                string where = $"seed {seed}: the {s.Name}";
                Assert.Equal(i0 + i1 + 1f, f.Cx);
                Assert.Equal(j0 + j1 + 1f, f.Cz);
                Assert.Equal(across - mat.Thick / 2, f.HalfA, 0.001f);
                Assert.Equal(deep - mat.Thick / 2, f.HalfD, 0.001f);
                // Each recess tile is floor of the frame.
                foreach (var (i, j) in inRecess)
                {
                    var (u, v) = f.Local(2 * i + 1, 2 * j + 1);
                    Assert.True(f.Inside(u, v), $"{where}: its recess at ({i}, {j}) is not floor");
                }
            }
        }
        Assert.True(rooms > 0, "no room with recesses");
    }
}
