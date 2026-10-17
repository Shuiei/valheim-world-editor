using ValheimGen;
using Xunit;
using static WorldEditor.Tests.MapDataTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The close-up cache of the world map (MapData): each close-up is kept per edit version, the cache stays small, and a build of
/// older edits never drops newer ones.
/// </summary>
[Collection("World files")]
public class MapDataCacheTests
{
    [Fact]
    public void CloseUpsAreKeptPerEditVersionAndTheCacheStaysSmall()
    {
        using var w = new TempWorld();
        var map = MapOf(w);
        var first = map.Detail(288, 288, 64, 1);
        Assert.Same(first, map.Detail(288, 288, 64, 1));
        // The window before the last one is kept too (panning back), and asking for it keeps it longest.
        var second = map.Detail(352, 288, 64, 1);
        Assert.Same(first, map.Detail(288, 288, 64, 1));
        Assert.Same(second, map.Detail(352, 288, 64, 1));
        // A third window drops the oldest one asked for: it is built again when asked.
        map.Detail(416, 288, 64, 1);
        Assert.Same(second, map.Detail(352, 288, 64, 1));
        Assert.NotSame(first, map.Detail(288, 288, 64, 1));
    }

    [Fact]
    public void CloseUpsOfAnOlderEditVersionAreDropped()
    {
        using var w = new TempWorld();
        var map = MapOf(w);
        var first = map.Detail(288, 288, 64, 1);
        var other = map.Detail(352, 288, 64, 1);
        Assert.NotSame(first, map.Detail(288, 288, 64, 2));
        // Version 1's other window went when version 2 came, though there was room for it.
        Assert.NotSame(other, map.Detail(352, 288, 64, 1));
    }

    [Fact]
    public async Task ACloseUpOfOlderEditsDoesNotDropNewerOnes()
    {
        using var w = new TempWorld();
        var map = MapOf(w);
        using var reached = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int builds = 0;
        // The first build (older edits) waits after its cache lookup until the newer one has been built and kept.
        map.BeforeDetailBuild = () =>
        {
            if (Interlocked.Increment(ref builds) == 1)
            {
                reached.Set();
                if (!release.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("The older build was never released.");
                }
            }
        };
        var olderBuild = Task.Run(() => map.Detail(352, 288, 64, 1), TestContext.Current.CancellationToken);
        Assert.True(reached.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        var newer = map.Detail(288, 288, 64, 2);
        release.Set();
        var older = await olderBuild;
        // The older build ended last: it is given but not kept, and the newer window stays.
        Assert.Same(newer, map.Detail(288, 288, 64, 2));
        Assert.NotSame(older, map.Detail(352, 288, 64, 1));
    }

    /// <summary>
    /// The map of the test world in this temporary folder, made from its terrain and its edits.
    /// </summary>
    private static MapData MapOf(TempWorld w)
    {
        var (_, terrain, edits) = Open(w);
        return new MapData(terrain, edits);
    }
}
