using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether two zones' edits of one point are the same change, for the merge: the height change (whether it is made, its
/// level and smoothing) and the paint (whether it is made, and its four values), each to within 1e-4.
/// </summary>
public class EditReplaySameChangeTests
{
    /// <summary>
    /// Whether two zones have a point's height change alike, through the replay's own check.
    /// </summary>
    private static bool SameHeight(ZoneEdit a, ZoneEdit b, int i) => (bool)PrivateCall.Static(typeof(EditReplay), "SameHeight", a, b, i)!;

    /// <summary>
    /// Whether two zones have a point's paint alike, through the replay's own check.
    /// </summary>
    private static bool SamePaint(ZoneEdit a, ZoneEdit b, int i) => (bool)PrivateCall.Static(typeof(EditReplay), "SamePaint", a, b, i)!;

    /// <summary>
    /// A zone whose point 0 has its height changed, at this level and smoothing.
    /// </summary>
    private static ZoneEdit Changed(float level, float smooth)
    {
        var zone = new ZoneEdit(0, 0);
        zone.Modified[0] = true;
        zone.Level[0] = level;
        zone.Smooth[0] = smooth;
        return zone;
    }

    /// <summary>
    /// A zone whose point 0 is painted.
    /// </summary>
    private static ZoneEdit Painted()
    {
        var zone = new ZoneEdit(0, 0);
        zone.PaintModified[0] = true;
        return zone;
    }

    [Fact]
    public void AHeightNotChangedInEitherZoneIsTheSameWhateverItsLevel()
    {
        var a = new ZoneEdit(0, 0);
        var b = new ZoneEdit(0, 0);
        a.Level[0] = 2f;
        b.Level[0] = -3f;

        Assert.True(SameHeight(a, b, 0));
    }

    [Fact]
    public void AHeightChangedInOnlyOneZoneIsNotTheSame()
    {
        var a = new ZoneEdit(0, 0);
        var b = new ZoneEdit(0, 0);
        b.Modified[0] = true;

        Assert.False(SameHeight(a, b, 0));
    }

    [Fact]
    public void HeightChangesWithALevelOffByMoreThanATenThousandthAreNotTheSame()
    {
        var a = Changed(level: 1f, smooth: 0f);
        var b = Changed(level: 1.001f, smooth: 0f);

        Assert.False(SameHeight(a, b, 0));
    }

    [Fact]
    public void HeightChangesWithTheSameLevelAndSmoothingAreTheSame()
    {
        var a = Changed(level: 1f, smooth: 0.5f);
        var b = Changed(level: 1f, smooth: 0.5f);

        Assert.True(SameHeight(a, b, 0));
    }

    [Fact]
    public void AnUnpaintedPointIsTheSamePaintWhateverItsPaintValues()
    {
        var a = new ZoneEdit(0, 0);
        var b = new ZoneEdit(0, 0);
        b.Paint[0] = 0.9f;

        Assert.True(SamePaint(a, b, 0));
    }

    [Fact]
    public void PaintedInOnlyOneZoneIsNotTheSame()
    {
        var a = new ZoneEdit(0, 0);
        var b = new ZoneEdit(0, 0);
        b.PaintModified[0] = true;

        Assert.False(SamePaint(a, b, 0));
    }

    [Fact]
    public void PaintWithOneChannelDifferentIsNotTheSame()
    {
        var a = Painted();
        var b = Painted();
        a.Paint[2] = 0.25f;
        b.Paint[2] = 0.75f;

        Assert.False(SamePaint(a, b, 0));
    }

    [Fact]
    public void PaintWithTheSameFourValuesIsTheSame()
    {
        var a = Painted();
        var b = Painted();
        a.Paint[0] = 0.1f;
        a.Paint[1] = 0.2f;
        a.Paint[2] = 0.3f;
        a.Paint[3] = 0.4f;
        b.Paint[0] = 0.1f;
        b.Paint[1] = 0.2f;
        b.Paint[2] = 0.3f;
        b.Paint[3] = 0.4f;

        Assert.True(SamePaint(a, b, 0));
    }
}
