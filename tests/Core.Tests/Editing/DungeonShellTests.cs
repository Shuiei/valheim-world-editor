using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// DungeonShell's pillars and posts: segments from the foot up to the top, the last rising into the roof slab, never
/// past it into the room above.
/// </summary>
public class DungeonShellTests
{
    [Theory]
    // Wood logs (4.44 m) in a tall room (10 m): a third would reach 13.32 m, 1.32 m into the room two levels up.
    [InlineData(4.44f, 10f, 2)]
    // In a room 4 m high, one rises 0.44 m into the slab.
    [InlineData(4.44f, 4f, 1)]
    // Stone pillars (2 m) reach the top exactly.
    [InlineData(2f, 10f, 5)]
    public void APillarRisesIntoTheRoofSlabNeverPastIt(float segment, float top, int count)
    {
        var o = new DungeonOut();

        DungeonShell.Stack(new DungeonKit.Column("pillar", segment, segment / 2), o, 0, 0, 0, top);

        Assert.Equal(count, o.Items.Count);
        Assert.All(o.Items, i => Assert.True(i.Position.Y + segment / 2 <= top + DungeonShell.RoofSlab + 0.01f));
    }
}
