using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether two zones end up with the same ground and paint: how a height is split between level and smoothing does not
/// count, but a height or a painted vertex that differs does.
/// </summary>
public class ZoneEditSameGroundTests
{
    /// <summary>A zone with its first vertex's height changed by level and smoothing.</summary>
    private static ZoneEdit HeightAtFirstVertex(float level, float smooth = 0f)
    {
        var zone = new ZoneEdit(0, 0);
        zone.Modified[0] = true;
        zone.Level[0] = level;
        zone.Smooth[0] = smooth;
        return zone;
    }

    /// <summary>A zone with its first vertex painted with this value.</summary>
    private static ZoneEdit PaintAtFirstVertex(float paint)
    {
        var zone = new ZoneEdit(0, 0);
        zone.PaintModified[0] = true;
        zone.Paint[0] = paint;
        return zone;
    }

    [Fact]
    public void TwoZonesWithNoChangesHaveTheSameGround()
    {
        Assert.True(new ZoneEdit(0, 0).SameGround(new ZoneEdit(0, 0)));
    }

    [Fact]
    public void AHeightSplitDifferentlyBetweenLevelAndSmoothingIsTheSameGround()
    {
        Assert.True(HeightAtFirstVertex(0.5f).SameGround(HeightAtFirstVertex(0.25f, 0.25f)));
    }

    [Fact]
    public void AHeightDifferingAtAVertexIsNotTheSameGround()
    {
        Assert.False(HeightAtFirstVertex(0.5f).SameGround(HeightAtFirstVertex(0.75f)));
    }

    [Fact]
    public void APaintDifferingAtAPaintedVertexIsNotTheSameGround()
    {
        Assert.False(PaintAtFirstVertex(0.5f).SameGround(PaintAtFirstVertex(0.9f)));
    }
}
