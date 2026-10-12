using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's remaining branches, on a flat 2 × 2 zone area: ends from a model box along either axis, end to end
/// with no known length, at one height, above the ground, masked or off the edge, flat pieces never stacked, the
/// leftover past several points, repeated points, facings along the line, the most a shape places, single pieces with
/// nothing near or nothing meeting, zone turns, figures without end to end, short loops, and heights picked with no
/// area, off a piece, or off a piece of no known name.
/// </summary>
public class PlaceToolBranchesTests
{
    private const string Wall = "woodwall";

    /// <summary>
    /// A Place tool on a flat 2 × 2 zone area with these objects, Beech1 chosen, no tilt, and made-up kinds with model
    /// boxes: Deep (2 m along z), Speck (1 cm), Slab (2 m long, 5 cm tall) and Post (2 m long, 2 m tall).
    /// </summary>
    private static (PlaceTool T, EditSession S) Tool(params WorldScene.Thing[] things)
    {
        var s = TestScenes.Flat(2, things);
        var t = new PlaceTool
        {
            Scene = () => s.Scene,
            Brush = s.Brush,
            NameOf = h => h == StableHash.Of(Wall) ? Wall : h == StableHash.Of("Beech1") ? "Beech1" : null,
            ModelBox = n => n switch
            {
                "Deep" => (new Vector3(-0.2f, 0, -1), new Vector3(0.2f, 2, 1)),
                "Speck" => (Vector3.Zero, new Vector3(0.01f, 0.01f, 0.01f)),
                "Slab" => (new Vector3(-1, 0, -0.2f), new Vector3(1, 0.05f, 0.2f)),
                "Post" => (new Vector3(-1, 0, -0.2f), new Vector3(1, 2, 0.2f)),
                _ => null,
            },
        };
        t.Chosen.Clear();
        t.Chosen.Add("Beech1");
        t.Tilt = 0;
        return (t, s);
    }

    /// <summary>
    /// A line of these points (no curve) of the one kind, end to end.
    /// </summary>
    private static void Run(PlaceTool t, string kind, params Vector2[] points)
    {
        t.Mode = PlaceTool.Modes.Line;
        t.Curve = false;
        t.EndToEnd = true;
        t.Chosen.Clear();
        t.Chosen.Add(kind);
        t.Points.Clear();
        t.Points.AddRange(points);
    }

    [Fact]
    public void EndsComeFromTheModelBoxAlongEitherAxis()
    {
        var (t, _) = Tool();
        var deep = t.EndsOf("Deep")!;
        Assert.Equal(2, deep.Len, 3);
        Assert.Equal(new Vector3(0, 0, -1), deep.A);
        Assert.Equal(new Vector3(0, 0, 1), deep.B);
        Assert.Equal(0, deep.Heading, 3);
        // Shorter than 5 cm, or no model known: no ends.
        Assert.Null(t.EndsOf("Speck"));
        Assert.Null(t.EndsOf("Beech1"));
    }

    [Fact]
    public void EndToEndWithNoKnownLengthPlacesNothing()
    {
        var (t, _) = Tool();
        Run(t, "Beech1", new(40, 64), new(60, 64));
        Assert.Empty(t.Preview(null));
    }

    [Fact]
    public void AnEndToEndRunGoesAtOneHeightOrAboveAndStacksOnlyTallPieces()
    {
        var (t, _) = Tool();
        Run(t, "Slab", new(20, 64), new(30, 64));
        t.Layers = 3;
        t.SnapTo = false;
        // Flat pieces are never stacked: one each, five 2 m slabs in 10 m.
        var ground = t.Preview(null);
        Assert.Equal(5, ground.Count);
        Assert.All(ground, o => Assert.Equal(30, o.Position.Y, 3));
        t.Elevation = PlaceTool.Elevations.At;
        t.Elev = 40;
        Assert.All(t.Preview(null), o => Assert.Equal(40, o.Position.Y, 3));
        t.Elevation = PlaceTool.Elevations.Above;
        t.Elev = 2;
        Assert.All(t.Preview(null), o => Assert.Equal(32, o.Position.Y, 3));
        // Tall pieces stack three high.
        Run(t, "Post", new(20, 64), new(30, 64));
        Assert.Equal(15, t.Preview(null).Count);
        // The mask, or the block's edge, keeps them off.
        t.Mask = () => _ => 0;
        Assert.Empty(t.Preview(null));
        t.Mask = () => null;
        Run(t, "Post", new(-6, 64), new(6, 64));
        var edge = t.Preview(null);
        Assert.NotEmpty(edge);
        Assert.All(edge, o => Assert.True(o.G.X >= 1));
    }

    [Fact]
    public void TheLeftoverCountsEveryStretchPastTheLastPiece()
    {
        var (t, _) = Tool();
        Run(t, "Post", new(40, 64), new(50, 64), new(50.5f, 64));
        t.SnapTo = false;
        Assert.Equal(5, t.Preview(null).Count);
        Assert.Equal(0.5f, t.SnapGap, 3);
    }

