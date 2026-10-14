using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D editor's keys in the window: V and L for the right-hand panels, the Paste tool's F, Esc and Ctrl + wheel, the
/// Area tool's Enter and Backspace, the Dungeon tool's R and Delete, Esc for the Measure tool's tape, I and Esc for the
/// inspector; the walking and flying eyes; and Inspect on a runestone or on more than one object.
/// </summary>
[Collection("World files")]
public class MainWindowKeysTests
{
    /// <summary>
    /// A shown window editing a flat 2 × 2 zone area (ground at 30 m) with these objects.
    /// </summary>
    private static (MainWindow W, EditSession S) Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return (w, s);
    }

    /// <summary>
    /// Presses a key (with these modifiers) on the window.
    /// </summary>
    private static void Press(MainWindow w, Key key, PhysicalKey physical, string? text, RawInputModifiers mods = RawInputModifiers.None)
    {
        w.KeyPress(key, mods, physical, text);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void VAndLOpenAndCloseTheRightHandPanels()
    {
        var (w, _) = Open();
        Assert.True(w.ViewPanelCard.IsVisible);
        Press(w, Key.V, PhysicalKey.V, "v");
        Assert.False(w.ViewPanelCard.IsVisible);
        Press(w, Key.V, PhysicalKey.V, "v");
        Assert.True(w.ViewPanelCard.IsVisible);
        Press(w, Key.L, PhysicalKey.L, "l");
        Assert.True(w.History.Card.IsVisible);
        Assert.False(w.ViewPanelCard.IsVisible);
        Press(w, Key.L, PhysicalKey.L, "l");
        Assert.False(w.History.Card.IsVisible);
    }

    [AvaloniaFact]
    public void ThePasteAndAreaToolsKeys()
    {
        var (w, _) = Open();
        w.View.Paste.Clip = BlueprintsPanelRun.Clip("Hut", "Beech1");
        Press(w, Key.V, PhysicalKey.V, "v", RawInputModifiers.Control);
        Assert.Equal(ToolMode.Paste, w.Tools.Mode);
        // F mirrors.
        bool mirror = w.View.Paste.Mirror;
        Press(w, Key.F, PhysicalKey.F, "f");
        Assert.NotEqual(mirror, w.View.Paste.Mirror);
        // Ctrl + wheel lifts the paste (Shift: by 0.1 m).
        decimal offset = w.PastePanel.OffsetBox.Value ?? 0;
        Assert.True(w.View.CtrlWheel!(1, false));
        Assert.Equal(offset + 0.5m, w.PastePanel.OffsetBox.Value);
        Assert.StartsWith("Paste height: ", w.MessageText.Text);
        Assert.True(w.View.CtrlWheel!(-1, true));
        Assert.Equal(offset + 0.4m, w.PastePanel.OffsetBox.Value);
        // Esc: back to the Area tool, where Ctrl + wheel is the view's.
        Press(w, Key.Escape, PhysicalKey.Escape, null);
        Assert.Equal(ToolMode.Area, w.Tools.Mode);
        Assert.False(w.View.CtrlWheel!(1, false));
        // Enter with no area drawn: nothing to apply; Backspace with no point does nothing.
        Press(w, Key.Enter, PhysicalKey.Enter, "\r");
        Assert.Equal("Select an area first.", w.MessageText.Text);
        Press(w, Key.Back, PhysicalKey.Backspace, null);
        Assert.Empty(w.View.Area.Points);
    }

    [AvaloniaFact]
    public void TheDungeonAndMeasureToolsKeysAndTheEyes()
    {
        var (w, _) = Open();
        w.Tools.ChooseMode(ToolMode.Dungeon);
        Assert.True(w.DungeonPanel.Card.IsVisible);
        int join = w.View.Dungeon.Join;
        Press(w, Key.R, PhysicalKey.R, "r");
        Assert.Equal(join + 1, w.View.Dungeon.Join);
        // Delete with no room selected: nothing.
        Press(w, Key.Delete, PhysicalKey.Delete, null);
        Assert.Equal(ToolMode.Dungeon, w.Tools.Mode);
        // Esc takes the Measure tool's tape away.
        w.Tools.ChooseMode(ToolMode.Measure);
        w.View.Tape.Down(new Vector3(0, 30, 0));
        Assert.NotNull(w.View.Tape.A);
        Press(w, Key.Escape, PhysicalKey.Escape, null);
        Assert.Null(w.View.Tape.A);
        Assert.Equal(ToolMode.Measure, w.Tools.Mode);
        // Walking, flying, and back.
        w.View.CycleEye();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(GlView.EyeMode.Walk, w.View.Eye);
        w.View.CycleEye();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(GlView.EyeMode.Fly, w.View.Eye);
        w.View.CycleEye();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(GlView.EyeMode.Orbit, w.View.Eye);
    }

    [AvaloniaFact]
    public void CtrlXCutsTheSelectionToTheClipboardAndCtrlZBringsItBack()
    {
        var stone = new WorldScene.Thing(4, StableHash.Of("Runestone_Meadows"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
        var beech = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 1, false);
        var (w, s) = Open(stone, beech);
        w.Tools.ChooseSelect();
        w.View.Select([1]);
        Press(w, Key.X, PhysicalKey.X, "x", RawInputModifiers.Control);
        Assert.True(s.Scene.Things[1].Gone);
        Assert.False(s.Scene.Things[0].Gone);
        Assert.Empty(w.View.Selected);
        Assert.Equal([1], w.View.Paste.Clip!.Taken);
        Assert.Equal(StableHash.Of("Beech1"), w.View.Paste.Clip.Objects.Single().Prefab);
        Assert.StartsWith("Cut 1 object(s): gone from here", w.MessageText.Text);
        Assert.True(s.CanUndo);
        Press(w, Key.Z, PhysicalKey.Z, "z", RawInputModifiers.Control);
        Assert.False(s.Scene.Things[1].Gone);
        // The copy stays on the clipboard, to be pasted after the undo too.
        Assert.Single(w.View.Paste.Clip.Objects);
    }

    [AvaloniaFact]
    public void ARunestoneIsNotInspectedNorAreTwoObjects()
    {
        var stone = new WorldScene.Thing(4, StableHash.Of("Runestone_Meadows"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);
        var beech = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 1, false);
        var (w, _) = Open(stone, beech);
        w.View.Select([0]);
        w.Inspect();
        Assert.Equal("A runestone holds no data of its own: the game builds it from its location. It can only be deleted.", w.MessageText.Text);
        Assert.False(w.Inspector.IsOpen);
        w.MessageText.Text = "";
        w.View.Select([0, 1]);
        w.Inspect();
        Assert.False(w.Inspector.IsOpen);
        Assert.Equal("", w.MessageText.Text);
    }

    [AvaloniaFact]
    public void IInspectsTheSelectedChestAndEscClosesIt()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            int chestKind = StableHash.Of("piece_chest_wood");
            var world = WorldSave.Load(dir);
            var at = world.Objects.First(o => o.Prefab == chestKind).Position;
            var scene = WorldScene.Load(dir, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            w.Tools.ChooseSelect();
            int chest = scene.Things.FindIndex(t => t.Prefab == chestKind);
            w.View.Select([chest]);
            Dispatcher.UIThread.RunJobs();
            Press(w, Key.I, PhysicalKey.I, "i");
            Assert.True(w.Inspector.IsOpen);
            Assert.False(w.ViewPanelCard.IsVisible);
            Press(w, Key.Escape, PhysicalKey.Escape, null);
            Assert.False(w.Inspector.IsOpen);
            Assert.True(w.ViewPanelCard.IsVisible);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
