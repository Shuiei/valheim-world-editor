using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

using static TerrainEditor.Desktop.Tests.SavedSelectionsData;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Picking a kind for the Replace lists (Area and Select) with the eyedropper: a click in the 3D view gives the kind
/// clicked, and Esc or a click on nothing cancels.
/// </summary>
[Collection("Saved selections")]
public sealed class SavedSelectionsPickTests : SavedSelectionsFolder
{
    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Where a point in view space is on screen.
    /// </summary>
    private static Avalonia.Point ScreenOf(MainWindow w, Vector3 viewPos)
    {
        var q = Vector4.Transform(new Vector4(viewPos, 1), w.View.ViewProj);
        var size = w.View.Bounds.Size;
        return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }

    /// <summary>
    /// A click in the window, once its layout is done (a tool chosen changes it).
    /// </summary>
    private static void Press(MainWindow w, Avalonia.Point at, MouseButton button)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        w.MouseMove(at);
        w.MouseDown(at, button);
        w.MouseUp(at, button);
    }

    /// <summary>
    /// A shown main window with one half wall (given its box, as there are no models here) and the view looking at it:
    /// with a screen point on the wall and one on empty ground.
    /// </summary>
    private static (MainWindow W, EditSession S, Avalonia.Point Wall, Avalonia.Point Empty) PickWindow()
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, new WorldScene.Thing(11, Wall, new Vector3(40, 30, 40), Vector3.Zero, 0, true));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
        // The wall's box, as drawing would know it (no models here): 2 m wide, 2 m high.
        w.View.SetBoxes([(0, c + new Vector3(-1, 0, -0.2f), c + new Vector3(1, 2, 0.2f))]);
        w.View.SetCamera(c + new Vector3(0, 6, 8), c, 1);
        // Laid out and drawn once: hit tests use the last frame (with one app for the whole run, the
        // window is not always drawn yet here).
        w.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (w, s, ScreenOf(w, c + new Vector3(0, 1, 0.2f)), ScreenOf(w, c + new Vector3(0, 0, 3)));
    }

    [AvaloniaFact]
    public void TheEyedropperGivesTheClickedKindAndSwallowsTheClick()
    {
        var (w, _, wall, _) = PickWindow();
        w.Tools.ChooseSelect();
        int? picked = null;
        w.PickKind("Replace", p => picked = p);
        Assert.Equal("Click an object to pick its kind for Replace. Esc cancels.", w.MessageText.Text);
        Press(w, wall, MouseButton.Left);
        Assert.Equal(Wall, picked);
        // The click was the eyedropper's: the Select tool did not select the wall.
        Assert.Empty(w.View.Selected);
        Assert.Null(w.View.PickObjectOnce);
        // The next click is the tool's again.
        Press(w, wall, MouseButton.Left);
        Assert.Equal([0], w.View.Selected);
    }

    [AvaloniaFact]
    public void AClickOnNothingOrEscCancelsTheEyedropper()
    {
        var (w, _, _, empty) = PickWindow();
        w.Tools.ChooseMode(ToolMode.Area);
        w.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var hit = InputExtensions.InputHitTest(w, empty);
        Assert.True(hit == w.Surface, $"{empty} hit {hit?.GetType().Name} {(hit as Control)?.Parent?.GetType().Name}");
        bool called = false;
        w.PickKind("Replace", _ => called = true);
        Press(w, empty, MouseButton.Left);
        Assert.False(called);
        Assert.Equal("Nothing picked: click right on an object (only things that are shown can be picked).", w.MessageText.Text);
        // The Area tool did not get that click either.
        Assert.Empty(w.View.Area.Points);
        w.PickKind("Replace", _ => called = true);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Equal("Picking cancelled.", w.MessageText.Text);
        Assert.Null(w.View.PickObjectOnce);
        Assert.False(called);
        // A right click does not pick: it is the camera's.
        w.PickKind("Replace", _ => called = true);
        Press(w, empty, MouseButton.Right);
        Assert.NotNull(w.View.PickObjectOnce);
    }

    [AvaloniaFact]
    public void TheAreaReplacePickButtonFillsTheWithList()
    {
        var (w, s, wall, _) = PickWindow();
        var a = w.AreaPanel;
        var messages = new List<string>();
        a.Message += m => messages.Add(m.Shown);
        w.Tools.ChooseMode(ToolMode.Area);
        a.Choose(AreaPanel.Act.Replace);
        a.Refresh();
        Assert.True(a.ToPickButton.IsEffectivelyVisible);
        Click(a.ToPickButton);
        Assert.NotNull(w.View.PickObjectOnce);
        Press(w, wall, MouseButton.Left);
        string name = w.NameOfPrefab(Wall)!;
        // The wall is a kind the game makes: it is chosen.
        Assert.Contains(Wall, s.Scene.World.Creatable);
        Assert.Equal(name, a.ToBox.SelectedItem);
        Assert.Equal($"Replace with {name}.", messages[^1]);
    }

    [AvaloniaFact]
    public void UsePickedTakesOnlyKindsTheGameCanMake()
    {
        var (w, s, _, _) = PickWindow();
        var a = w.AreaPanel;
        var messages = new List<string>();
        a.Message += m => messages.Add(m.Shown);
        a.Refresh();
        var creatable = s.Scene.World.Creatable.Where(p => w.NameOfPrefab(p) != null).OrderBy(w.NameOfPrefab, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.NotEmpty(creatable);
        a.UsePicked(creatable[^1]);
        Assert.Equal(creatable.Count - 1, a.ToBox.SelectedIndex);
        Assert.Equal($"Replace with {w.NameOfPrefab(creatable[^1])}.", messages[^1]);
        a.UsePicked(12345);
        Assert.Equal(creatable.Count - 1, a.ToBox.SelectedIndex);
        Assert.Equal("12345 cannot be placed: the game has no such kind to copy.", messages[^1]);
    }

    [AvaloniaFact]
    public void TheSelectReplacePickButtonFillsReplaceWith()
    {
        var (w, _, wall, _) = PickWindow();
        var p = w.SelectPanel;
        var messages = new List<string>();
        p.Message += m => messages.Add(m.Shown);
        w.Tools.ChooseSelect();
        Click(p.ReplacePickButton);
        Press(w, wall, MouseButton.Left);
        int i = p.ReplaceKinds.IndexOf(Wall);
        string name = w.NameOfPrefab(Wall)!;
        Assert.True(i >= 0);
        Assert.Equal(i, p.ReplaceBox.SelectedIndex);
        Assert.Equal($"Replace with {name}.", messages[^1]);
        // A kind not in the list.
        p.UsePicked(777);
        Assert.Equal("777 cannot be placed: the game has no such kind to copy.", messages[^1]);
        p.NameOf = null;
        p.UsePicked(778);
        Assert.Equal("778 cannot be placed: the game has no such kind to copy.", messages[^1]);
    }
}
