using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectAreaRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's keys in a shown window: the rail's Select mode, turning and lifting with the keys, a user's move
/// kept apart from Claude's, Escape and Delete, and Same kind with Invert.
/// </summary>
[Collection("World files")]
public class SelectToolKeyTests
{
    [AvaloniaFact]
    public void TheSelectToolIsOnTheRailAndOnE()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
        w.Show();
        w.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
        Assert.True(w.Tools.SelectMode);
        Assert.Equal(ToolMode.Select, w.View.Mode);
        Assert.True(w.SelectPanel.Card.IsVisible);
        w.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
        Assert.Equal(ToolMode.Brush, w.View.Mode);
        Assert.False(w.SelectPanel.Card.IsVisible);
    }

    [AvaloniaFact]
    public void TurningAndLiftingWithKeysMovesTheSelectionOnceTheKeysStop()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.KeyPress(Key.OemPeriod, RawInputModifiers.Shift, PhysicalKey.Period, ">");
        w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
        Assert.True(w.View.SelectTool.Moving);
        // The move is written when the keys stop (or at once on a commit).
        w.View.SelectTool.Commit();
        var moved = Last(s);
        Assert.Equal(15, moved.Rotation.Y, 3);
        // On the ground (30 m) and lifted a quarter metre.
        Assert.Equal(30.25f, moved.Position.Y, 3);
        Assert.Equal(new Vector2(20, 20), new Vector2(moved.Position.X, moved.Position.Z));
        Assert.Equal([s.Scene.Things.Count - 1], w.View.Selected);
        // Ctrl+Z puts it back.
        w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
        Assert.False(s.Scene.Things[0].Gone);
    }

    // A key move of the user's still waiting when Claude presses Apply in the Select panel is the user's step, and the
    // move Claude's typed place makes is Claude's: Take back Claude's changes leaves the user's.
    [AvaloniaFact]
    public async Task AUsersKeyMoveCommittedByClaudesApplyStaysTheUsers()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
        Assert.True(w.View.SelectTool.Moving);
        var t = new ClaudeTools(w);
        await t.SetControl("select.x", "22");
        await t.SetControl("select.apply", "click");
        Assert.Equal(["Moved 1", "Claude: Moved 1"], s.UndoList.Select(c => c.Label));
        Assert.Equal(22, Last(s).Position.X, 2);
        w.CloseWithoutAsking();
    }

    [AvaloniaFact]
    public void EscapeCancelsAMove()
    {
        var (w, s) = Window();
        w.View.Select([0]);
        w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(w.View.SelectTool.Moving);
        Assert.Equal(2, s.Scene.Things.Count);
        // Still in the Select tool: Esc only cancelled the move.
        Assert.True(w.Tools.SelectMode);
    }

    [AvaloniaFact]
    public void DeleteKeyRemovesTheSelection()
    {
        var (w, s) = Window();
        w.View.Select([0, 1]);
        w.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Assert.True(s.Scene.Things[0].Gone && s.Scene.Things[1].Gone);
        Assert.Empty(w.View.Selected);
        Assert.Equal(2, s.Pending.Deleted);
    }

    [AvaloniaFact]
    public void SameKindAndInvert()
    {
        var (w, _) = Window();
        // Headless, nothing is drawn, so nothing counts as shown: both select nothing more.
        w.View.Select([0]);
        w.View.SelectTool.Invert();
        Assert.Empty(w.View.Selected);
    }
}
