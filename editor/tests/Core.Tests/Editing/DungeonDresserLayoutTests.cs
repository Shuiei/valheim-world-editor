using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where the dressing puts a room's stairs: the steps before a platform rise toward it.
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
}