    [Fact]
    public void RepeatedPointsAndFacingsAlongTheLine()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.Curve = false;
        t.Every = 4;
        t.Along = false;
        t.RandomYaw = false;
        t.Points.AddRange([new(40, 64), new(40, 64), new(56, 64)]);
        // One where the two points meet, then every 4 m.
        Assert.Equal(5, t.Preview(null).Count);
        // A kind with a known length turns it along the line.
        t.Along = true;
        t.Chosen.Clear();
        t.Chosen.Add("Post");
        var along = t.Preview(null);
        // (The first, where the points meet, has no direction to follow.)
        Assert.Equal(5, along.Count);
        Assert.All(along.Skip(1), o => Assert.Equal(0, o.Rotation.Y, 2));
        // A rectangle always faces along its sides, Along or not.
        t.Along = false;
        t.LineShape = PlaceTool.LineShapes.Rect;
        t.FigA = new Vector2(40, 40);
        t.FigB = new Vector2(52, 48);
        Assert.NotEmpty(t.Preview(null));
    }

    [Fact]
    public void LinesAndGridsStopAtTheMostAShapePlaces()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.Curve = false;
        t.Every = 0.1f;
        t.Points.AddRange([new(2, 2), new(126, 2), new(126, 126), new(2, 126), new(2, 2)]);
        Assert.InRange(t.Preview(null).Count, 1, PlaceTool.MaxShape);
        t.Mode = PlaceTool.Modes.Grid;
        t.Cell = 1;
        t.GridA = new Vector2(2, 2);
        t.GridB = new Vector2(126, 126);
        Assert.InRange(t.Preview(null).Count, 1, PlaceTool.MaxShape);
    }

    [Fact]
    public void ASinglePieceWithNothingNearOrNothingMeetingStaysPut()
    {
        var (t, _) = Tool();
        t.Chosen.Clear();
        t.Chosen.Add(Wall);
        t.AutoSnap();
        Assert.Equal(new Vector2(64, 64), Assert.Single(t.Preview(new Vector2(64, 64))).G);
        Assert.Null(t.SnappedTo);
        // A wall 8 m away: near, but no snap points meet.
        var (u, _) = Tool(new WorldScene.Thing(1, StableHash.Of(Wall), new Vector3(72 - 32, 30, 64 - 32), Vector3.Zero, 0, true));
        u.Chosen.Clear();
        u.Chosen.Add(Wall);
        u.AutoSnap();
        Assert.Equal(new Vector2(64, 64), Assert.Single(u.Preview(new Vector2(64, 64))).G);
        Assert.Null(u.SnappedTo);
        // Snapping off: no pieces looked at.
        u.SnapTo = false;
        Assert.Equal(new Vector2(64, 64), Assert.Single(u.Preview(new Vector2(64, 64))).G);
    }

    [Fact]
    public void AZoneTurnsOnceItHasPoints()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Zone;
        Assert.False(t.TurnShape(30));
        t.Points.AddRange([new(40, 40), new(80, 40), new(60, 80)]);
        Assert.True(t.TurnShape(30));
        Assert.Equal(30, t.ShapeTurn, 3);
    }

    [Fact]
    public void FiguresWithoutEndToEndKeepTheirSize()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.LineShape = PlaceTool.LineShapes.Circle;
        var (a, b) = (new Vector2(64, 64), new Vector2(72, 64));
        Assert.Equal(b, t.SnapFigure(a, b));
        t.FigA = a;
        t.FigB = b;
        Assert.Equal(35, Assert.Single(t.LineRuns()).Count);
        t.LineShape = PlaceTool.LineShapes.Rect;
        // End to end, a side of no length gets one whole piece.
        t.Chosen.Clear();
        t.Chosen.Add(Wall);
        t.AutoSnap();
        float len = t.EndsOf(Wall)!.Len;
        var fit = t.SnapFigure(new Vector2(40, 40), new Vector2(40, 40 + 3 * len));
        Assert.Equal(40 + len, fit.X, 3);
        Assert.Equal(40 + 3 * len, fit.Y, 3);
    }

    [Fact]
    public void ALoopNeedsThreePoints()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.Loop = true;
        t.Points.AddRange([new(40, 40), new(60, 40)]);
        var run = Assert.Single(t.LineRuns());
        Assert.NotEqual(run[0], run[^1]);
    }

    [Fact]
    public void HeightsArePickedAndNudged()
    {
        var none = new PlaceTool();
        Assert.Contains("(the ground there)", none.PickElevation(0, 12.3f));
        Assert.Equal(12.3f, none.Elev, 3);
        // A tree is no piece: the ground; a wall without a name known: its own.
        var wall = new WorldScene.Thing(2, StableHash.Of(Wall), new Vector3(0, 31, 0), Vector3.Zero, 0, true);
        var (t, _) = Tool(new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false), wall);
        Assert.Contains("(the ground there)", t.PickElevation(0, 30));
        t.NameOf = _ => null;
        Assert.Contains("(the top of ", t.PickElevation(1, 30));
        Assert.Equal(PlaceTool.Elevations.At, t.Elevation);
    }
}
