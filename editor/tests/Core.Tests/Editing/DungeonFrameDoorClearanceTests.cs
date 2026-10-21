using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The dressing of a room keeps clear of its doorways: a point at a doorway is near the door, a point well away is not,
/// and the wall spots it offers are all clear of the doors.
/// </summary>
public class DungeonFrameDoorClearanceTests
{
    /// <summary>A room's dressing frame that has at least one doorway, from a few seeded plans.</summary>
    private static DungeonDresser.Frame FrameWithADoor()
    {
        var mat = DungeonKit.MaterialOf(DungeonKit.BiomeOf("Black Forest").Material);
        var style = DungeonKit.StyleOf("Crypt");
        for (int seed = 1; seed <= 5; seed++)
        {
            var plan = DungeonPlanner.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style.Name, Size: 3, Levels: 2, Seed: seed), style, new Random(seed));
            foreach (var s in plan.Spaces)
            {
                var f = new DungeonDresser.Frame(s, mat, plan.Levels[s.Level]);
                if (f.Mouths.Count > 0)
                {
                    return f;
                }
            }
        }
        throw new InvalidOperationException("No seeded plan has a room with a doorway.");
    }

    [Fact]
    public void APointAtADoorwayIsNearTheDoor()
    {
        var f = FrameWithADoor();
        var (u, v) = f.Local(f.Mouths[0].X, f.Mouths[0].Z);
        Assert.True(f.NearDoor(u, v, 0.5f));
    }

    [Fact]
    public void APointFiftyMetresFromADoorwayIsNotNearIt()
    {
        var f = FrameWithADoor();
        var (u, v) = f.Local(f.Mouths[0].X + 50f, f.Mouths[0].Z);
        Assert.False(f.NearDoor(u, v, 3f));
    }

    [Fact]
    public void EveryWallSpotIsClearOfTheDoors()
    {
        var f = FrameWithADoor();
        var spots = f.WallSpots(2.2f, 3f);
        Assert.NotEmpty(spots);
        foreach (var (u, v, _) in spots)
        {
            Assert.False(f.NearDoor(u, v, 3f));
        }
    }
}
