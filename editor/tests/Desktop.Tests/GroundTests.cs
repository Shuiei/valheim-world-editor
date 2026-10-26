using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The area's ground (Ground) with No limit: what is past the game's ±8 m goes into the lift, and the limit counts from
/// the lifted ground; and where its grid starts in the world.
/// </summary>
public class GroundTests
{
    [Fact]
    public void ANewGroundHoldsItsArraysOnly()
    {
        const int W = 17 * 64 + 1;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var g = new Ground(W, W, 0, 0, 17);
        long made = GC.GetAllocatedBytesForCurrentThread() - before;
        // Base, level, smooth, lift (4 bytes a point each), paint and its base mask (16 each), the two flags (1 each):
        // 50 bytes a point, and no copy of them as loaded.
        Assert.True(made < 55L * W * W, $"{made / (W * W)} bytes a point");
        // An edit store's zone of it: as made, unchanged.
        Assert.All(g.ZoneEdit(0, 0).Modified, Assert.False);
    }

    [Fact]
    public void TheGridStartsHalfAZoneBeforeTheFirstZonesMiddle()
    {
        var g = new Ground(65, 65, 2, -1, 1);
        Assert.Equal((96f, -96f), (g.OriginX, g.OriginZ));
    }

    [Fact]
    public void NoLimitPutsWhatIsPastTheLimitInTheLift()
    {
        var g = new Ground(65, 65, 0, 0, 1);
        int p = 32 * 65 + 32;
        g.Base[p] = 30;
        g.NoLimit = true;
        Assert.False(g.SetHeight(p, 60));
        Assert.Equal(60, g.HeightOf(p), 0.001f);
        Assert.Equal(30, g.Lift[p], 0.001f);
        Assert.False(g.AtLimit(p));
        // With the limit back, the ±8 m count from the lifted ground.
        g.NoLimit = false;
        Assert.True(g.SetHeight(p, 80));
        Assert.Equal(68, g.HeightOf(p), 0.001f);
        Assert.True(g.AtLimit(p));
        var e = g.ZoneEdit(0, 0);
        Assert.Equal(30, e.Lift[p], 0.001f);
        Assert.True(e.HasLift);
    }

    // The limit's stop is said whichever limit it was: No limit's lift (±400 m), and the level part's own ±8 m beside
    // smoothing (the point stops short of a height within the game's ±8 m).
    [Fact]
    public void EveryLimitThatStopsAPointIsSaid()
    {
        var g = new Ground(65, 65, 0, 0, 1);
        int p = 32 * 65 + 32;
        g.Base[p] = 30;
        g.NoLimit = true;
        Assert.True(g.SetHeight(p, 30 + EditStore.MaxLift + 50));
        Assert.Equal(30 + EditStore.MaxLift, g.HeightOf(p), 0.01f);
        g.NoLimit = false;
        Array.Clear(g.Lift);
        g.Mod[p] = 1;
        g.Level[p] = 0;
        g.Smooth[p] = -5;
        Assert.True(g.SetHeight(p, 37));
        Assert.Equal(33, g.HeightOf(p), 0.001f);
        // Within every limit: not stopped.
        Assert.False(g.SetHeight(p, 32));
        Assert.Equal(32, g.HeightOf(p), 0.001f);
    }
}
