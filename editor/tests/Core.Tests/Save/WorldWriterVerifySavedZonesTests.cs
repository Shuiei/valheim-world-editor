using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.TerrainZoneSupport;

namespace WorldEditor.Tests;

/// <summary>
/// The saved zones read back: each saved zone must be in the new save and hold the ground and paint it was given.
/// </summary>
public class WorldWriterVerifySavedZonesTests
{
    /// <summary>The zones of the save, by zone.</summary>
    private static ILookup<(int, int), TerrainZone> Read(params TerrainZone[] zones) => zones.ToLookup(t => (t.ZoneX, t.ZoneZ));

    [Fact]
    public void ASavedZoneMissingFromTheSaveIsReported()
    {
        string? problem = WorldWriter.CheckSavedZones(Read(), [new ZoneEdit(0, 0)]);

        Assert.Equal("zone 0, 0 is missing", problem);
    }

    [Fact]
    public void ASavedZoneReadingBackAsGivenIsRight()
    {
        var edit = new ZoneEdit(0, 0);
        edit.Modified[5] = true;
        edit.Level[5] = 2f;

        Assert.Null(WorldWriter.CheckSavedZones(Read(Matching(edit)), [edit]));
    }

    [Fact]
    public void AHeightThatDiffersIsReportedAtItsPoint()
    {
        var edit = new ZoneEdit(0, 0);
        TerrainZone zone = Matching(edit);
        zone.ModifiedHeight[5] = true;

        Assert.Equal("zone 0, 0 differs at point 5", WorldWriter.CheckSavedZones(Read(zone), [edit]));
    }

    [Fact]
    public void APaintThatDiffersIsReportedAtItsPoint()
    {
        var edit = new ZoneEdit(0, 0);
        edit.PaintModified[7] = true;
        edit.Paint[28] = 1f;
        TerrainZone zone = Matching(edit);
        zone.Paint[7] = new Vector4(0, 0, 0, 0);

        Assert.Equal("zone 0, 0 paint differs at point 7", WorldWriter.CheckSavedZones(Read(zone), [edit]));
    }
}
