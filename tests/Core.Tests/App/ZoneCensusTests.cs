using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The per-zone census of a save (ZoneCensus.Count): every object that is not terrain, deleted or vanished is counted
/// once, and the pieces are counted among them.
/// </summary>
public class ZoneCensusTests
{
    [Fact]
    public void EveryLiveObjectIsCountedOnce()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var (_, objects) = ZoneCensus.Count(world, edits);
        int expected = Enumerable.Range(0, world.ObjectRefs.Count)
            .Count(i => !world.ObjectRefs[i].IsTerrain && !world.Vanished.Contains(i) && !edits.Deleted.Contains(i));
        Assert.Equal(expected, objects.Values.Sum());
    }

    [Fact]
    public void PiecesAreCountedAmongTheObjects()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var (pieces, objects) = ZoneCensus.Count(world, edits);
        Assert.All(pieces, p => Assert.True(p.Value <= objects[p.Key]));
    }
}
