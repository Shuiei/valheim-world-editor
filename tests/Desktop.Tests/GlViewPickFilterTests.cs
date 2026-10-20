using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select panel's Pick (GlView.PickFilter): everything shown, built pieces only, or nature only; what it leaves out
/// is neither picked nor in the way of what is behind it, and other tools (Place) pick as before.
/// </summary>
[Collection("World files")]
public class GlViewPickFilterTests
{
    /// <summary>
    /// Looking straight down: a wall under a tree whose crown (8 m across, up to 38 m) hides it, and a tree beside them.
    /// </summary>
    private static View WallUnderATree()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Beech, 40, 30, 40, false), T(3, Beech, 48, 30, 48, false));
        var c = v.S.Scene;
        v.W.View.SetBoxes([(1, new Vector3(36 - c.Cx, 30, -(44 - c.Cz)), new Vector3(44 - c.Cx, 38, -(36 - c.Cz)))]);
        return v;
    }

    /// <summary>A box drawn with Alt held from one world point on the ground to another.</summary>
    private static void Box(View v, (float X, float Z) from, (float X, float Z) to)
    {
        v.Tool.Down(v.At(from.X, 30, from.Z), v.Size, KeyModifiers.Alt, 1);
        v.Tool.Moved(v.At(to.X, 30, to.Z), v.Size);
        v.Tool.Up(v.At(to.X, 30, to.Z), v.Size, KeyModifiers.None);
    }

    [AvaloniaFact]
    public void BuiltPiecesOnlyTakesTheWallThroughTheTree()
    {
        var v = WallUnderATree();
        Box(v, (37, 37), (43, 43));
        Assert.Equal([1], v.Selected);
        v.Tool.Picks = GlView.PickKinds.Built;
        Box(v, (37, 37), (43, 43));
        Assert.Equal([0], v.Selected);
        v.Tool.SelectAll();
        Assert.Equal([0], v.Selected);
        v.Click(48, 48);
        Assert.Empty(v.Selected);
    }

    [AvaloniaFact]
    public void NatureOnlyTakesTheTrees()
    {
        var v = WallUnderATree();
        v.Tool.Picks = GlView.PickKinds.Nature;
        v.Tool.SelectAll();
        Assert.Equal([1, 2], v.Selected);
        v.Tool.Picks = GlView.PickKinds.Everything;
        v.Tool.SelectAll();
        Assert.Equal([0, 1, 2], v.Selected);
    }

    [AvaloniaFact]
    public void OtherToolsPickEveryKind()
    {
        var v = WallUnderATree();
        v.Tool.Picks = GlView.PickKinds.Built;
        Assert.False(v.W.View.IsPickable(2));
        v.W.Tools.ChooseMode(ToolMode.Place);
        Assert.True(v.W.View.IsPickable(2));
    }
}
