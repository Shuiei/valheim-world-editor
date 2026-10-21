using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Which replayed dungeon parts are shown: a part is hidden when a node up its parents is hidden, a walk around a loop of
/// parents ends, and a node outside the list is ignored.
/// </summary>
public class DungeonReplayVisibilityTests
{
    [Fact]
    public void APartUnderAHiddenNodeIsNotShown()
    {
        int[] nodes = [-1, 0];

        Assert.False(Dungeons.IsShown(1, nodes, [true, false]));
    }

    [Fact]
    public void APartUnderShownNodesIsShown()
    {
        int[] nodes = [-1, 0];

        Assert.True(Dungeons.IsShown(1, nodes, [false, false]));
    }

    [Fact]
    public void ALoopOfParentsEndsTheWalkAndTheWalkShows()
    {
        int[] nodes = [1, 0];

        Assert.True(Dungeons.IsShown(0, nodes, [false, false]));
    }

    [Fact]
    public void ANodeHiddenSetsItsFlag()
    {
        bool[] off = new bool[2];

        Dungeons.SetHidden(off, 1, true);

        Assert.Equal([false, true], off);
    }

    [Fact]
    public void ANodeOutsideTheListIsIgnored()
    {
        bool[] off = new bool[2];

        Dungeons.SetHidden(off, 2, true);
        Dungeons.SetHidden(off, -1, true);

        Assert.Equal([false, false], off);
    }
}
