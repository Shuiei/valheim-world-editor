using System.Runtime.CompilerServices;
using ValheimGen;
using Xunit;
using static WorldEditor.Tests.MapDataTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The global world map (MapData.Global): its layout, a world opened again keeps its map, a map no longer used can be collected, and a
/// cancelled map throws even when kept.
/// </summary>
[Collection("World files")]
public class MapDataGlobalTests
{
    [Fact]
    public void TheGlobalMapUsesTheGamesLayout()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = Open(w);
        var g = new MapData(terrain, edits).Global;
        Assert.Equal(MapData.GlobalSize, g.Width);
        Assert.Equal(MapData.GlobalSize, g.Height);
        // The corner is far out at sea: below the water, blue mask, no forest.
        Assert.True((float)g.Heights[0] < 30f);
        Assert.Equal(0, g.Mask[0]);
        // Somewhere near the middle is land with a known biome colour (opaque).
        int mid = (MapData.GlobalSize / 2) * MapData.GlobalSize + MapData.GlobalSize / 2;
        Assert.Equal(255, g.Biome[mid * 4 + 3]);
    }

    // The same world opened again (Reload, or back to the worlds and in again): its world map is the one built
    // before, not built again; edits never change it (base terrain only).
    [Fact]
    public void TheGlobalMapOfAWorldOpenedAgainIsNotBuiltAgain()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = Open(w);
        var first = new MapData(terrain, edits).Global;
        var (_, again, againEdits) = Open(w);
        Assert.Same(first, new MapData(again, againEdits).Global);
    }

    // The world map kept for opening the world again does not hold its memory once no map uses it.
    [Fact]
    public void AWorldMapNoLongerUsedCanBeCollected()
    {
        using var w = new TempWorld();
        var map = UsedOnce(w);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(map.IsAlive);
    }

    /// <summary>A world map built for a world and dropped at once.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UsedOnce(TempWorld w)
    {
        var (_, terrain, edits) = Open(w);
        return new WeakReference(new MapData(terrain, edits).Global);
    }

    // Cancelled before it is asked for: thrown, as when it is built, though the world map is kept.
    [Fact]
    public void ACancelledWorldMapThrowsEvenWhenItIsKept()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = Open(w);
        _ = new MapData(terrain, edits).Global;
        var (_, again, againEdits) = Open(w);
        Assert.Throws<OperationCanceledException>(() => new MapData(again, againEdits, new CancellationToken(canceled: true)).Global);
    }
}
