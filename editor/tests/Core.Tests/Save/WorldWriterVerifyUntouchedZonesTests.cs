using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.TerrainZoneSupport;

namespace WorldEditor.Tests;

/// <summary>
/// The zones a save did not edit or reset: each must read back exactly as it was before the save.
/// </summary>
public class WorldWriterVerifyUntouchedZonesTests
{
    /// <summary>A world whose only terrain zone is this one.</summary>
    private static WorldSave Before(TerrainZone zone)
    {
        var world = new WorldSave { Directory = "none", SaveNumber = 1 };
        world.TerrainZones.Add(zone);
        return world;
    }

    /// <summary>The zones of the save, by zone.</summary>
    private static ILookup<(int, int), TerrainZone> Read(params TerrainZone[] zones) => zones.ToLookup(t => (t.ZoneX, t.ZoneZ));

    [Fact]
    public void AZoneThatReadsBackTheSameIsUntouched()
    {
        TerrainZone zone = Flat(3, 3);

        Assert.Null(WorldWriter.CheckUntouchedZones(Before(zone), Read(Flat(3, 3)), [], []));
    }

    [Fact]
    public void AZoneThatReadsBackAlteredIsReported()
    {
        TerrainZone altered = Flat(3, 3);
        altered.LevelDelta[0] = 1f;

        Assert.Equal("unchanged zone 3, 3 was altered", WorldWriter.CheckUntouchedZones(Before(Flat(3, 3)), Read(altered), [], []));
    }

    [Fact]
    public void AZoneMissingFromTheSaveIsReported()
    {
        Assert.Equal("unchanged zone 3, 3 was altered", WorldWriter.CheckUntouchedZones(Before(Flat(3, 3)), Read(), [], []));
    }

    [Fact]
    public void AZoneTheSaveEditedIsNotChecked()
    {
        TerrainZone altered = Flat(3, 3);
        altered.LevelDelta[0] = 1f;

        Assert.Null(WorldWriter.CheckUntouchedZones(Before(Flat(3, 3)), Read(altered), [(3, 3)], []));
    }

    [Fact]
    public void AZoneWhoseGroundIsResetIsNotChecked()
    {
        Assert.Null(WorldWriter.CheckUntouchedZones(Before(Flat(3, 3)), Read(), [], [(3, 3)]));
    }
}
