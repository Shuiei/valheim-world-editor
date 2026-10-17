using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The ground parts of a step: a ground change's points and their values, a mountain's raise, the zone ground cleared
/// by a reset, the ground read again, and what is inside the area.
/// </summary>
public class EditSessionGroundStepTests
{
    [Fact]
    public void NoGroundChangeMakesAnEmptyGroundPart()
    {
        var s = TestScenes.Flat(1);
        var (points, before, after, zones) = s.ChangeGround(null);
        Assert.Empty(points);
        Assert.Empty(zones);
    }

    [Fact]
    public void AGroundChangeThatTouchesNothingMakesAnEmptyGroundPart()
    {
        var s = TestScenes.Flat(1);
        var (points, before, after, zones) = s.ChangeGround(_ => (new List<int>(), (0, 0, 0, 0)));
        Assert.Empty(points);
        Assert.Empty(zones);
    }

    [Fact]
    public void AGroundChangeReportsItsPointsAndTheirZone()
    {
        var s = TestScenes.Flat(1);
        int p = 32 * s.Ground.W + 32;
        var (points, before, after, zones) = s.ChangeGround(g =>
        {
            g.SetHeight(p, g.HeightOf(p) + 2);
            return (new List<int> { p }, (31, 31, 33, 33));
        });
        Assert.Equal(new[] { p }, points);
        Assert.Equal(new[] { (0, 0) }, zones.ToArray());
        Assert.True(before.Level[0] != after.Level[0] || before.Smooth[0] != after.Smooth[0]);
    }

    [Fact]
    public void RaiseGroundRaisesTheSquareInReachAndLeavesTheLimitAsItWas()
    {
        var s = TestScenes.Flat(1);
        var g = s.Ground;
        var (touched, rect) = EditSession.RaiseGround(g, 32, 32, 2, (_, _) => 1f, null);
        Assert.Equal(25, touched.Count);
        Assert.Equal((29, 29, 35, 35), rect);
        Assert.False(g.NoLimit);
    }

    [Fact]
    public void RaiseGroundLeavesPointsTheMaskCloses()
    {
        var s = TestScenes.Flat(1);
        var (touched, _) = EditSession.RaiseGround(s.Ground, 32, 32, 2, (_, _) => 1f, _ => 0f);
        Assert.Empty(touched);
    }

    [Fact]
    public void RaiseGroundSkipsAZeroHeight()
    {
        var s = TestScenes.Flat(1);
        var (touched, _) = EditSession.RaiseGround(s.Ground, 32, 32, 2, (_, _) => 0f, null);
        Assert.Empty(touched);
    }

    [Fact]
    public void ClearZoneGroundUndoesTheEditsInTheZone()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 2, 3, 0, "A");
        int center = 32 * s.Ground.W + 32;
        var (touched, _) = s.ClearZoneGround(s.Ground, [(0, 0)]);
        Assert.Contains(center, touched);
        Assert.Equal((byte)0, s.Ground.Mod[center]);
        Assert.Equal(0f, s.Ground.Level[center]);
    }

    [Fact]
    public void ReadGroundTouchesTheWholeArea()
    {
        var s = TestScenes.Flat(1);
        s.TakeDirty();
        s.ReadGround(false);
        Assert.Equal((0, 0, 64, 64), s.TakeDirty());
    }

    [Fact]
    public void InsideAreaOnlyCountsPositionsInsideItsZones()
    {
        var s = TestScenes.Flat(1);
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ;
        Assert.True(s.InsideArea(new Vector3(ox + 32, 30, oz + 32)));
        Assert.False(s.InsideArea(new Vector3(ox + 500, 30, oz + 32)));
    }
}
