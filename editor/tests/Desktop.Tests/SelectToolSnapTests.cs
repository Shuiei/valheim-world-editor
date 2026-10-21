using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's snapping bookkeeping: the pieces around a moved selection are filed under their metre cells, the
/// cells about a point are read in list order, and a move with no unlinked object has no nearest snap point.
/// </summary>
public class SelectToolSnapTests
{
    private static readonly int Wall = StableHash.Of("wood_wall_half");

    [Fact]
    public void PointsAroundReadsTheNineCellsAboutThePointInListOrder()
    {
        // The point is in cell (0, 0): cells (-1..1, -1..1) count, (2, 0) and (3, 3) do not.
        var mv = new SelectTool.Move
        {
            Items = [],
            OthersAt = new()
            {
                [(0, 0)] = [2, 0],
                [(1, -1)] = [5],
                [(-1, 1)] = [3],
                [(2, 0)] = [9],
                [(3, 3)] = [8],
            },
        };
        Assert.Equal<int>([0, 2, 3, 5], SelectTool.PointsAround(mv, new Vector3(0.5f, 0, 0.5f)));
    }

    [AvaloniaFact]
    public void GatherOthersFilesEveryGatheredPointUnderItsCell()
    {
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, Wall, new Vector3(20, 30, 20), Vector3.Zero, 0, true),
            new WorldScene.Thing(2, Wall, new Vector3(40, 30, 40), Vector3.Zero, 0, true));
        var v = new GlView();
        v.Show(s.Scene, null);
        var mv = new SelectTool.Move { Items = [new SelectTool.Item { Index = 1, Thing = s.Scene.Things[1], Linked = false }] };
        v.SelectTool.GatherOthers(mv, 20, 20);
        Assert.NotNull(mv.Others);
        Assert.Equal(20f, mv.Ox);
        Assert.Equal(20f, mv.Oz);
        // Every point is listed once, under the cell it is in.
        Assert.Equal(mv.Others!.Count, mv.OthersAt.Values.Sum(indices => indices.Count));
        foreach (var (cell, indices) in mv.OthersAt)
        {
            foreach (int k in indices)
            {
                var p = mv.Others[k].P;
                Assert.Equal(cell, ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Z)));
            }
        }
    }

    [AvaloniaFact]
    public void NearestSnapIsNullWhenEveryObjectIsLinked()
    {
        // Linked objects (parts of a group) are not snapped: nothing to look for.
        var v = new GlView();
        var mv = new SelectTool.Move
        {
            Items = [new SelectTool.Item { Index = 0, Thing = new WorldScene.Thing(0, Wall, Vector3.Zero, Vector3.Zero, 0, true), Linked = true }],
            Others = [(new Vector3(0.5f, 0, 0.5f), "wood_wall_half")],
            OthersAt = new() { [(0, 0)] = [0] },
        };
        Assert.Null(v.SelectTool.NearestSnap(mv));
    }
}
