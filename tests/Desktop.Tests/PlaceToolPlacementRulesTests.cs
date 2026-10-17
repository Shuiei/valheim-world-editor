using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Where a placed object may stand: on the block and not masked out, how far it keeps from others, the height a piece
/// along a run stands at, and the layers a tall piece stacks.
/// </summary>
public class PlaceToolPlacementRulesTests
{
    [Fact]
    public void AGridPointOnTheEdgeMetreIsOffTheBlock()
    {
        var w = TestScenes.Flat(2).Scene;
        Assert.False(PlaceTool.OnBlockAndMask(w, new Vector2(0.5f, 10), null));
        Assert.False(PlaceTool.OnBlockAndMask(w, new Vector2(10, w.H - 1.5f), null));
        Assert.True(PlaceTool.OnBlockAndMask(w, new Vector2(1, 1), null));
        Assert.True(PlaceTool.OnBlockAndMask(w, new Vector2(w.W - 2, w.H - 2), null));
    }

    [Fact]
    public void AMaskedOutPointIsOffTheBlock()
    {
        var w = TestScenes.Flat(2).Scene;
        var g = new Vector2(10, 10);
        int at = 10 * w.W + 10;
        Assert.False(PlaceTool.OnBlockAndMask(w, g, i => i == at ? 0 : 1));
        Assert.True(PlaceTool.OnBlockAndMask(w, g, i => i == at ? 0.5f : 1));
    }

    [Fact]
    public void ObjectsKeepTheSpacingWhenNotPlacedOneAtATime()
    {
        var (t, _) = SharedPlaceTool.Tool();
        t.OneAtATime = false;
        t.Spacing = 3;
        Assert.Equal(3f, t.SpacingFor("Beech1"));
    }

    [Fact]
    public void OneAtATimeOnlyKeepsOffTheObjectRightOnTop()
    {
        var (t, _) = SharedPlaceTool.Tool();
        t.OneAtATime = true;
        t.SnapTo = false;
        Assert.Equal(0.3f, t.SpacingFor("Beech1"));
    }

    [Fact]
    public void APieceSnapsRightOverThePieceBelowOnBrushMode()
    {
        var (t, _) = SharedPlaceTool.Tool();
        t.OneAtATime = true;
        t.SnapTo = true;
        t.Mode = PlaceTool.Modes.Brush;
        Assert.Equal(0f, t.SpacingFor("woodwall"));
    }

    [Fact]
    public void ARunPieceNeedsAMetreFromTheEdgeOfTheBlock()
    {
        var w = TestScenes.Flat(2).Scene;
        Assert.True(PlaceTool.OnRunBlock(w, 1, 1, null));
        Assert.False(PlaceTool.OnRunBlock(w, 0.9f, 5, null));
        Assert.True(PlaceTool.OnRunBlock(w, w.W - 2, w.H - 2, null));
        Assert.False(PlaceTool.OnRunBlock(w, w.W - 1.5f, 5, null));
    }

    [Fact]
    public void AnElevatedRunPieceStandsAtTheElevation()
    {
        var (t, s) = SharedPlaceTool.Tool();
        t.Elevation = PlaceTool.Elevations.At;
        t.Elev = 7;
        Assert.Equal(7f, t.RunBottom(s.Scene, new Vector2(1, 1), new Vector2(2, 1), null));
    }

    [Fact]
    public void AGroundRunPieceNextToASnappedPieceStandsAtItsHeight()
    {
        var (t, s) = SharedPlaceTool.Tool();
        t.Elevation = PlaceTool.Elevations.Ground;
        Assert.Equal(5f, t.RunBottom(s.Scene, new Vector2(1, 1), new Vector2(2, 1), new Vector3(0, 5, 0)));
    }

    [Fact]
    public void ATallRunPieceStacksItsLayers()
    {
        var outp = new List<PlaceTool.Placement>();
        var e = new PlaceTool.Ends(Vector3.Zero, new Vector3(2, 0, 0), 2, 1, 0);
        PlaceTool.AddRunLayers(outp, "Beech1", e, 5, 6, 1, 0, 3, 0, 0);
        Assert.Equal(3, outp.Count);
        Assert.Equal(new[] { 1f, 2f, 3f }, outp.Select(o => o.Position.Y).ToArray());
    }

    [Fact]
    public void AFlatRunPieceHasOneLayer()
    {
        var outp = new List<PlaceTool.Placement>();
        var e = new PlaceTool.Ends(Vector3.Zero, new Vector3(2, 0, 0), 2, 0.05f, 0);
        PlaceTool.AddRunLayers(outp, "Beech1", e, 5, 6, 1, 0, 3, 0, 0);
        Assert.Single(outp);
    }

    [Fact]
    public void LayersStopAtTheShapeLimit()
    {
        var outp = new List<PlaceTool.Placement>();
        for (int i = 0; i < PlaceTool.MaxShape - 1; i++)
        {
            outp.Add(new PlaceTool.Placement("Beech1", Vector3.Zero, Vector3.Zero, 1, Vector2.Zero));
        }
        var e = new PlaceTool.Ends(Vector3.Zero, new Vector3(2, 0, 0), 2, 1, 0);
        PlaceTool.AddRunLayers(outp, "Beech1", e, 5, 6, 1, 0, 3, 0, 0);
        Assert.Equal(PlaceTool.MaxShape, outp.Count);
    }

    [Fact]
    public void TheGapIsTheWalkFromWhereTheRunStoppedToItsEnd()
    {
        var (t, _) = SharedPlaceTool.Tool();
        var pts = new List<Vector2> { new(0, 0), new(3, 4), new(3, 8) };
        t.AddSnapGap(pts, 0, new Vector2(0, 0));
        Assert.InRange(t.SnapGap, 8.999f, 9.001f);
    }
}
