using System.Numerics;
using TerrainEditor.Save;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedPlaceTool;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's shapes: rectangles that snap to whole pieces, grids with a piece in each cell that turns, zones filled
/// by density, elevations and underwater, a single piece beside another, and the presets.
/// </summary>
public class PlaceToolShapeTests
{
    private const string Wall = "woodwall";

    [Fact]
    public void ARectangleEndToEndSnapsToWholePieces()
    {
        var (t, _) = Tool();
        ChooseWallLine(t, Wall, PlaceTool.LineShapes.Rect);
        t.FigA = new Vector2(40, 40);
        t.FigB = new Vector2(47, 45);
        var e = t.EndsOf(Wall)!;
        var b = t.SnapFigure(t.FigA.Value, t.FigB.Value);
        Assert.Equal(0, (b.X - 40) % e.Len, 3);
        var p = t.Preview(null);
        Assert.Equal(2 * ((b.X - 40) / e.Len + (b.Y - 40) / e.Len), p.Count, 1);
    }

    [Fact]
    public void AGridHasOneInEachCellAndTurns()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Grid;
        t.Cell = 4;
        t.GridA = new Vector2(40, 40);
        t.GridB = new Vector2(60, 52);
        var p = t.Preview(null);
        Assert.Equal(15, p.Count);
        Assert.Contains(p, o => Vector2.Distance(o.G, new Vector2(42, 42)) < 1e-3f);
        Assert.True(t.TurnShape(90));
        var q = t.Preview(null);
        Assert.Equal(15, q.Count);
        Assert.DoesNotContain(q, o => Vector2.Distance(o.G, new Vector2(42, 42)) < 1e-3f);
    }

    [Fact]
    public void AZoneIsFilledByDensity()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Zone;
        t.Density = 5;
        t.Spacing = 2;
        foreach (var p in new[] { new Vector2(30, 30), new Vector2(70, 30), new Vector2(70, 70), new Vector2(30, 70) })
        {
            t.Points.Add(p);
        }
        var all = t.Preview(null);
        Assert.InRange(all.Count, 60, 80);
        Assert.All(all, o => Assert.InRange(o.G.X, 30, 70));
        t.Clump = 100;
        Assert.True(t.Preview(null).Count < all.Count / 2);
    }

    [Fact]
    public void ElevationsAndUnderwater()
    {
        var (t, s) = Tool();
        t.OneAtATime = true;
        t.Elevation = PlaceTool.Elevations.At;
        t.Elev = 50;
        Assert.Equal(50, t.Preview(new Vector2(50, 50))[0].Position.Y, 3);
        t.Elevation = PlaceTool.Elevations.Above;
        t.Elev = 2;
        Assert.Equal(31.95f, t.Preview(new Vector2(50, 50))[0].Position.Y, 3);
        Assert.Equal("Elevation: above the ground by 2.5 m.", t.NudgeElevation(0.5f).Shown);
        // Under water nothing grows (sea level is 30 in this test area: lower it there).
        t.Elevation = PlaceTool.Elevations.Ground;
        s.Shape(50, 50, Formula.Compile("-3", []), 3, 0, "pond");
        Assert.Empty(t.Preview(new Vector2(50, 50)));
    }

    [Fact]
    public void ASinglePieceSnapsBesideAnother()
    {
        // A wall standing at grid (64, 64), facing north.
        var (t, _) = Tool(new WorldScene.Thing(1, StableHash.Of(Wall), new Vector3(32, 30, 32), Vector3.Zero, 0, true));
        t.Chosen.Clear();
        t.Chosen.Add(Wall);
        t.AutoSnap();
        var e = t.EndsOf(Wall)!;
        var p = Assert.Single(t.Preview(new Vector2(64 + e.Len + 0.3f, 64.2f)));
        Assert.Equal(PlaceTool.SnapWays.Beside, t.Snapped?.Way);
        // End to end with it: one wall length along.
        Assert.Equal(e.Len, Vector2.Distance(p.G, new Vector2(64, 64)), 2);
        Assert.Equal(30, p.Position.Y, 2);
        t.OnTop = true;
        var top = Assert.Single(t.Preview(new Vector2(64.2f, 64)));
        Assert.Equal(PlaceTool.SnapWays.OnTop, t.Snapped?.Way);
        // Its bottom on the top of the one below: one wall higher.
        Assert.Equal(30 + e.H, top.Position.Y, 2);
    }

    [Fact]
    public void PresetsLoadTheKindsTheWorldCanMake()
    {
        var t = new PlaceTool();
        string msg = t.Load(PlaceTool.BuiltIn[0], n => n != "Oak1").Shown;
        Assert.DoesNotContain("Oak1", t.Chosen);
        Assert.Equal(4, t.WeightOf("Beech1"));
        Assert.Contains("1 kind(s) this world cannot place left out", msg);
        Assert.Equal(50, t.Clump);
        Assert.StartsWith("None of the kinds", t.Load(PlaceTool.BuiltIn[0], _ => false).Shown);
    }
}
