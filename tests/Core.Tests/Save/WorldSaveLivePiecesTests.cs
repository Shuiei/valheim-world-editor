using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Live merges of pieces, locations and the world's holds: players' pieces come and go with the game, and a merge waits
/// while the world's lists are read or held elsewhere.
/// </summary>
public class WorldSaveLivePiecesTests
{
    [Fact]
    public async Task LiveAMergeWaitsWhileTheWorldsListsAreReadElsewhere()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var reader = Task.Run(() =>
        {
            lock (world.Sync)
            {
                held.Set();
                release.Wait(TestContext.Current.CancellationToken);
            }
        }, TestContext.Current.CancellationToken);
        held.Wait(TestContext.Current.CancellationToken);
        try
        {
            // Busy: no merge now (the caller tries again later).
            Assert.Null(world.MergeLive([(0, 0)], [], _ => false, _ => false));
        }
        finally
        {
            release.Set();
        }
        await reader;
        Assert.NotNull(world.MergeLive([(0, 0)], [], _ => false, _ => false));
    }

    // A hold (the history written in the background after Apply live) keeps merges out until every hold is let go;
    // letting one go twice counts once.
    [Fact]
    public void LiveAMergeWaitsWhileTheWorldIsHeld()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var first = world.Hold();
        var second = world.Hold();
        Assert.Null(world.MergeLive([(0, 0)], [], _ => false, _ => false));
        first.Dispose();
        first.Dispose();
        Assert.Null(world.MergeLive([(0, 0)], [], _ => false, _ => false));
        second.Dispose();
        Assert.NotNull(world.MergeLive([(0, 0)], [], _ => false, _ => false));
    }

    [Fact]
    public void LiveAPlayersPieceAndAPlacedLocationComeAndGo()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        // ZDOIDs of a user the snapshot has none of.
        // The piece is of a kind the world has none of (a mod's).
        List<(long, uint, byte[])> made =
        [
            (4242, 1, MadeObjects.Piece("mod_live_wall", at, 777, "Astrid")),
            (4242, 2, MadeObjects.Location(at + new Vector3(5, 0, 5), MadeObjects.MadeUpLocation)),
        ];
        int templates = world.TemplatesGeneration;
        Assert.False(world.Templates.ContainsKey(Fixtures.Hash("mod_live_wall")));
        var came = world.MergeLive([(10, 10)], made, _ => false, _ => false)!;
        Assert.Equal(2, came.Added.Count);
        // A kind the world had none of: its first object is its template, and the templates' generation moved on;
        // the same objects again do not move it.
        Assert.True(world.Templates.ContainsKey(Fixtures.Hash("mod_live_wall")));
        Assert.True(world.TemplatesGeneration > templates);
        templates = world.TemplatesGeneration;
        world.MergeLive([(10, 10)], made, _ => false, _ => false);
        Assert.Equal(templates, world.TemplatesGeneration);
        // A placed location shapes the ground as the game runs.
        Assert.True(came.Modifiers);
        Assert.Equal(1, world.Creators[777]);
        Assert.Equal(777, world.TopBuilder);
        Assert.Equal("Astrid", world.PlayerNames[777]);
        Assert.Contains(world.Pieces, p => p.Prefab == Fixtures.Hash("mod_live_wall"));
        Assert.Contains(world.Locations, l => l.Location == MadeObjects.MadeUpLocation);
        Assert.Contains(world.Placed, p => p.Location == MadeObjects.MadeUpLocation);
        // Gone from the game: out of every list, its builder's count down.
        var gone = world.MergeLive([(10, 10)], [], _ => false, _ => false)!;
        Assert.Equal(2, gone.Vanished.Count);
        Assert.True(gone.Modifiers);
        // A builder with no pieces left is no builder: kept at zero, TopBuilder could still name them.
        Assert.False(world.Creators.ContainsKey(777));
        Assert.NotEqual(777, world.TopBuilder);
        Assert.DoesNotContain(world.Pieces, p => p.Prefab == Fixtures.Hash("mod_live_wall"));
        Assert.DoesNotContain(world.Locations, l => l.Location == MadeObjects.MadeUpLocation);
        Assert.DoesNotContain(world.Placed, p => p.Location == MadeObjects.MadeUpLocation);
    }
}
