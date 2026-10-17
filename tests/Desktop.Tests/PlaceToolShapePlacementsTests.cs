using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The placements of each shape on its own: the zone's spots, the pieces end to end along a line, the objects along
/// a line, and the objects in a grid of cells.
/// </summary>
public class PlaceToolShapePlacementsTests
{
    /// <summary>
    /// A Place tool on a flat zone area, Beech1 chosen and no tilt, with a hash of the spacing.
    /// </summary>
    private static (PlaceTool T, WorldScene S, PlaceTool.Hash H) Tool()
    {
        var (t, s) = SharedPlaceTool.Tool();
        return (t, s.Scene, new PlaceTool.Hash(4));
    }

    /// <summary>
    /// A line of two points (20, 20) and (40, 20) placed end to end with this kind, the Line mode set.
    /// </summary>
    private static List<PlaceTool.Placement> EndToEndLinePlacements(PlaceTool t, WorldScene s, string kind)
    {
        t.Mode = PlaceTool.Modes.Line;
        t.EndToEnd = true;
        t.Points.Add(new Vector2(20, 20));
        t.Points.Add(new Vector2(40, 20));
        var outp = new List<PlaceTool.Placement>();
        t.EndToEndPlacements(s, [kind], null, null, outp);
        return outp;
    }

    [Fact]
    public void AZoneNeedsThreePointsToPlaceAnything()
    {
        var (t, s, h) = Tool();
        t.Mode = PlaceTool.Modes.Zone;
        t.Points.Add(new Vector2(20, 20));
        t.Points.Add(new Vector2(40, 20));
        var outp = new List<PlaceTool.Placement>();
        t.ZonePlacements(s, ["Beech1"], h, null, outp);
        Assert.Empty(outp);
    }

    [Fact]
    public void ZoneSpotsStayInsideTheZone()
    {
        var (t, s, h) = Tool();
        t.Mode = PlaceTool.Modes.Zone;
        t.Density = 20;
        foreach (var p in new[] { new Vector2(20, 20), new Vector2(60, 20), new Vector2(60, 60), new Vector2(20, 60) })
        {
            t.Points.Add(p);
        }
        var outp = new List<PlaceTool.Placement>();
        t.ZonePlacements(s, ["Beech1"], h, null, outp);
        Assert.NotEmpty(outp);
        Assert.All(outp, o => Assert.InRange(o.G.X, 20, 60));
        Assert.All(outp, o => Assert.InRange(o.G.Y, 20, 60));
    }

    [Fact]
    public void EndToEndWithoutAKindThatHasEndsPlacesNothing()
    {
        var (t, s, _) = Tool();
        Assert.Empty(EndToEndLinePlacements(t, s, "Beech1"));
    }

    [Fact]
    public void EndToEndLaysPiecesAlongTheRun()
    {
        var (t, s, _) = Tool();
        var outp = EndToEndLinePlacements(t, s, "woodwall");
        Assert.NotEmpty(outp);
        Assert.All(outp, o => Assert.Equal("woodwall", o.Name));
    }

    [Fact]
    public void ALineNeedsTwoPointsToPlaceAnything()
    {
        var (t, s, h) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.Points.Add(new Vector2(20, 20));
        var outp = new List<PlaceTool.Placement>();
        t.LinePlacements(s, ["Beech1"], h, null, outp);
        Assert.Empty(outp);
    }

    [Fact]
    public void ObjectsAlongALineStayOnIt()
    {
        var (t, s, h) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.Every = 4;
        t.Points.Add(new Vector2(20, 20));
        t.Points.Add(new Vector2(40, 20));
        var outp = new List<PlaceTool.Placement>();
        t.LinePlacements(s, ["Beech1"], h, null, outp);
        Assert.InRange(outp.Count, 4, 6);
        Assert.All(outp, o => Assert.InRange(o.G.Y, 19.99f, 20.01f));
    }

    [Fact]
    public void AGridPlacesObjectsOnlyInsideItsCorners()
    {
        var (t, s, h) = Tool();
        t.Mode = PlaceTool.Modes.Grid;
        t.Cell = 4;
        var outp = new List<PlaceTool.Placement>();
        t.GridPlacements(s, ["Beech1"], h, null, new Vector2(20, 20), new Vector2(30, 30), outp);
        Assert.InRange(outp.Count, 1, 4);
        Assert.All(outp, o => Assert.InRange(o.G.X, 20, 30));
        Assert.All(outp, o => Assert.InRange(o.G.Y, 20, 30));
    }
}
