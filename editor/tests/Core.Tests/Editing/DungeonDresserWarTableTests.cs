using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The war table is kept off the dais' steps.
/// </summary>
public class DungeonDresserWarTableTests
{
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
