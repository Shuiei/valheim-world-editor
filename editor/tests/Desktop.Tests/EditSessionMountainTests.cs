using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mountain tool's ground (EditSession.Mountain): the natural objects it buries, on the ground, not those high above it
/// in a dungeon.
/// </summary>
public class EditSessionMountainTests
{
    /// <summary>The tree on the ground: the first thing.</summary>
    private static readonly int[] OnTheGround = [0];

    [Fact]
    public void ObjectsToClearLeavesThingsHighAboveTheGround()
    {
        // A tree on the ground and one in a dungeon 5000 m up, at the same point.
        var s = TestScenes.Flat(1, TestScenes.BeechInFlat(1, 32, 32), TestScenes.BeechInFlat(2, 32, 32, y: 5030));
        Assert.Equal(OnTheGround, s.ObjectsToClear(32, 32, 3, (_, _) => 40f, null));
    }
}
