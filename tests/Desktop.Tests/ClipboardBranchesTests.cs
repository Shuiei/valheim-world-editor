using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Copy and paste, remaining branches: an object with no known name, ground-only copies stacked upwards, and a scaled
/// building clearing its site under a mask.
/// </summary>
public class ClipboardBranchesTests
{
    [Fact]
    public void AnObjectWithNoKnownNameIsNamedByItsHash()
    {
        int prefab = StableHash.Of("Beech1");
        var s = TestScenes.Flat(1, new WorldScene.Thing(1, prefab, new Vector3(0, 31, 0), Vector3.Zero, 1, false));
        var c = CopyData.FromThings(s.Scene, [0], _ => null, (_, _) => 30);
        Assert.NotNull(c);
        var o = Assert.Single(c.Objects);
        Assert.Equal(prefab.ToString(System.Globalization.CultureInfo.InvariantCulture), o.Name);
        Assert.Equal(1, o.Dy, 3);
    }

    [Fact]
    public void GroundOnlyCopiesStackTwoMetresApart()
    {
        var clip = new CopyData
        {
            W = 1,
            H = 1,
            Rel = [0f],
            Wt = [1f],
            Pnt = [-1f, -1, -1, -1],
            Objects = [],
            Poly = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)],
            Name = "Bit",
        };
        var paste = new PasteTool { Clip = clip, Copies = 2, Gap = 1, Direction = PasteTool.Along.Up };
        var (objs, outlines) = paste.Preview(new Vector2(30, 30), _ => 30);
        Assert.Empty(objs);
        Assert.Equal(2, outlines.Count);
    }

    [Fact]
    public void AScaledBuildingClearsItsSiteUnderAMask()
    {
        var s = TestScenes.Flat(2);
        s.Shape(60, 60, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 6, 3, "mound");
        float before = s.Ground.HeightOf(60 * s.Ground.W + 60), foot = s.Ground.HeightOf(60 * s.Ground.W + 66);
        var wall = new CopyData
        {
            W = 1,
            H = 1,
            Rel = [float.NaN],
            Wt = [0f],
            Pnt = [-1f, -1, -1, -1],
            Objects = [new CopyData.Obj(StableHash.Of("woodwall"), "woodwall", -6, 0, 1, new Vector3(0, 90, 0), 1, null)],
            Poly = [new(-8, -2), new(2, -2), new(2, 2), new(-8, 2)],
            Name = "Wall",
        };
        var (touched, _, add) = new PasteTool { Clip = wall }.Apply(s.Ground, new Vector2(66, 60), _ => 1f);
        Assert.Single(add);
        Assert.NotEmpty(touched);
        Assert.True(before > foot + 2);
        Assert.Equal(foot, s.Ground.HeightOf(60 * s.Ground.W + 60), 2);
    }

    [Fact]
    public void AGroundBoxWithoutPiecesIsEmpty()
    {
        var none = new List<(float X0, float Z0, float X1, float Z1, float Bottom)>();
        Assert.Equal((0, 0, 0, 0), CopyData.GroundBox(none, 4f));
    }

    [Fact]
    public void AGroundBoxReachesWholeMetresPastThePieces()
    {
        var one = new List<(float X0, float Z0, float X1, float Z1, float Bottom)> { (1.5f, 2.2f, 3.4f, 5.1f, 0f) };
        Assert.Equal((0, 5, 1, 7), CopyData.GroundBox(one, 1f));
    }

    [Fact]
    public void AShapedPointTakesTheCopyAndIsRecordedAsShaped()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        var c = new CopyData { W = 1, H = 1, Rel = [0.5f], Wt = [1f], Pnt = [-1f], Objects = [], Poly = [] };
        var shaped = new HashSet<int>();
        var touched = new List<int>();
        new PasteTool { Clip = c }.ShapeCell(g, c, new Vector2(10, 10), 30f, 10, 10, null, shaped, touched);
        Assert.Equal(new[] { 10 * 129 + 10 }, touched);
        Assert.Contains(10 * 129 + 10, shaped);
    }

    [Fact]
    public void AShapedRectangleTouchesEveryPointTheCopyCovers()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        var c = new CopyData { W = 3, H = 3, Rel = [.. Enumerable.Repeat(0.5f, 9)], Wt = [.. Enumerable.Repeat(1f, 9)], Pnt = [.. Enumerable.Repeat(-1f, 36)], Objects = [], Poly = [] };
        var touched = new List<int>();
        new PasteTool { Clip = c }.ShapeGround(g, c, new Vector2(10, 10), 30f, 9, 11, 9, 11, null, [], touched);
        Assert.Equal(9, touched.Count);
    }

    [Fact]
    public void EachObjectOfACopyIsAddedAsANewObject()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        var objects = new List<CopyData.Obj>
        {
            new(StableHash.Of("editor_test_nothing"), "a", 0, 0, 0, Vector3.Zero, 1, null),
            new(StableHash.Of("editor_test_nothing"), "b", 1, 0, 0, Vector3.Zero, 1, null),
        };
        var c = new CopyData { W = 1, H = 1, Rel = [], Wt = [], Pnt = [], Objects = objects, Poly = [] };
        var add = new List<(NewObject, bool)>();
        new PasteTool { Clip = c }.AddObjects(g, c, new Vector2(10, 10), 0f, 30f, add);
        Assert.Equal(2, add.Count);
    }

    [Fact]
    public void AnObjectThatIsNotAPieceIsNotAddedAsOne()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        var objects = new List<CopyData.Obj> { new(StableHash.Of("editor_test_nothing"), "a", 0, 0, 0, Vector3.Zero, 1, null) };
        var c = new CopyData { W = 1, H = 1, Rel = [], Wt = [], Pnt = [], Objects = objects, Poly = [] };
        var add = new List<(NewObject, bool)>();
        new PasteTool { Clip = c }.AddObjects(g, c, new Vector2(10, 10), 0f, 30f, add);
        Assert.All(add, a => Assert.False(a.Item2));
    }

    [Fact]
    public void AnObjectThatIsNotABuildablePieceDigsNothing()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        var placed = new List<(NewObject O, bool Piece)> { (new NewObject(0, StableHash.Of("editor_test_nothing"), Vector3.Zero, Vector3.Zero, 1), true) };
        Assert.Empty(PasteTool.Digs(g, placed));
    }

    [Fact]
    public void ACopysSpotsAreOneCopyWithEachObjectPlaced()
    {
        var objects = new List<CopyData.Obj>
        {
            new(StableHash.Of("editor_test_nothing"), "a", 0, 0, 0, Vector3.Zero, 1, null),
            new(StableHash.Of("editor_test_nothing"), "b", 1, 0, 0, Vector3.Zero, 1, null),
        };
        var p = new PasteTool { Clip = new CopyData { W = 1, H = 1, Rel = [], Wt = [], Pnt = [], Objects = objects, Poly = [] } };
        var copies = p.CopySpots(p.Clip!, new Vector2(10, 10), _ => 0f).ToList();
        var (_, spots) = Assert.Single(copies);
        Assert.Equal(2, spots.Count());
    }
}
