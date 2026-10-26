using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The dressing of a pit arena: its four corner columns stand.
/// </summary>
public class DungeonDresserPitArenaTests
{
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
}
