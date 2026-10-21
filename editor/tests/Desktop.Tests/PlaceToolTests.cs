using System.Numerics;
using TerrainEditor.Save;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedPlaceTool;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's placement on the ground: the wall's snap points, the brush scattering inside itself, single pieces,
/// lines and their layers, and the end-to-end pieces.
/// </summary>
public class PlaceToolTests
{
    private const string Wall = "woodwall";

    /// <summary>
    /// The placement's turn in degrees, 0 to 360.
    /// </summary>
    private static float Yaw(PlaceTool.Placement p) => (p.Rotation.Y % 360 + 360) % 360;

    [Fact]
    public void TheWallHasSnapPoints()
    {
        Assert.True(PlaceTool.IsPiece(Wall));
        Assert.False(PlaceTool.IsPiece("Beech1"));
    }

    [Fact]
    public void TheBrushScattersInsideItKeepingTheSpacing()
    {
        var (t, _) = Tool();
        t.Brush.Radius = 15;
        t.Density = 5;
        t.Spacing = 3;
        var p = t.Preview(new Vector2(64, 64));
        Assert.InRange(p.Count, 10, 40);
        Assert.All(p, o => Assert.True(Vector2.Distance(o.G, new Vector2(64, 64)) <= 15.01f));
        for (int i = 0; i < p.Count; i++)
        {
            for (int j = i + 1; j < p.Count; j++)
            {
                Assert.True(Vector2.Distance(p[i].G, p[j].G) >= 3 - 1e-3f);
            }
        }
        Assert.All(p, o => Assert.InRange(o.Scale, 0.8f, 1.2f));
        // On the ground (a little sunk, like the game places them).
        Assert.All(p, o => Assert.Equal(29.95f, o.Position.Y, 3));
        // The layout stays the same while the cursor moves.
        var q = t.Preview(new Vector2(70, 64));
        Assert.Equal(p.Count, q.Count);
        Assert.True(Vector2.Distance(p[0].G + new Vector2(6, 0), q[0].G) < 1e-3f);
    }

    [Fact]
    public void ASmallBrushStillPlacesOne()
    {
        var (t, _) = Tool();
        t.Brush.Radius = 2;
        t.Density = 3;
        Assert.Single(t.Preview(new Vector2(64, 64)));
    }

    [Fact]
    public void SingleIsExactlyAtTheCursorAndStaysAwayFromOthers()
    {
        var (t, _) = Tool(new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 1, false));
        t.OneAtATime = true;
        var p = Assert.Single(t.Preview(new Vector2(50, 50)));
        Assert.Equal(new Vector2(50, 50), p.G);
        // Right on top of a standing tree (world 32, 32 is grid 64, 64): nothing.
        Assert.Empty(t.Preview(new Vector2(64.1f, 64)));
    }

    [Fact]
    public void ALineHasOneEveryFewMetresFacingAlongIt()
    {
        var (t, _) = Tool();
        t.Mode = PlaceTool.Modes.Line;
        t.Every = 4;
        t.Points.Add(new Vector2(20, 64));
        t.Points.Add(new Vector2(60, 64));
        var p = t.Preview(null);
        Assert.Equal(11, p.Count);
        Assert.All(p, o => Assert.Equal(90, Yaw(o), 2));
        t.Along = false;
        t.RandomYaw = false;
        Assert.All(t.Preview(null), o => Assert.Equal(0, Yaw(o), 2));
    }

    [Fact]
    public void EndToEndPiecesMeetAndStack()
    {
        var (t, _) = Tool();
        t.Chosen.Clear();
        t.Chosen.Add(Wall);
        t.AutoSnap();
        Assert.True(t.EndToEnd);
        Assert.True(t.OneAtATime);
        t.Mode = PlaceTool.Modes.Line;
        t.Points.Add(new Vector2(20, 64));
        t.Points.Add(new Vector2(41, 64));
        var e = t.EndsOf(Wall)!;
        var p = t.Preview(null);
        int n = (int)(21 / e.Len);
        Assert.Equal(n, p.Count);
        // Each piece starts where the last one ends.
        for (int i = 1; i < p.Count; i++)
        {
            Assert.Equal(e.Len, Vector2.Distance(p[i].G, p[i - 1].G), 2);
        }
        Assert.Equal(21 - n * e.Len, t.SnapGap, 2);
        Assert.All(p, o => Assert.Equal(0, o.Scale));
        t.Layers = 2;
        var two = t.Preview(null);
        Assert.Equal(2 * n, two.Count);
        Assert.Equal(e.H, two[1].Position.Y - two[0].Position.Y, 2);
    }

    [Fact]
    public void LayersStopAtTheShapesCap()
    {
        var (t, _) = Tool();
        t.Chosen.Clear();
        t.Chosen.Add(Wall);
        t.AutoSnap();
        t.Mode = PlaceTool.Modes.Line;
        // A zigzag of about 130 walls, 20 high: some 2600 placements.
        foreach (var (x, z) in new[] { (2, 2), (126, 2), (126, 10), (2, 10), (2, 20) })
        {
            t.Points.Add(new Vector2(x, z));
        }
        t.Layers = 20;
        Assert.Equal(PlaceTool.MaxShape, t.Preview(null).Count);
    }
}
