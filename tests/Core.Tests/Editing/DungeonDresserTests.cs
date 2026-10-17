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
    public void AShrinesStoneStandsOnItsPlinthWhicheverIsPicked()
    {
        // The memorial stone was lifted by the medium one's foot whichever was picked: the large one sank 0.13 m.
        var bases = new List<(string Prefab, float Base)>();
        for (int seed = 1; seed <= 8; seed++)
        {
            var r = DungeonGen.Make(new DungeonGen.Settings(Biome: "Mountain", Style: "Temple", Size: 3, Levels: 1, Seed: seed));
            foreach (var i in r.Items.Where(i => i.Prefab.StartsWith("MemorialStone_", StringComparison.Ordinal)))
            {
                var shrine = r.Map!.Where(m => m.Name == "Shrine").SingleOrDefault(m => i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1);
                if (shrine != null)
                {
                    bases.Add((i.Prefab, i.Position.Y - DungeonProps.Foot(i.Prefab) - shrine.Y));
                }
            }
        }
        Assert.Contains(bases, b => b.Prefab == "MemorialStone_Large");
        Assert.All(bases, b => Assert.Equal(bases[0].Base, b.Base, 0.001f));
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

    [Fact]
    public void AWarTableKeepsOffTheDaisSteps()
    {
        // The war room's long table went down the middle with no check: in a room entered on its long side it lay across
        // the dais' steps. It now stands only where it has the room.
        int tables = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var r = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: seed));
            var thrones = r.Map!.Where(m => m.Name == "Throne room").ToList();
            foreach (var t in r.Items.Where(i => i.Prefab == "piece_table_oak"
                && thrones.Any(m => i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1)))
            {
                var axis = Vector3.Transform(Vector3.UnitX, t.Rotation);
                foreach (var step in r.Items.Where(i => i.Prefab == "stone_stair" && MathF.Abs(i.Position.Y - t.Position.Y) < 1.5f))
                {
                    var d = step.Position - t.Position;
                    d.Y = 0;
                    float along = Vector3.Dot(d, axis);
                    Assert.False(MathF.Abs(along) < 3.5f && (d - along * axis).Length() < 1.2f, $"seed {seed}: a table on the steps at {step.Position}");
                }
                tables++;
            }
        }
        Assert.True(tables > 0, "no war table");
    }
}
