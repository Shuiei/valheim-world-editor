using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// DungeonShell.PastRoof: whether a pillar segment rises past the roof slab over a height.
/// </summary>
public class DungeonShellRoofTests
{
    [Fact]
    public void ASegmentThatStopsWithinTheRoofSlabIsNotPastIt()
    {
        // A 3 m segment from 0 reaches 3 m: the slab over a roof at 1 m tops out at 1 + 2 = 3 m (plus 0.01 m allowance), so it is not past.
        Assert.False(DungeonShell.PastRoof(new DungeonKit.Column("Pillar", 3f, 0f), 0f, 1f));
    }

    [Fact]
    public void ASegmentThatRisesWellPastTheRoofSlabIsPastIt()
    {
        // The same segment over a roof at 0 m reaches 3 m, past the 2 m slab.
        Assert.True(DungeonShell.PastRoof(new DungeonKit.Column("Pillar", 3f, 0f), 0f, 0f));
    }

    [Fact]
    public void TheStartingHeightCountsTowardTheTop()
    {
        // From 1 m, the 3 m segment reaches 4 m: past the 2 m slab over a roof at 0 m, so the start height adds to the reach.
        Assert.True(DungeonShell.PastRoof(new DungeonKit.Column("Pillar", 3f, 0f), 1f, 0f));
    }
}
