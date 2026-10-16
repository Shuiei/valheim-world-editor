using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a thing's outline marks (GlView.OutlineBox): a standing tree a column round its root, the full height of its
/// box; a log, a stump or anything else its whole box.
/// </summary>
public class GlViewOutlineBoxTests
{
    [Fact]
    public void AStandingTreeIsOutlinedByAColumnRoundItsRoot()
    {
        // A beech 14 m across and 20 m tall, its root at 10, 30, -4: a column 1.4 m either way of the root.
        var box = (new Vector3(3, 30, -11), new Vector3(17, 50, 3));
        var (lo, hi) = GlView.OutlineBox(ObjectKind.Trees, box, new Vector3(10, 30, -4));
        Assert.Equal(new Vector3(8.6f, 30, -5.4f), lo);
        Assert.Equal(new Vector3(11.4f, 50, -2.6f), hi);
        // A thin sapling: never under half a metre either way; a huge oak: never over two.
        Assert.Equal(0.5f, GlView.OutlineBox(ObjectKind.Trees, (new Vector3(-1, 0, -1), new Vector3(1, 6, 1)), Vector3.Zero).Max.X);
        Assert.Equal(2f, GlView.OutlineBox(ObjectKind.Trees, (new Vector3(-20, 0, -20), new Vector3(20, 30, 20)), Vector3.Zero).Max.X);
    }

    [Fact]
    public void LogsStumpsAndOtherThingsKeepTheirBox()
    {
        // A fallen log: 8 m long and 2 m high is wider than tall.
        var log = (new Vector3(0, 30, 0), new Vector3(8, 32, 2));
        Assert.Equal(log, GlView.OutlineBox(ObjectKind.Trees, log, new Vector3(0, 30, 1)));
        var rock = (new Vector3(0, 30, 0), new Vector3(4, 10, 4));
        Assert.Equal(rock, GlView.OutlineBox(ObjectKind.Rocks, rock, new Vector3(2, 30, 2)));
    }
}
