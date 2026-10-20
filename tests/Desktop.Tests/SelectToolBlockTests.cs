using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A move not put on the ground: the whole selection goes as one block that keeps its shape, up or down with the
/// ground under its middle.
/// </summary>
[Collection("World files")]
public class SelectToolBlockTests
{
    // Two walls and a tree moved 20 m east, their middle onto a 4 m step: all three go up 4 m, though the first wall
    // and the tree end up off the step, so the walls stay 1 m apart in height and the tree 3 m over the first.
    [AvaloniaFact]
    public void TheSelectionFollowsTheGroundUnderItsMiddleAsOneBlock()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 44, 31, 40), T(3, Beech, 39, 33, 40, false));
        var s = v.S.Scene;
        // The ground steps up 4 m east of world x 60.5.
        for (int i = 0; i < s.Heights.Length; i++)
        {
            s.Heights[i] = i % s.W - (s.W - 1) / 2f + s.Cx > 60.5f ? 34 : 30;
        }
        v.Tool.OnGround = false;
        v.Tool.SnapToPieces = false;
        v.W.View.Select([0, 1, 2]);
        v.Drag(false, (40, 40), (50, 40), (60, 40));
        var moved = s.Things.Where(t => !t.Gone).OrderBy(t => t.Position.X).ToList();
        Assert.Equal([59f, 60f, 64f], moved.Select(t => MathF.Round(t.Position.X, 1)));
        Assert.Equal([37f, 34f, 35f], moved.Select(t => MathF.Round(t.Position.Y, 2)));
    }

    // How far the block follows the ground: as much as the ground under its middle goes up or down; nothing when the
    // middle is, or was, past the area's edge, where there is no ground.
    [AvaloniaFact]
    public void TheBlockFollowsNothingPastTheAreasEdge()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        var s = v.S.Scene;
        for (int i = 0; i < s.Heights.Length; i++)
        {
            s.Heights[i] = i % s.W - (s.W - 1) / 2f + s.Cx > 60.5f ? 34 : 30;
        }
        float Follow(float dx) =>
            (float)PrivateMethods.Call(typeof(SelectTool), "FollowGround", v.Tool, new SelectTool.Move { Items = [], Cx = 40, Cz = 40, Dx = dx })!;
        Assert.Equal(4, Follow(21), 2);
        Assert.Equal(0, Follow(10_000));
        Assert.Equal(0, (float)PrivateMethods.Call(typeof(SelectTool), "FollowGround", v.Tool, new SelectTool.Move { Items = [], Cx = 10_000, Cz = 40, Dx = -9_960 })!);
    }
}
