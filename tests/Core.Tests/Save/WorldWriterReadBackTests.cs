using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Ground and paint that cannot read back the same: the save is refused and the backup restored.
/// </summary>
public class WorldWriterReadBackTests
{
    [Fact]
    public void AValueThatDoesNotReadBackTheSameRestoresTheBackup()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var e = FirstZoneEditCopy.Of(world);
        // Not a number never equals itself: the read-back check finds the zone different.
        e.Modified[500] = true;
        e.Level[500] = float.NaN;
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [e]);
        Assert.False(r.Saved);
        Assert.StartsWith("The saved world did not read back correctly (zone", r.Message);
        Assert.Contains("differs at point 500", r.Message);
        Assert.Contains("Nothing changed", r.Message);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void PaintThatDoesNotReadBackTheSameRestoresTheBackup()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var e = FirstZoneEditCopy.Of(world);
        e.PaintModified[600] = true;
        e.Paint[600 * 4] = float.NaN;
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [e]);
        Assert.False(r.Saved);
        Assert.Contains("paint differs at point 600", r.Message);
        AssertSame(before, w.Dir);
    }
}
