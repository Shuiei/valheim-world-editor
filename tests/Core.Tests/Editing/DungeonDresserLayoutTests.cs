using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where the dressing puts a room's furniture and features: the steps before a platform, the antechamber's warning, the
/// shrine's stone on its plinth, and the war table kept off the dais' steps.
/// </summary>
public class DungeonDresserLayoutTests
{
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
